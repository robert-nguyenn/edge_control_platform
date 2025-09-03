using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EdgeControlApi.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure Entity Framework
builder.Services.AddPooledDbContextFactory<EdgeControlContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<EdgeControlContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Redis Cache Service
builder.Services.AddSingleton<RedisCacheService>();

// Configure Rate Limiter Service
builder.Services.AddSingleton<RateLimiterService>();

// Configure GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType<EdgeControlApi.GraphQL.Query>()
    .AddMutationType<EdgeControlApi.GraphQL.Mutation>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .RegisterDbContext<EdgeControlContext>(DbContextKind.Pooled);

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter("EdgeControlApi")
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("EdgeControlApi"));
    });

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("ETag");
    });
});

var app = builder.Build();

app.UseCors();

// Add prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EdgeControlContext>();
    await context.Database.EnsureCreatedAsync();
    await SeedData(context);
}

// Health check endpoint
app.MapGet("/healthz", () => new { status = "ok", version = "1.0.0" });

// Map GraphQL endpoint
app.MapGraphQL();

// Get single flag
app.MapGet("/flags/{key}", async (string key, EdgeControlContext context, HttpContext httpContext, RedisCacheService cache, RateLimiterService rateLimiter) =>
{
    // Apply rate limiting
    var (allowed, retryAfter, _) = await rateLimiter.AllowRequestAsync(
        key: $"flag_read:{key}",
        tokenCost: 1,
        clientId: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
    );
    
    if (!allowed)
    {
        if (retryAfter.HasValue)
        {
            httpContext.Response.Headers.Add("Retry-After", ((int)retryAfter.Value.TotalSeconds).ToString());
        }
        return Results.StatusCode(429); // Too Many Requests
    }
    
    // Try to get from cache first
    var cachedFlag = await cache.GetAsync<Flag>($"flag:{key}");
    Flag? flag = cachedFlag;
    
    // If not in cache, get from database
    if (flag == null)
    {
        flag = await context.Flags.FirstOrDefaultAsync(f => f.Key == key);
        if (flag == null)
            return Results.NotFound();
            
        // Store in cache
        await cache.SetAsync($"flag:{key}", flag, TimeSpan.FromMinutes(5));
    }

    var etag = GenerateETag(flag);
    
    // Check If-None-Match header
    if (httpContext.Request.Headers.ContainsKey("If-None-Match"))
    {
        var clientETag = httpContext.Request.Headers["If-None-Match"].ToString().Trim('"');
        if (clientETag == etag)
        {
            return Results.StatusCode(304);
        }
    }

    httpContext.Response.Headers.Add("ETag", $"\"{etag}\"");
    httpContext.Response.Headers.Add("Cache-Control", "max-age=5, stale-while-revalidate=30");

    // Handle shadow evaluation
    if (httpContext.Request.Headers.ContainsKey("X-Proposed-Rules"))
    {
        await HandleShadowEvaluation(key, httpContext.Request.Headers["X-Proposed-Rules"], flag, context);
    }

    return Results.Ok(new
    {
        key = flag.Key,
        description = flag.Description,
        rolloutPercent = flag.RolloutPercent,
        rules = string.IsNullOrEmpty(flag.Rules) ? new object() : JsonSerializer.Deserialize<object>(flag.Rules)
    });
});

// Update flag
app.MapPut("/flags/{key}", async (string key, FlagUpdateRequest request, EdgeControlContext context, HttpContext httpContext, RedisCacheService cache, RateLimiterService rateLimiter) =>
{
    // Apply rate limiting with higher token cost for writes
    var (allowed, retryAfter, _) = await rateLimiter.AllowRequestAsync(
        key: "flag_write",
        tokenCost: 5, // Higher cost for writes
        clientId: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
    );
    
    if (!allowed)
    {
        if (retryAfter.HasValue)
        {
            httpContext.Response.Headers.Add("Retry-After", ((int)retryAfter.Value.TotalSeconds).ToString());
        }
        return Results.StatusCode(429); // Too Many Requests
    }

    var flag = await context.Flags.FirstOrDefaultAsync(f => f.Key == key);
    var isNew = flag == null;
    
    if (isNew)
    {
        flag = new Flag { Key = key };
        context.Flags.Add(flag);
    }

    flag.Description = request.Description ?? flag.Description;
    flag.RolloutPercent = request.RolloutPercent ?? flag.RolloutPercent;
    flag.Rules = request.Rules != null ? JsonSerializer.Serialize(request.Rules) : flag.Rules;
    flag.UpdatedAt = DateTime.UtcNow;

    // Add audit log
    var auditLog = new AuditLog
    {
        FlagKey = key,
        Action = isNew ? "create" : "update",
        Actor = "admin",
        PayloadHash = GeneratePayloadHash(flag),
        CreatedAt = DateTime.UtcNow
    };
    context.AuditLogs.Add(auditLog);

    await context.SaveChangesAsync();
    
    // Update cache
    await cache.SetAsync($"flag:{key}", flag, TimeSpan.FromMinutes(5));
    
    // Invalidate cache for all flags list
    await cache.RemoveAsync("all_flags");

    var etag = GenerateETag(flag);
    httpContext.Response.Headers.Add("ETag", $"\"{etag}\"");

    return Results.Ok(new
    {
        key = flag.Key,
        description = flag.Description,
        rolloutPercent = flag.RolloutPercent,
        rules = string.IsNullOrEmpty(flag.Rules) ? new object() : JsonSerializer.Deserialize<object>(flag.Rules)
    });
});

