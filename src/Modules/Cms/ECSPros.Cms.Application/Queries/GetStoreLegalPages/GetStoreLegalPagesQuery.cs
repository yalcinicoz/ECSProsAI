using ECSPros.Cms.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Queries.GetStoreLegalPages;

/// <summary>
/// C8: müşteriye dönük hukuki/bilgilendirme sayfaları (mesafeli satış sözleşmesi,
/// ön bilgilendirme formu, gizlilik…) — PageType "legal", içerik sayfanın aktif
/// "rich_text" section'larının Settings["html"] alanlarından birleşir.
/// Codes verilirse yalnız o kodlar döner; verilmezse platformun tüm legal sayfaları.
/// F1: PageType parametreli — kurumsal içerik sayfaları "corporate" tipiyle aynı
/// mekanizmayı kullanır.
/// </summary>
/// <param name="PageType">legal | corporate | all (B7 2026-09-07: "all" tip filtresi uygulamaz).</param>
/// <param name="Codes">Sayfa kodu, site slug'ı ya da takma ad (B7: StoreSiteRoutes ile koda çözülür).</param>
public record GetStoreLegalPagesQuery(
    Guid FirmPlatformId,
    List<string>? Codes = null,
    string PageType = "legal") : IRequest<Result<List<StoreLegalPageDto>>>;

public record StoreLegalPageDto(
    string Code,
    string Title,
    string BodyHtml,
    DateTime? ContentUpdatedAt,
    string? Slug = null,          // B7: sitedeki URL yolu (baştaki / olmadan) — kurumsal sayfalarda site linkiyle birebir
    string? PageType = null,      // legal | corporate
    string[]? Aliases = null);    // B7: bu sayfayı çözen diğer anahtarlar

public class GetStoreLegalPagesQueryHandler(ICmsDbContext db)
    : IRequestHandler<GetStoreLegalPagesQuery, Result<List<StoreLegalPageDto>>>
{
    public async Task<Result<List<StoreLegalPageDto>>> Handle(GetStoreLegalPagesQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var tumTipler = string.Equals(request.PageType, "all", StringComparison.OrdinalIgnoreCase);
        var sayfalar = await db.Pages
            .Where(p => p.FirmPlatformId == request.FirmPlatformId
                        && (tumTipler || p.PageType == request.PageType)
                        && p.IsActive
                        && (p.PublishAt == null || p.PublishAt <= now)
                        && (p.UnpublishAt == null || p.UnpublishAt > now))
            .Select(p => new
            {
                p.Code,
                p.PageType,
                p.NameI18n,
                p.UpdatedAt,
                p.CreatedAt,
                Sections = p.Sections
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new { s.Settings, s.UpdatedAt })
                    .ToList()
            })
            .ToListAsync(ct);

        // Settings jsonb sözlüğü SQL'e çevrilemez — html alanı bellek tarafında okunur
        // (platform başına bir avuç sayfa; SepetController 5 dk IMemoryCache'ler).
        // B7: istenen anahtarlar (kod / site slug'ı / takma ad) → kod
        var bilinenKodlar = sayfalar.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var istenenKodlar = request.Codes?
            .Select(c => ECSPros.Cms.Application.Helpers.StoreSiteRoutes.Resolve(c, bilinenKodlar).Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var liste = sayfalar
            .Where(p => istenenKodlar == null || istenenKodlar.Contains(p.Code))
            .Select(p => new StoreLegalPageDto(
                p.Code,
                p.NameI18n.TryGetValue("tr", out var ad) ? ad : p.NameI18n.Values.FirstOrDefault() ?? p.Code,
                string.Join("\n", p.Sections
                    .Select(s => s.Settings.TryGetValue("html", out var html) ? html?.ToString() : null)
                    .Where(h => !string.IsNullOrWhiteSpace(h))),
                p.Sections.Select(s => s.UpdatedAt)
                    .Concat(new DateTime?[] { p.UpdatedAt ?? p.CreatedAt }).Max(),
                ECSPros.Cms.Application.Helpers.StoreSiteRoutes.SlugFor(p.Code),
                p.PageType,
                ECSPros.Cms.Application.Helpers.StoreSiteRoutes.AliasesFor(p.Code) is { Length: > 0 } al ? al : null))
            .Where(p => p.BodyHtml.Length > 0)
            .ToList();

        return Result.Success(liste);
    }
}
