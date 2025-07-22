using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CPTHub.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheService> _logger;

        public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out var cachedValue))
                {
                    if (cachedValue is string jsonString)
                    {
                        return JsonConvert.DeserializeObject<T>(jsonString);
                    }
                    return cachedValue as T;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve cached item with key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var options = new MemoryCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiration.Value;
                }
                else
                {
                    options.SlidingExpiration = TimeSpan.FromHours(1);
                }

                // Set priority based on cache type
                options.Priority = CacheItemPriority.Normal;

                var serializedValue = JsonConvert.SerializeObject(value);
                _memoryCache.Set(key, serializedValue, options);
                
                _logger.LogDebug("Cached item with key: {Key}, expiration: {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache item with key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                _logger.LogDebug("Removed cached item with key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove cached item with key: {Key}", key);
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                if (_memoryCache is MemoryCache mc)
                {
                    mc.Compact(1.0);
                }
                _logger.LogInformation("Cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear cache");
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return _memoryCache.TryGetValue(key, out _);
        }
    }
}
