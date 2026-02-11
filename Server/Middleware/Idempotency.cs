using Microsoft.Extensions.Caching.Memory;

namespace Server.Middleware
{
    public class IdempotencyMiddleware
    {
        private readonly IMemoryCache _memoryCache;
        private readonly RequestDelegate _next;
        public IdempotencyMiddleware(IMemoryCache memoryCache, RequestDelegate next)
        {
            _memoryCache = memoryCache;
            _next = next;
        }
        public async Task Invoke(HttpContext context)
        {
            if(context.Request.Headers.TryGetValue("User-uuid", out var user\Uuid))
            if(context.Request.Headers.TryGetValue("Idempotency-Key", out var key))
            {
                var cacheKey = $"{userId}:{key}";
            }
        }
    }
}
