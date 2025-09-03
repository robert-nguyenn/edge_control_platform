using HotChocolate;
using HotChocolate.Data;

namespace EdgeControlApi.GraphQL
{
    public class Mutation
    {
        [UseDbContext(typeof(EdgeControlContext))]
        public async Task<Flag> UpsertFlag(
            [ScopedService] EdgeControlContext context, 
            string key, 
            string description,
            int rolloutPercent,
            string? rules)
        {
            var flag = await context.Flags.FindAsync(key);
            var isNew = flag == null;
            
            if (isNew)
            {
                flag = new Flag { Key = key };
                context.Flags.Add(flag);
            }

            flag.Description = description;
            flag.RolloutPercent = rolloutPercent;
            flag.Rules = rules ?? "{}";
            flag.UpdatedAt = DateTime.UtcNow;

            // Add audit log
            var auditLog = new AuditLog
            {
                FlagKey = key,
                Action = isNew ? "create" : "update",
                Actor = "graphql",
                PayloadHash = GeneratePayloadHash(flag),
                CreatedAt = DateTime.UtcNow
            };
            context.AuditLogs.Add(auditLog);

            await context.SaveChangesAsync();
            return flag;
        }

        private string GeneratePayloadHash(Flag flag)
        {
            var content = $"{flag.Description}:{flag.RolloutPercent}:{flag.Rules}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(hash)[..16].ToLower();
        }
    }
}