// List all flags
app.MapGet("/flags", async (EdgeControlContext context, RedisCacheService cache, RateLimiterService rateLimiter, HttpContext httpContext) =>
{
    // Apply rate limiting
    var (allowed, retryAfter, _) = await rateLimiter.AllowRequestAsync(
        key: "flags_list",
        tokenCost: 2, // Medium cost for listing all flags
        clientId: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
    );
    
    if (!allowed)
    {
        if (retryAfter.HasValue)
        {
            httpContext.Response.Headers.Add("Retry-After", ((int)retryAfter.Value.TotalSeconds).ToString());
        }
        return Results.StatusCode(429); // Too Many Requests
    }

    // Try to get from cache first
    var cachedFlags = await cache.GetAsync<List<object>>("all_flags");
    if (cachedFlags != null)
    {
        return Results.Ok(cachedFlags);
    }

    // If not in cache, get from database
    var flags = await context.Flags
        .Select(f => new
        {
            key = f.Key,
            description = f.Description,
            rolloutPercent = f.RolloutPercent,
            updatedAt = f.UpdatedAt
        })
        .ToListAsync();
        
    // Store in cache
    await cache.SetAsync("all_flags", flags, TimeSpan.FromMinutes(1));

    return Results.Ok(flags);
});

// Get audit logs
app.MapGet("/audit", async (string? flag, int limit = 10, EdgeControlContext context) =>
{
    var query = context.AuditLogs.AsQueryable();
    
    if (!string.IsNullOrEmpty(flag))
        query = query.Where(a => a.FlagKey == flag);

    var logs = await query
        .OrderByDescending(a => a.CreatedAt)
        .Take(limit)
        .Select(a => new
        {
            id = a.Id,
            flagKey = a.FlagKey,
            action = a.Action,
            actor = a.Actor,
            payloadHash = a.PayloadHash,
            createdAt = a.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(logs);
});

app.Run();

// Helper methods
static string GenerateETag(Flag flag)
{
    var content = $"{flag.Key}:{flag.UpdatedAt:O}:{flag.RolloutPercent}";
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
    return Convert.ToHexString(hash)[..16].ToLower();
}

static string GeneratePayloadHash(Flag flag)
{
    var content = $"{flag.Description}:{flag.RolloutPercent}:{flag.Rules}";
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
    return Convert.ToHexString(hash)[..16].ToLower();
}

static async Task HandleShadowEvaluation(string flagKey, string proposedRulesHeader, Flag currentFlag, EdgeControlContext context)
{
    try
    {
        var proposedRulesJson = Encoding.UTF8.GetString(Convert.FromBase64String(proposedRulesHeader));
        var proposedRules = JsonSerializer.Deserialize<object>(proposedRulesJson);
        
        // Simple comparison - in a real system you'd evaluate with test data
        var currentRulesJson = currentFlag.Rules ?? "{}";
        if (currentRulesJson != proposedRulesJson)
        {
            var auditLog = new AuditLog
            {
                FlagKey = flagKey,
                Action = "shadow_mismatch",
                Actor = "system",
                PayloadHash = GeneratePayloadHash(currentFlag),
                CreatedAt = DateTime.UtcNow
            };
            context.AuditLogs.Add(auditLog);
            await context.SaveChangesAsync();
        }
    }
    catch
    {
        // Ignore invalid shadow evaluation requests
    }
}

static async Task SeedData(EdgeControlContext context)
{
    if (!await context.Flags.AnyAsync())
    {
        var flags = new[]
        {
            new Flag
            {
                Key = "pricing.v2",
                Description = "Enable new pricing model v2",
                RolloutPercent = 0,
                Rules = "{}",
                UpdatedAt = DateTime.UtcNow
            },
            new Flag
            {
                Key = "exp.freeShipping",
                Description = "Experiment: Free shipping promotion",
                RolloutPercent = 0,
                Rules = "{}",
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Flags.AddRange(flags);
        await context.SaveChangesAsync();
    }
}

// Models
public class Flag
{
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
    public int RolloutPercent { get; set; }
    public string Rules { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string FlagKey { get; set; } = "";
    public string Action { get; set; } = "";
    public string Actor { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class FlagUpdateRequest
{
    public string? Description { get; set; }
    public int? RolloutPercent { get; set; }
    public object? Rules { get; set; }
}

public class EdgeControlContext : DbContext
{
    public EdgeControlContext(DbContextOptions<EdgeControlContext> options) : base(options) { }

    public DbSet<Flag> Flags { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flag>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Rules).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.FlagKey).HasMaxLength(255);
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Actor).HasMaxLength(50);
            entity.Property(e => e.PayloadHash).HasMaxLength(50);
        });
    }
}
