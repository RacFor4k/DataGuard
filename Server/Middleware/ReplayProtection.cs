using Microsoft.Extensions.Caching.Memory;

namespace Server.Middleware
{
    public class ReplayProtectionMiddleware
    {
        private readonly IMemoryCache _memoryCache;
        private readonly RequestDelegate _next;
        public ReplayProtectionMiddleware(IMemoryCache memoryCache, RequestDelegate next)
        {
            _memoryCache = memoryCache;
            _next = next;
        }
        public async Task Invoke(HttpContext context)
        {
            if(context.)
            if(context.Request.Headers.TryGetValue("Idempotency-Key", out var key))
            {
                var cacheKey = $"{userId}:{key}";
            }
        }
    }
}
