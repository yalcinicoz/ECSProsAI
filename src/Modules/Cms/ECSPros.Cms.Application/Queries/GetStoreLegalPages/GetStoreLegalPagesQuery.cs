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
/// </summary>
public record GetStoreLegalPagesQuery(
    Guid FirmPlatformId,
    List<string>? Codes = null) : IRequest<Result<List<StoreLegalPageDto>>>;

public record StoreLegalPageDto(
    string Code,
    string Title,
    string BodyHtml,
    DateTime? ContentUpdatedAt);

public class GetStoreLegalPagesQueryHandler(ICmsDbContext db)
    : IRequestHandler<GetStoreLegalPagesQuery, Result<List<StoreLegalPageDto>>>
{
    public async Task<Result<List<StoreLegalPageDto>>> Handle(GetStoreLegalPagesQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var sayfalar = await db.Pages
            .Where(p => p.FirmPlatformId == request.FirmPlatformId
                        && p.PageType == "legal"
                        && p.IsActive
                        && (p.PublishAt == null || p.PublishAt <= now)
                        && (p.UnpublishAt == null || p.UnpublishAt > now))
            .Select(p => new
            {
                p.Code,
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
        var liste = sayfalar
            .Where(p => request.Codes == null || request.Codes.Contains(p.Code))
            .Select(p => new StoreLegalPageDto(
                p.Code,
                p.NameI18n.TryGetValue("tr", out var ad) ? ad : p.NameI18n.Values.FirstOrDefault() ?? p.Code,
                string.Join("\n", p.Sections
                    .Select(s => s.Settings.TryGetValue("html", out var html) ? html?.ToString() : null)
                    .Where(h => !string.IsNullOrWhiteSpace(h))),
                p.Sections.Select(s => s.UpdatedAt)
                    .Concat(new DateTime?[] { p.UpdatedAt ?? p.CreatedAt }).Max()))
            .Where(p => p.BodyHtml.Length > 0)
            .ToList();

        return Result.Success(liste);
    }
}
