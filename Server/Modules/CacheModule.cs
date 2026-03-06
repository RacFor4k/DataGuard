using Microsoft.Extensions.Caching.Memory;

namespace Server.Modules
{
    public interface AuthNonceCacheI
    {
        public bool TryWrite(string key, string value);
        public bool TryRead(string key, out string? value);
        public bool Equals(string key);
        public void Remove(string key);
    }

    public class AuthNonceCache : AuthNonceCacheI
    {
        public readonly IMemoryCache _cache;
        public readonly MemoryCacheEntryOptions _cacheOptions;
        public AuthNonceCache(IMemoryCache cache) 
        {
            _cache = cache;
            _cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
        }
        public bool TryWrite(string key, string value)
        {
            try
            {
                _cache.Set(key, value, _cacheOptions);
            }
            catch
            {
                return false;
            }
                return true;
        }
        public bool TryRead(string key, out string? value)
        {
            return _cache.TryGetValue(key, out value);
        }
        public bool Equals(string key)
        {
            return _cache.Equals(key);
        }
        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}
