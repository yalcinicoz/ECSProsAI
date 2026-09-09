using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCollectionsForModeration;

/// <summary>
/// Koleksiyon moderasyonu DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + durum sekmesi +
/// global arama (ad, açıklama, paylaşım kodu).
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — kapsam dışı kanalın koleksiyonu listede/Excel'de görünmez.
/// (Eski uçta kapsam kontrolü YOKTU; DataGrid'e alırken eklendi.)</para>
/// </summary>
public static class CollectionModerationGrid
{
    public static readonly string[] Statuses = { "pending", "approved", "rejected" };

    public static readonly GridSchema<Collection> Schema = new GridSchema<Collection>()
        .Kanal(c => c.FirmPlatformId)   // Y3 (K2)
        .Text("name", c => c.Name)
        .Text("description", c => c.Description)
        .Text("shareCode", c => c.ShareCode)
        .Enum("status", c => c.Status, Statuses)
        .Bool("isPublic", c => c.IsPublic)
        .Bool("isShareable", c => c.IsShareable)
        .Bool("isQuickSave", c => c.IsQuickSave)
        .Bool("moderated", c => c.ModeratedAt != null)
        .Number("viewCount", c => c.ViewCount)
        .Number("itemCount", c => c.Items.Count(i => !i.IsDeleted))
        .Date("createdAt", c => c.CreatedAt)
        .Date("moderatedAt", c => c.ModeratedAt)
        .Guid("firmPlatformId", c => c.FirmPlatformId)
        .Guid("memberId", c => c.MemberId)
        .Sort("name", c => c.Name)
        .Sort("description", c => c.Description)
        .Sort("status", c => c.Status)
        .Sort("isPublic", c => c.IsPublic)
        .Sort("isQuickSave", c => c.IsQuickSave)
        .Sort("viewCount", c => c.ViewCount)
        .Sort("itemCount", c => c.Items.Count(i => !i.IsDeleted))
        .Sort("createdAt", c => c.CreatedAt)
        .Sort("moderatedAt", c => c.ModeratedAt)
        // Moderasyon kuyruğu: EN ESKİ önce (sıra bekleyen ilk sırada) — eski davranış korunur.
        .DefaultSort(c => c.CreatedAt, desc: false)
        .TieBreaker(c => c.Id);

    public static IQueryable<Collection> ApplyNamed(IQueryable<Collection> query, CollectionModerationFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(c => c.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(t)
                || (c.Description != null && c.Description.ToLower().Contains(t))
                || c.ShareCode.ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<Collection> ApplyAll(
        IQueryable<Collection> query, CollectionModerationFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);

    public static string StatusLabel(string s) => s switch
    {
        "pending" => "Onay Bekliyor", "approved" => "Onaylı", "rejected" => "Reddedildi", _ => s,
    };
}

public record CollectionModerationFilters(string? Status = null, string? Search = null);

public record CollectionModerationExportRow(
    string Name, string? Description, string Status, bool IsPublic, bool IsShareable, bool IsQuickSave,
    int ItemCount, int ViewCount, DateTime CreatedAt, DateTime? ModeratedAt);

public record ExportCollectionsForModerationQuery(CollectionModerationFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<CollectionModerationExportRow>>>;

public class ExportCollectionsForModerationQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportCollectionsForModerationQuery, Result<GridExportSource<CollectionModerationExportRow>>>
{
    public async Task<Result<GridExportSource<CollectionModerationExportRow>>> Handle(
        ExportCollectionsForModerationQuery r, CancellationToken ct)
    {
        var q = CollectionModerationGrid.ApplyAll(db.Collections.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<CollectionModerationExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = CollectionModerationGrid.Schema.ApplySort(q, r.Grid).Select(c => new CollectionModerationExportRow(
            c.Name, c.Description, c.Status, c.IsPublic, c.IsShareable, c.IsQuickSave,
            c.Items.Count(i => !i.IsDeleted), c.ViewCount, c.CreatedAt, c.ModeratedAt));
        return Result.Success(new GridExportSource<CollectionModerationExportRow>(count, rows));
    }
}
