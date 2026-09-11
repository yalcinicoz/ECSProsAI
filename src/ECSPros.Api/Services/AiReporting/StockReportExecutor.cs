using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Npgsql;

namespace ECSPros.Api.Services.AiReporting;

public sealed record StockReportResult(IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows, DateTimeOffset CalculatedAtUtc,
    long? TotalCount = null, IReadOnlyDictionary<string, decimal>? Totals = null,
    IReadOnlyList<ReportCurrencyTotal>? CurrencyTotals = null)
{
    public OrderReportScope? ResolvedPeriod { get; init; }
}

/// <summary>userId must come from authenticated server identity; HTTP rollout is disabled by default.</summary>
public sealed class StockReportExecutor(NpgsqlDataSource source, IEtkinYetkiServisi authorization,
    IReportAttributeCatalog attributeCatalog)
{
    // Per-process protection; total capacity scales with nodes, not a global distributed quota.
    private static readonly SemaphoreSlim Slots = ReportExecutionBudget.Slots;

    public async Task<StockReportResult> ExecuteAsync(Guid userId, string json, CancellationToken ct, ReportGridState? grid = null)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        ct = deadline.Token;
        var effective = await authorization.GetirAsync(userId, ct);
        var permissions = ResolvePermissions(effective);
        var attributes = await attributeCatalog.LoadAsync(permissions, ct);
        // Reparse now, including saved recipes; never trust a previously validated client object.
        var validation = ReportDefinitionValidator.Parse(json, permissions, attributes: attributes);
        if (!validation.IsValid) throw new ArgumentException(validation.Error, nameof(json));
        if (!await Slots.WaitAsync(0, ct)) throw new InvalidOperationException("Rapor sistemi meşgul; tekrar deneyin.");
        try
        {
            await using var connection = await source.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using (var guard = new NpgsqlCommand(
                "SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15s';", connection, transaction))
                await guard.ExecuteNonQueryAsync(ct);
            await using var command = grid is null ? StockReportQuery.Create(json, permissions, attributes)
                : ReportGridQuery.Create(json, permissions, attributes, grid);
            command.Connection = connection;
            command.Transaction = transaction;
            var recipe = validation.Definition!;
            var rows = new List<object?[]>();
            long? totalCount = null;
            var totals = new Dictionary<string, decimal>();
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var offset = 0;
                    if (grid is not null)
                    {
                        totalCount = reader.GetInt64(0);
                        if (grid.ExportMode) ReportExcelExport.CheckCount(totalCount.Value);
                        for (var metric = 0; metric < recipe.Metrics!.Length; metric++)
                            totals[recipe.Metrics[metric]] = reader.GetFieldValue<decimal>(metric + 1);
                        offset = recipe.Metrics.Length + 2;
                        if (reader.IsDBNull(offset - 1)) continue; // Empty page still returns count/totals.
                    }
                    if (rows.Count == (grid?.PageSize ?? recipe.Limit))
                        throw new InvalidOperationException("Sonuç sınırı aşıldı; filtreyi daraltın. Kısmi rapor gösterilmedi.");
                    var row = new object?[reader.FieldCount - offset];
                    for (var i = 0; i < row.Length; i++)
                        row[i] = await reader.IsDBNullAsync(i + offset, ct) ? null : reader.GetValue(i + offset);
                    rows.Add(row);
                }
            }
            await transaction.CommitAsync(ct);
            return new(recipe.Dimensions!.Concat(recipe.Metrics!).ToArray(), rows, DateTimeOffset.UtcNow,
                totalCount, grid is null ? null : totals);
        }
        finally { Slots.Release(); }
    }

    public static IReadOnlySet<string> ResolvePermissions(EfektifYetkiler effective)
        => ReportSourceCatalog.ResolvePermissions(effective);
}
