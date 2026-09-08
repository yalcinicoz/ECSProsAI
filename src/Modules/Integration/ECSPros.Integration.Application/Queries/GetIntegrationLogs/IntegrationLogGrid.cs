using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Application.Queries.GetIntegrationLogs;

/// <summary>
/// Entegrasyon logları DataGrid şeması (tüm sütunlarda filtre, 2026-09-08): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (firmIntegrationId, serviceType, operationType, status, from, to — eski istemciler korunur) + global arama
/// (işlem / hata / servis / referans tipi). Servis ve durum listesi açık (DB'de legacy, dry_run gibi değerler var).
/// </summary>
public static class IntegrationLogGrid
{
    public static readonly GridSchema<IntegrationLog> Schema = new GridSchema<IntegrationLog>()
        .Date("createdAt", l => l.CreatedAt)
        .Enum("service", l => l.ServiceType)
        .Text("operation", l => l.OperationType)
        .Number("duration", l => l.DurationMs)
        .Enum("status", l => l.Status)
        .Text("error", l => l.ErrorMessage)
        .Bool("hasError", l => l.ErrorMessage != null && l.ErrorMessage != "")
        .Number("httpStatus", l => l.HttpStatusCode)
        .Text("referenceType", l => l.ReferenceType)
        .Guid("referenceId", l => l.ReferenceId)
        .Guid("integrationId", l => l.FirmIntegrationId)
        .Sort("createdAt", l => l.CreatedAt)
        .Sort("service", l => l.ServiceType)
        .Sort("operation", l => l.OperationType)
        .Sort("duration", l => l.DurationMs)
        .Sort("status", l => l.Status)
        .Sort("httpStatus", l => l.HttpStatusCode)
        .DefaultSort(l => l.CreatedAt, desc: true)
        .TieBreaker(l => l.Id);

    public static IQueryable<IntegrationLog> ApplyNamed(IQueryable<IntegrationLog> q, IntegrationLogListFilters f)
    {
        if (f.FirmIntegrationId.HasValue) q = q.Where(x => x.FirmIntegrationId == f.FirmIntegrationId.Value);
        if (f.FirmIntegrationIds is { Count: > 0 }) q = q.Where(x => f.FirmIntegrationIds.Contains(x.FirmIntegrationId));
        if (!string.IsNullOrWhiteSpace(f.ServiceType)) q = q.Where(x => x.ServiceType == f.ServiceType);
        if (!string.IsNullOrWhiteSpace(f.OperationType)) q = q.Where(x => x.OperationType == f.OperationType);
        if (!string.IsNullOrWhiteSpace(f.Status)) q = q.Where(x => x.Status == f.Status);
        if (f.From.HasValue) q = q.Where(x => x.CreatedAt >= f.From.Value);
        if (f.To.HasValue) q = q.Where(x => x.CreatedAt <= f.To.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            q = q.Where(x => x.OperationType.ToLower().Contains(term) || x.ServiceType.ToLower().Contains(term)
                || (x.ErrorMessage != null && x.ErrorMessage.ToLower().Contains(term))
                || (x.ReferenceType != null && x.ReferenceType.ToLower().Contains(term)));
        }
        return q;
    }

    public static IQueryable<IntegrationLog> ApplyAll(IQueryable<IntegrationLog> q, IntegrationLogListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(q, f), grid);

    public static string StatusLabel(string s) => s switch
    { "success" => "Başarılı", "failure" => "Hata", "error" => "Hata", "pending" => "Bekliyor", "dry_run" => "Deneme (dry-run)", _ => s };
}

public record IntegrationLogListFilters(
    Guid? FirmIntegrationId = null, string? ServiceType = null, string? OperationType = null, string? Status = null,
    DateTime? From = null, DateTime? To = null, List<Guid>? FirmIntegrationIds = null, string? Search = null);

public record IntegrationLogExportRow(
    DateTime CreatedAt, string ServiceType, string OperationType, string Status, int DurationMs, int? HttpStatusCode,
    string? ErrorMessage, string? ReferenceType, Guid? ReferenceId, Guid FirmIntegrationId);

public record ExportIntegrationLogsQuery(IntegrationLogListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<IntegrationLogExportRow>>>;

public class ExportIntegrationLogsQueryHandler(IIntegrationDbContext db) : IRequestHandler<ExportIntegrationLogsQuery, Result<GridExportSource<IntegrationLogExportRow>>>
{
    public async Task<Result<GridExportSource<IntegrationLogExportRow>>> Handle(ExportIntegrationLogsQuery r, CancellationToken ct)
    {
        var q = IntegrationLogGrid.ApplyAll(db.IntegrationLogs.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<IntegrationLogExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = IntegrationLogGrid.Schema.ApplySort(q, r.Grid).Select(x => new IntegrationLogExportRow(
            x.CreatedAt, x.ServiceType, x.OperationType, x.Status, x.DurationMs, x.HttpStatusCode, x.ErrorMessage, x.ReferenceType, x.ReferenceId, x.FirmIntegrationId));
        return Result.Success(new GridExportSource<IntegrationLogExportRow>(count, rows));
    }
}
