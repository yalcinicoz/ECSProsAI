namespace ECSPros.Api.Services.AiReporting;

/// <summary>Shared per-process capacity, not a distributed lock. New sources cannot multiply DB concurrency.</summary>
internal static class ReportExecutionBudget
{
    internal static readonly SemaphoreSlim Slots = new(2, 2);
}
