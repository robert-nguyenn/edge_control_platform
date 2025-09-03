using HotChocolate;
using HotChocolate.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeControlApi.GraphQL
{
    public class Query
    {
        [UseDbContext(typeof(EdgeControlContext))]
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Flag> GetFlags([ScopedService] EdgeControlContext context)
        {
            return context.Flags.OrderBy(f => f.Key);
        }

        [UseDbContext(typeof(EdgeControlContext))]
        public async Task<Flag?> GetFlag([ScopedService] EdgeControlContext context, string key)
        {
            return await context.Flags.FirstOrDefaultAsync(f => f.Key == key);
        }

        [UseDbContext(typeof(EdgeControlContext))]
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public IQueryable<AuditLog> GetAuditLogs([ScopedService] EdgeControlContext context)
        {
            return context.AuditLogs.OrderByDescending(a => a.CreatedAt);
        }

        public async Task<FlagEvaluationResult> EvaluateFlag([Service] EdgeControlContext context, string key, string userId)
        {
            var flag = await context.Flags.FirstOrDefaultAsync(f => f.Key == key);
            if (flag == null)
            {
                return new FlagEvaluationResult
                {
                    Key = key,
                    IsEnabled = false,
                    Reason = "FLAG_NOT_FOUND"
                };
            }

            // Simple evaluation logic (matches the SDKs)
            var userIdHash = HashUserId(userId);
            var isEnabled = userIdHash < flag.RolloutPercent;

            return new FlagEvaluationResult
            {
                Key = key,
                IsEnabled = isEnabled,
                RolloutPercent = flag.RolloutPercent,
                Reason = isEnabled ? "ROLLOUT_MATCH" : "ROLLOUT_NO_MATCH"
            };
        }

        private int HashUserId(string userId)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userId));
            var intHash = BitConverter.ToInt32(hash, 0);
            return Math.Abs(intHash % 100);
        }
    }

    public class FlagEvaluationResult
    {
        public string Key { get; set; } = "";
        public bool IsEnabled { get; set; }
        public int RolloutPercent { get; set; }
        public string Reason { get; set; } = "";
    }
}
