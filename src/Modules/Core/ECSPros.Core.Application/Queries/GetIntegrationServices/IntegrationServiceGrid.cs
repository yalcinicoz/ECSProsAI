using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetIntegrationServices;

/// <summary>
/// Entegrasyon servisleri (servis kataloğu) DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /core/integration-services</c> TAM liste + ayar şeması döner ve firma
/// entegrasyon formunun dropdown'ını besler; sayfalamak onu kırar. Liste ekranı sayfalı
/// <c>/core/integration-services/grid</c> kullanır — satırda ŞEMA JSON'u YOK, yalnız "şeması var" bayrağı.
///
/// <para>★ Bu tablo <c>definition</c> şemasındadır: yalnız geliştirici firma (platform yönetimi)
/// doldurur, veri aktarımları kayıt EKLEYEMEZ. Liste ekranı da salt görüntüleme + gösterim düzenlemesi.</para>
/// </summary>
public static class IntegrationServiceGrid
{
    public static readonly GridSchema<IntegrationService> Schema = new GridSchema<IntegrationService>()
        .Text("code", s => s.Code)
        .Text("name", s => GridJson.Text(s.NameI18n, "tr"))
        .Text("trackingUrlTemplate", s => s.TrackingUrlTemplate)
        .Enum("serviceType", s => s.ServiceType)
        .Enum("cargoCodeStrategy", s => s.CargoCodeStrategy)
        .Bool("isAvailable", s => s.IsAvailable)
        // ⚠ SettingsSchemaJson JSONB kolonudur (IntegrationServiceConfiguration): boş DİZGEYLE
        // karşılaştırmak PostgreSQL'de "invalid input syntax for type json" verir — yalnız null denetimi.
        .Bool("hasSchema", s => s.SettingsSchemaJson != null)
        .Bool("hasLogo", s => s.LogoUrl != null && s.LogoUrl != "")
        .Bool("kullanimda", s => s.PlatformIntegrations.Any(i => !i.IsDeleted))
        .Number("integrationCount", s => s.PlatformIntegrations.Count(i => !i.IsDeleted))
        .Date("createdAt", s => s.CreatedAt)
        .Sort("code", s => s.Code)
        .Sort("name", s => GridJson.Text(s.NameI18n, "tr"))
        .Sort("serviceType", s => s.ServiceType)
        .Sort("isAvailable", s => s.IsAvailable)
        .Sort("integrationCount", s => s.PlatformIntegrations.Count(i => !i.IsDeleted))
        .Sort("createdAt", s => s.CreatedAt)
        .DefaultSort(s => s.ServiceType, desc: false)
        .TieBreaker(s => s.Code);

    public static IQueryable<IntegrationService> ApplyNamed(IQueryable<IntegrationService> query, IntegrationServiceFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.ServiceType)) query = query.Where(s => s.ServiceType == f.ServiceType);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(s => s.Code.ToLower().Contains(t)
                || GridJson.Text(s.NameI18n, "tr")!.ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<IntegrationService> ApplyAll(IQueryable<IntegrationService> query, IntegrationServiceFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record IntegrationServiceFilters(string? ServiceType = null, string? Search = null);

public record IntegrationServiceGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, string ServiceType, bool IsAvailable,
    string? LogoUrl, string? TrackingUrlTemplate, bool HasSchema, int IntegrationCount,
    string? CargoCodeStrategy, DateTime CreatedAt);

public record GetIntegrationServicesGridQuery(IntegrationServiceFilters Filters, int Page = 1, int PageSize = 30, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<IntegrationServiceGridRow>>>;

public class GetIntegrationServicesGridQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetIntegrationServicesGridQuery, Result<PagedResult<IntegrationServiceGridRow>>>
{
    public async Task<Result<PagedResult<IntegrationServiceGridRow>>> Handle(GetIntegrationServicesGridQuery r, CancellationToken ct)
    {
        var q = IntegrationServiceGrid.ApplyAll(db.IntegrationServices.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await IntegrationServiceGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(s => new IntegrationServiceGridRow(
                s.Id, s.Code, s.NameI18n, s.ServiceType, s.IsAvailable, s.LogoUrl, s.TrackingUrlTemplate,
                s.SettingsSchemaJson != null,
                s.PlatformIntegrations.Count(i => !i.IsDeleted), s.CargoCodeStrategy, s.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<IntegrationServiceGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record IntegrationServiceExportRow(
    string Code, string Name, string ServiceType, bool IsAvailable, bool HasSchema,
    int IntegrationCount, string? CargoCodeStrategy, DateTime CreatedAt);

public record ExportIntegrationServicesQuery(IntegrationServiceFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<IntegrationServiceExportRow>>>;

public class ExportIntegrationServicesQueryHandler(ICoreDbContext db)
    : IRequestHandler<ExportIntegrationServicesQuery, Result<GridExportSource<IntegrationServiceExportRow>>>
{
    public async Task<Result<GridExportSource<IntegrationServiceExportRow>>> Handle(ExportIntegrationServicesQuery r, CancellationToken ct)
    {
        var q = IntegrationServiceGrid.ApplyAll(db.IntegrationServices.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<IntegrationServiceExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = IntegrationServiceGrid.Schema.ApplySort(q, r.Grid).Select(s => new IntegrationServiceExportRow(
            s.Code, GridJson.Text(s.NameI18n, "tr") ?? s.Code, s.ServiceType, s.IsAvailable,
            s.SettingsSchemaJson != null,
            s.PlatformIntegrations.Count(i => !i.IsDeleted), s.CargoCodeStrategy, s.CreatedAt));
        return Result.Success(new GridExportSource<IntegrationServiceExportRow>(count, rows));
    }
}
