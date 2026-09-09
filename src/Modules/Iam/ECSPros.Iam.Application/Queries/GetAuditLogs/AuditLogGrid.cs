using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetAuditLogs;

/// <summary>
/// Denetim logları DataGrid şeması (2026-09-08): beyaz listeli filtre/sıralama + mevcut adlandırılmış parametreler (userId, entityType,
/// entityId, action, from, to) + global arama (entityType/entityId/action/ip contains). Liste ve export aynı modeli kullanır.
/// `action` açık kümedir (Created/Updated/…/grid_export) → Enum izinli liste YOK.
/// </summary>
public static class AuditLogGrid
{
    public static readonly GridSchema<AuditLog> Schema = new GridSchema<AuditLog>()
        .Enum("action", l => l.Action)
        .Text("entityType", l => l.EntityType)
        .Text("entityId", l => l.EntityId.ToString())
        .Text("ip", l => l.IpAddress)
        .Date("createdAt", l => l.CreatedAt)
        .Guid("userId", l => l.UserId)
        .Sort("entityId", l => l.EntityId.ToString())
        .Sort("createdAt", l => l.CreatedAt)
        .Sort("action", l => l.Action)
        .Sort("entityType", l => l.EntityType)
        .Sort("ip", l => l.IpAddress)
        .DefaultSort(l => l.CreatedAt, desc: true)
        .TieBreaker(l => l.Id);

    public static IQueryable<AuditLog> ApplyNamed(IQueryable<AuditLog> query, AuditLogListFilters f)
    {
        if (f.UserId.HasValue) query = query.Where(l => l.UserId == f.UserId);
        if (!string.IsNullOrEmpty(f.EntityType)) query = query.Where(l => l.EntityType == f.EntityType);
        if (f.EntityId.HasValue) query = query.Where(l => l.EntityId == f.EntityId);
        if (!string.IsNullOrEmpty(f.Action)) query = query.Where(l => l.Action == f.Action);
        if (f.From.HasValue) query = query.Where(l => l.CreatedAt >= f.From.Value);
        if (f.To.HasValue) query = query.Where(l => l.CreatedAt <= f.To.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            query = query.Where(l =>
                l.EntityType.ToLower().Contains(s) ||
                l.Action.ToLower().Contains(s) ||
                l.EntityId.ToString().Contains(s) ||
                (l.IpAddress != null && l.IpAddress.Contains(s)));
        }
        return query;
    }

    public static IQueryable<AuditLog> ApplyAll(IQueryable<AuditLog> query, AuditLogListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record AuditLogListFilters(
    Guid? UserId = null, string? EntityType = null, Guid? EntityId = null, string? Action = null,
    DateTime? From = null, DateTime? To = null, string? Search = null);

public record AuditLogExportRow(DateTime CreatedAt, string Action, string EntityType, Guid EntityId, string? UserName, Guid? UserId, string? IpAddress, string? UserAgent);

public record ExportAuditLogsQuery(AuditLogListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<AuditLogExportRow>>>;

public class ExportAuditLogsQueryHandler(IIamDbContext db) : IRequestHandler<ExportAuditLogsQuery, Result<GridExportSource<AuditLogExportRow>>>
{
    public async Task<Result<GridExportSource<AuditLogExportRow>>> Handle(ExportAuditLogsQuery r, CancellationToken ct)
    {
        var q = AuditLogGrid.ApplyAll(db.AuditLogs.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<AuditLogExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = AuditLogGrid.Schema.ApplySort(q, r.Grid).Select(l => new AuditLogExportRow(
            l.CreatedAt, l.Action, l.EntityType, l.EntityId,
            db.Users.Where(u => u.Id == l.UserId).Select(u => u.Username).FirstOrDefault(),
            l.UserId, l.IpAddress, l.UserAgent));
        return Result.Success(new GridExportSource<AuditLogExportRow>(count, rows));
    }
}
