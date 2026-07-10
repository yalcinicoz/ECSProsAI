using ECSPros.Cms.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Queries.GetStoreFaq;

/// <summary>
/// F2: SSS soru/cevap listesi — "kurumsal-sss" sayfasının (PageType corporate) aktif
/// "faq" section'ının aktif item'ları sıralı döner (TitleI18n=soru, DescriptionI18n=cevap;
/// admin CMS'ten soru ekleyip/düzenleyebilir).
/// </summary>
public record GetStoreFaqQuery(Guid FirmPlatformId) : IRequest<Result<List<StoreFaqItemDto>>>;

public record StoreFaqItemDto(string Soru, string Cevap);

public class GetStoreFaqQueryHandler(ICmsDbContext db)
    : IRequestHandler<GetStoreFaqQuery, Result<List<StoreFaqItemDto>>>
{
    public async Task<Result<List<StoreFaqItemDto>>> Handle(GetStoreFaqQuery request, CancellationToken ct)
    {
        var itemlar = await db.Pages
            .Where(p => p.FirmPlatformId == request.FirmPlatformId
                        && p.PageType == "corporate"
                        && p.Code == "kurumsal-sss"
                        && p.IsActive)
            .SelectMany(p => p.Sections.Where(s => s.IsActive))
            .SelectMany(s => s.Items.Where(i => i.IsActive))
            .OrderBy(i => i.SortOrder)
            .Select(i => new { i.TitleI18n, i.DescriptionI18n })
            .ToListAsync(ct);

        var liste = itemlar
            .Select(i => new StoreFaqItemDto(
                i.TitleI18n != null && i.TitleI18n.TryGetValue("tr", out var soru) ? soru : "",
                i.DescriptionI18n != null && i.DescriptionI18n.TryGetValue("tr", out var cevap) ? cevap : ""))
            .Where(i => i.Soru.Length > 0 && i.Cevap.Length > 0)
            .ToList();

        return Result.Success(liste);
    }
}
