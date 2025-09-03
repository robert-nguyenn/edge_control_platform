using Grpc.Net.Client;
using EdgeControlApi.RateLimiting;

namespace EdgeControlApi.Services
{
    public class RateLimiterService
    {
        private readonly RateLimiter.RateLimiterClient _client;
        private readonly ILogger<RateLimiterService> _logger;

        public RateLimiterService(IConfiguration configuration, ILogger<RateLimiterService> logger)
        {
            _logger = logger;
            string? rateLimiterUri = configuration.GetValue<string>("Services:RateLimiter:Uri");
            
            if (string.IsNullOrEmpty(rateLimiterUri))
            {
                rateLimiterUri = "http://rate-limiter:50051";
                _logger.LogWarning("Rate limiter URI not found in configuration. Using default: {DefaultUri}", rateLimiterUri);
            }

            try
            {
                var channel = GrpcChannel.ForAddress(rateLimiterUri);
                _client = new RateLimiter.RateLimiterClient(channel);
                _logger.LogInformation("Connected to rate limiter at {RateLimiterUri}", rateLimiterUri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create gRPC channel to rate limiter at {RateLimiterUri}", rateLimiterUri);
                throw;
            }
        }

        public async Task<(bool Allowed, TimeSpan? RetryAfter, double QuotaRemaining)> AllowRequestAsync(
            string key, 
            uint tokenCost = 1, 
            string? clientId = null)
        {
            try
            {
                var request = new AllowRequest
                {
                    Key = key,
                    TokenCost = tokenCost,
                    ClientId = clientId ?? "unknown"
                };

                var response = await _client.AllowAsync(request);
                
                TimeSpan? retryAfter = null;
                if (response.RetryAfterMs > 0)
                {
                    retryAfter = TimeSpan.FromMilliseconds(response.RetryAfterMs);
                }

                return (response.Allowed, retryAfter, response.QuotaRemaining);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rate limiter check failed for key {Key}", key);
                // Default to allowing the request if the rate limiter is unavailable
                return (true, null, 0);
            }
        }

        public async Task<bool> ConfigureRateLimiterAsync(string key, double refillRate, double bucketCapacity)
        {
            try
            {
                var request = new ConfigureRequest
                {
                    Key = key,
                    RefillRate = refillRate,
                    BucketCapacity = bucketCapacity
                };

                var response = await _client.ConfigureAsync(request);
                if (!response.Success)
                {
                    _logger.LogWarning("Failed to configure rate limiter for key {Key}: {Message}", key, response.Message);
                }
                return response.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure rate limiter for key {Key}", key);
                return false;
            }
        }

        public async Task<RateLimiterStatus?> GetStatusAsync(string key)
        {
            try
            {
                var request = new StatusRequest { Key = key };
                var response = await _client.StatusAsync(request);

                return new RateLimiterStatus
                {
                    Key = response.Key,
                    TokensRemaining = response.TokensRemaining,
                    RefillRate = response.RefillRate,
                    BucketCapacity = response.BucketCapacity,
                    LastRefillTime = DateTimeOffset.FromUnixTimeMilliseconds(response.LastRefillTimeMs)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get rate limiter status for key {Key}", key);
                return null;
            }
        }
    }

    public class RateLimiterStatus
    {
        public string Key { get; set; } = "";
        public double TokensRemaining { get; set; }
        public double RefillRate { get; set; }
        public double BucketCapacity { get; set; }
        public DateTimeOffset LastRefillTime { get; set; }
    }
}
