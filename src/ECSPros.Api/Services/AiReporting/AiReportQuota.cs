using StackExchange.Redis;

namespace ECSPros.Api.Services.AiReporting;

public sealed record AiQuotaDecision(bool Allowed, int RetryAfterSeconds = 0);

public interface IAiReportQuotaStore
{
    Task<AiQuotaDecision> ReserveAsync(string scope, Guid userId, Guid firmId, int perMinute,
        int userDaily, int firmDaily, CancellationToken ct);
}

/// <summary>All nodes must use the same state Redis and scope. No memory fallback or refunds.</summary>
public sealed class RedisAiReportQuotaStore(IServiceProvider services) : IAiReportQuotaStore
{
    // A single cluster hash slot makes all three limits atomic. Windows start on the first reservation.
    public const string Script = """
        local counts = {}
        local retry = 0
        for i = 1, 3 do
          local raw = redis.call('GET', KEYS[i])
          local value = tonumber(raw or '0')
          if not value or value < 0 or value ~= math.floor(value) then return -1 end
          counts[i] = value
          if raw then
            local ttl = redis.call('PTTL', KEYS[i])
            if ttl <= 0 then return -1 end
            if value >= tonumber(ARGV[i]) then retry = math.max(retry, ttl) end
          end
        end
        if retry > 0 then return math.ceil(retry / 1000) end
        for i = 1, 3 do
          redis.call('INCR', KEYS[i])
          if counts[i] == 0 then redis.call('PEXPIRE', KEYS[i], ARGV[i + 3]) end
        end
        return 0
        """;

    public async Task<AiQuotaDecision> ReserveAsync(string scope, Guid userId, Guid firmId,
        int perMinute, int userDaily, int firmDaily, CancellationToken ct)
    {
        var redis = services.GetService<IConnectionMultiplexer>()
            ?? throw new InvalidOperationException("AI quota state is unavailable.");
        var prefix = $"ECSPros:{{ai-report-{scope}}}:";
        var value = (long)await redis.GetDatabase().ScriptEvaluateAsync(Script,
            new RedisKey[] { $"{prefix}user:{userId:N}:minute", $"{prefix}user:{userId:N}:day", $"{prefix}firm:{firmId:N}:day" },
            new RedisValue[] { perMinute, userDaily, firmDaily, 60_000, 86_400_000, 86_400_000 })
            .WaitAsync(TimeSpan.FromSeconds(3), ct);
        if (value < 0 || value > 86400) throw new InvalidOperationException("AI quota state is invalid.");
        return new(value == 0, (int)value);
    }
}

public sealed class AiReportQuota(IConfiguration configuration, IAiReportQuotaStore store)
{
    public async Task<AiQuotaDecision> ReserveAsync(Guid userId, Guid firmId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var scope = configuration["AiReporting:Quota:Scope"];
        static bool Limit(string? value, out int limit) => int.TryParse(value, out limit) && limit is > 0 and <= 1_000_000;
        if (userId == Guid.Empty || firmId == Guid.Empty || string.IsNullOrEmpty(scope) || scope.Length > 64
            || scope.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
            || !Limit(configuration["AiReporting:Quota:UserPerMinute"], out var minute)
            || !Limit(configuration["AiReporting:Quota:UserPer24Hours"], out var userDay)
            || !Limit(configuration["AiReporting:Quota:FirmPer24Hours"], out var firmDay))
            throw new InvalidOperationException("AI quota configuration is required.");
        return await store.ReserveAsync(scope, userId, firmId, minute, userDay, firmDay, ct);
    }
}
