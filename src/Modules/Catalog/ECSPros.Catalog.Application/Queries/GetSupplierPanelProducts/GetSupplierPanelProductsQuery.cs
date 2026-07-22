using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetSupplierPanelProducts;

/// <summary>
/// Satıcı paneli "Ürünlerim" — owner-scoped BİRLEŞİK liste: canlı ürünler (Product) +
/// gönderimler (ProductSubmission) SupplierProductCode üzerinden tek listede birleşir.
/// Durumlar: live (canlı) | pending (onay bekliyor) | rejected (reddedildi);
/// canlı ürünün bekleyen revizyonu PendingRevision bayrağıyla işaretlenir.
/// Başka tedarikçinin kaydı asla dönmez (WHERE SupplierId).
/// </summary>
public record GetSupplierPanelProductsQuery(
    Guid SupplierId, string? Status, string? Search, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<SupplierPanelProductRowDto>>>;

public record SupplierPanelProductRowDto(
    string SupplierProductCode,
    string? ProductCode,               // canlıysa iç katalog kodu
    Dictionary<string, string> Name,
    string GroupCode,
    Dictionary<string, string>? GroupName,
    int VariantCount,
    string Status,                     // live | pending | rejected
    bool PendingRevision,              // canlı üründe onay bekleyen revizyon var
    string? ReviewNote,                // son red/onay notu
    bool IsSaleOpen,                   // canlıysa satışa açık mı
    DateTime LastActivityAt);          // sıralama: son gönderim/güncelleme

public class GetSupplierPanelProductsQueryHandler
    : IRequestHandler<GetSupplierPanelProductsQuery, Result<PagedResult<SupplierPanelProductRowDto>>>
{
    private readonly ICatalogDbContext _db;
    public GetSupplierPanelProductsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<PagedResult<SupplierPanelProductRowDto>>> Handle(
        GetSupplierPanelProductsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        // İki kaynak da tedarikçiye dar projeksiyonla çekilir; birleştirme bellek içinde.
        // (Tek tedarikçinin ürün sayısı panel ölçeğindedir; admin kataloğu gibi 28K satır DEĞİL.)
        var products = await _db.Products
            .Where(p => p.SupplierId == request.SupplierId)
            .Select(p => new
            {
                p.Code,
                p.SupplierProductCode,
                p.NameI18n,
                GroupCode = p.ProductGroup.Code,
                GroupName = p.ProductGroup.NameI18n,
                VariantCount = p.Variants.Count(v => !v.IsDeleted),
                p.IsSaleOpen,
                LastAt = p.UpdatedAt ?? p.CreatedAt,
            })
            .ToListAsync(ct);

        var submissions = await _db.ProductSubmissions
            .Where(s => s.SupplierId == request.SupplierId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.SupplierProductCode,
                s.GroupCode,
                s.Name,
                s.VariantCount,
                s.Status,
                s.ReviewNote,
                s.CreatedAt,
                s.ReviewedAt,
            })
            .ToListAsync(ct);

        // Kod başına EN GÜNCEL gönderim (liste zaten CreatedAt desc)
        var latestByCode = submissions
            .GroupBy(s => s.SupplierProductCode)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<SupplierPanelProductRowDto>();

        // 1) Canlı ürünler — varsa bekleyen revizyon / son inceleme notu iliştirilir
        foreach (var p in products)
        {
            var key = p.SupplierProductCode ?? p.Code;
            latestByCode.TryGetValue(key, out var latest);
            var pendingRevision = latest?.Status == "pending";
            var reviewNote = latest?.Status is "rejected" or "approved" ? latest.ReviewNote : null;
            var lastAt = latest is not null && latest.CreatedAt > p.LastAt ? latest.CreatedAt : p.LastAt;
            rows.Add(new SupplierPanelProductRowDto(
                key, p.Code, p.NameI18n, p.GroupCode, p.GroupName, p.VariantCount,
                "live", pendingRevision, reviewNote, p.IsSaleOpen, lastAt));
        }

        // 2) Henüz canlı olmayan kodlar — son gönderimin durumu (pending/rejected)
        var liveCodes = rows.Select(r => r.SupplierProductCode).ToHashSet();
        foreach (var (code, s) in latestByCode)
        {
            if (liveCodes.Contains(code)) continue;
            if (s.Status == "approved") continue; // onaylı ama ürün silinmiş — canlı listede yeri yok
            rows.Add(new SupplierPanelProductRowDto(
                code, null, s.Name, s.GroupCode, null, s.VariantCount,
                s.Status, false, s.ReviewNote, false, s.CreatedAt));
        }

        // Filtre: durum
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            rows = request.Status switch
            {
                "live_pending" => rows.Where(r => r.Status == "live" && r.PendingRevision).ToList(),
                _ => rows.Where(r => r.Status == request.Status).ToList(),
            };
        }

        // Filtre: arama (kod + ad, kültür-duyarsız)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.SupplierProductCode.ToLowerInvariant().Contains(term)
                || (r.ProductCode?.ToLowerInvariant().Contains(term) ?? false)
                || r.Name.Values.Any(v => v.ToLowerInvariant().Contains(term))).ToList();
        }

        var total = rows.Count;
        var items = rows
            .OrderByDescending(r => r.LastActivityAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToList();

        return Result.Success(new PagedResult<SupplierPanelProductRowDto>(items, total, page, pageSize));
    }
}
