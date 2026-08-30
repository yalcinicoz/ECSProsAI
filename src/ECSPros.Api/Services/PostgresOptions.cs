namespace ECSPros.Api.Services;

public sealed class PostgresOptions
{
    public bool RequirePrimary { get; init; } = true;
    public int HostRecheckSeconds { get; init; } = 10;
    public bool LoadBalanceHosts { get; init; } = true;
    public int MinPoolSize { get; init; } = 5;
    public int MaxPoolSize { get; init; } = 200;
    public int ConnectionIdleLifetimeSeconds { get; init; } = 300;
    public int TimeoutSeconds { get; init; } = 5;
    public int CommandTimeoutSeconds { get; init; } = 30;

    public void Validate()
    {
        if (HostRecheckSeconds is < 1 or > 300)
            throw new InvalidOperationException("Postgres:HostRecheckSeconds 1-300 aralığında olmalıdır.");
        if (MinPoolSize < 0 || MaxPoolSize < 1 || MinPoolSize > MaxPoolSize)
            throw new InvalidOperationException("Postgres pool sınırları geçersizdir.");
        if (MaxPoolSize > 1000)
            throw new InvalidOperationException("Postgres:MaxPoolSize 1000 değerini aşamaz.");
        if (TimeoutSeconds is < 1 or > 60 || CommandTimeoutSeconds is < 1 or > 600)
            throw new InvalidOperationException("Postgres timeout sınırları geçersizdir.");
    }
}
