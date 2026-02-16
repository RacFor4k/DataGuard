using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Server.Middleware
{
    public class IdempotencyMiddleware
    {
        private readonly IMemoryCache _memoryCache;
        private readonly RequestDelegate _next;
        private static readonly HashSet<string> SafeHttpMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "POST",
            "PUT",
            "PATCH",
            "DELETE"
        };
        
        // Regex for validating idempotency key format (alphanumeric, hyphens, underscores)
        private static readonly Regex IdempotencyKeyRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
        
        // Maximum length for idempotency keys to prevent DoS attacks
        private const int MaxIdempotencyKeyLength = 255;
        
        // Cache entry options with sliding expiration
        private static readonly MemoryCacheEntryOptions CacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(60),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };

        public IdempotencyMiddleware(IMemoryCache memoryCache, RequestDelegate next)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only process requests with safe HTTP methods
            if (!SafeHttpMethods.Contains(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Check for idempotency key header
            if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues))
            {
                await _next(context);
                return;
            }

            var key = keyValues.FirstOrDefault();
            if (string.IsNullOrEmpty(key))
            {
                await _next(context);
                return;
            }

            // Validate idempotency key format and length
            if (!IsValidIdempotencyKey(key))
            {
                context.Response.StatusCode = 400; // Bad Request
                await context.Response.WriteAsync("Invalid idempotency key format");
                return;
            }

            // Get user identifier
            if (!context.Request.Headers.TryGetValue("User-uuid", out var userUuidValues))
            {
                await _next(context);
                return;
            }

            var userUuid = userUuidValues.FirstOrDefault();
            if (string.IsNullOrEmpty(userUuid))
            {
                await _next(context);
                return;
            }

            // Construct cache key securely
            var cacheKey = $"idempotency:{SanitizeForCacheKey(userUuid)}:{SanitizeForCacheKey(key)}";

            // Check if response is already cached
            if (_memoryCache.TryGetValue(cacheKey, out IdempotencyResponse cachedResponse))
            {
                // Return cached response
                context.Response.StatusCode = cachedResponse.StatusCode;
                
                foreach (var header in cachedResponse.Headers)
                {
                    context.Response.Headers.Append(header.Key, header.Value);
                }
                
                await context.Response.WriteAsync(cachedResponse.Body);
                return;
            }

            // Capture the original response stream to store the response
            using var originalResponseBodyStream = context.Response.Body;
            
            using var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            try
            {
                // Process the request
                await _next(context);

                // Only cache successful responses (2xx status codes)
                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                {
                    // Read the response body
                    responseStream.Seek(0, SeekOrigin.Begin);
                    var responseBody = await new StreamReader(responseStream).ReadToEndAsync();

                    // Create response object to cache
                    var responseToCache = new IdempotencyResponse
                    {
                        StatusCode = context.Response.StatusCode,
                        Body = responseBody,
                        Headers = context.Response.Headers
                            .Where(h => !h.Key.StartsWith("x-") || h.Key.Equals("x-request-id", StringComparison.OrdinalIgnoreCase))
                            .ToDictionary(h => h.Key, h => h.Value.ToString())
                    };

                    // Store in cache
                    _memoryCache.Set(cacheKey, responseToCache, CacheOptions);
                    
                    // Write response to original stream
                    responseStream.Seek(0, SeekOrigin.Begin);
                    await responseStream.CopyToAsync(originalResponseBodyStream);
                }
                else
                {
                    // For non-successful responses, copy to original stream without caching
                    responseStream.Seek(0, SeekOrigin.Begin);
                    await responseStream.CopyToAsync(originalResponseBodyStream);
                }
            }
            finally
            {
                context.Response.Body = originalResponseBodyStream;
            }
        }

        private static bool IsValidIdempotencyKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxIdempotencyKeyLength)
            {
                return false;
            }

            return IdempotencyKeyRegex.IsMatch(key);
        }

        private static string SanitizeForCacheKey(string input)
        {
            // Sanitize input to prevent cache key injection
            // Only allow alphanumeric characters, hyphens, and underscores
            return Regex.Replace(input, @"[^a-zA-Z0-9_-]", "_");
        }
    }

    /// <summary>
    /// Represents a cached response for idempotency purposes
    /// </summary>
    internal class IdempotencyResponse
    {
        public int StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
    }
}
