using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetSavedSearchesForAdmin;

/// <summary>
/// Kayıtlı aramalar DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (notifyEnabled/firmPlatformId) + global arama (sorgu metni, ad).
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — kayıtlı arama bir kanala aittir.</para>
/// </summary>
public static class SavedSearchGrid
{
    public static readonly GridSchema<SavedSearch> Schema = new GridSchema<SavedSearch>()
        .Kanal(s => s.FirmPlatformId)   // Y3 (K2)
        .Text("name", s => s.Name)
        .Text("query", s => s.Query)
        .Bool("notifyEnabled", s => s.NotifyEnabled)
        .Bool("notified", s => s.LastNotifiedAt != null)
        .Date("createdAt", s => s.CreatedAt)
        .Date("lastNotifiedAt", s => s.LastNotifiedAt)
        .Guid("firmPlatformId", s => s.FirmPlatformId)
        .Guid("memberId", s => s.MemberId)
        .Sort("name", s => s.Name)
        .Sort("query", s => s.Query)
        .Sort("notifyEnabled", s => s.NotifyEnabled)
        .Sort("createdAt", s => s.CreatedAt)
        .Sort("lastNotifiedAt", s => s.LastNotifiedAt)
        .DefaultSort(s => s.CreatedAt, desc: true)
        .TieBreaker(s => s.Id);

    public static IQueryable<SavedSearch> ApplyNamed(IQueryable<SavedSearch> query, SavedSearchFilters f)
    {
        if (f.NotifyEnabled.HasValue) query = query.Where(s => s.NotifyEnabled == f.NotifyEnabled.Value);
        if (f.FirmPlatformId.HasValue) query = query.Where(s => s.FirmPlatformId == f.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var aranan = f.Search.Trim().ToLower();
            query = query.Where(s => s.Query.ToLower().Contains(aranan)
                || (s.Name != null && s.Name.ToLower().Contains(aranan)));
        }
        return query;
    }

    public static IQueryable<SavedSearch> ApplyAll(
        IQueryable<SavedSearch> query, SavedSearchFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);
}

public record SavedSearchFilters(bool? NotifyEnabled = null, Guid? FirmPlatformId = null, string? Search = null);

public record SavedSearchExportRow(
    string? Name, string Query, bool NotifyEnabled, DateTime? LastNotifiedAt, DateTime CreatedAt);

public record ExportSavedSearchesQuery(SavedSearchFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<SavedSearchExportRow>>>;

public class ExportSavedSearchesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportSavedSearchesQuery, Result<GridExportSource<SavedSearchExportRow>>>
{
    public async Task<Result<GridExportSource<SavedSearchExportRow>>> Handle(ExportSavedSearchesQuery r, CancellationToken ct)
    {
        var q = SavedSearchGrid.ApplyAll(db.SavedSearches.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<SavedSearchExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = SavedSearchGrid.Schema.ApplySort(q, r.Grid).Select(s => new SavedSearchExportRow(
            s.Name, s.Query, s.NotifyEnabled, s.LastNotifiedAt, s.CreatedAt));
        return Result.Success(new GridExportSource<SavedSearchExportRow>(count, rows));
    }
}
