using StackExchange.Redis;
using System.Text.Json;

namespace EdgeControlApi.Services
{
    public class RedisCacheService : IDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(5);
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(IConfiguration configuration, ILogger<RedisCacheService> logger)
        {
            _logger = logger;
            string? redisConnectionString = configuration.GetConnectionString("Redis");
            
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                _logger.LogWarning("Redis connection string not found. Using localhost:6379");
                redisConnectionString = "localhost:6379";
            }

            try
            {
                _redis = ConnectionMultiplexer.Connect(redisConnectionString);
                _database = _redis.GetDatabase();
                _logger.LogInformation("Connected to Redis at {RedisConnectionString}", redisConnectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis at {RedisConnectionString}", redisConnectionString);
                throw;
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                if (value.IsNull)
                {
                    return default;
                }

                return JsonSerializer.Deserialize<T>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value from Redis for key {Key}", key);
                return default;
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                return await _database.StringSetAsync(key, serializedValue, expiry ?? _defaultExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value in Redis for key {Key}", key);
                return false;
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key {Key} from Redis", key);
                return false;
            }
        }

        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key {Key} exists in Redis", key);
                return false;
            }
        }

        public async Task ClearFlagCacheAsync(string flagKey)
        {
            try
            {
                await _database.KeyDeleteAsync($"flag:{flagKey}");
                await _database.KeyDeleteAsync("all_flags");
                _logger.LogInformation("Cleared Redis cache for flag {FlagKey}", flagKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear Redis cache for flag {FlagKey}", flagKey);
            }
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}
