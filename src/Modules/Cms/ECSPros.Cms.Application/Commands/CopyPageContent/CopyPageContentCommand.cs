using ECSPros.Cms.Application.Services;
using ECSPros.Cms.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Commands.CopyPageContent;

// P2b (K20): kaynak sayfanın bölüm içerikleri, AYNI Code'lu hedef sayfalara bilinçli
// olarak kopyalanır (otomatik senkron yok — firma/taraf bilgileri farklı olabilir).
// Eşleme: aynı bölüm tipindeki bölümler sıra (SortOrder) düzeninde eşlenir.
public record CopyPageContentCommand(
    Guid SourcePageId,
    List<Guid> TargetPageIds,
    Guid UpdatedBy) : IRequest<Result<int>>;

public class CopyPageContentCommandHandler(ICmsDbContext db)
    : IRequestHandler<CopyPageContentCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CopyPageContentCommand request, CancellationToken ct)
    {
        var source = await db.Pages
            .AsNoTracking()
            .Include(p => p.Sections).ThenInclude(s => s.SectionType)
            .Include(p => p.Sections).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.Id == request.SourcePageId, ct);
        if (source is null)
            return Result.Failure<int>("Kaynak sayfa bulunamadı.");

        var targets = await db.Pages
            .Include(p => p.Sections).ThenInclude(s => s.SectionType)
            .Include(p => p.Sections).ThenInclude(s => s.Items)
            .Where(p => request.TargetPageIds.Contains(p.Id))
            .ToListAsync(ct);

        if (targets.Count == 0)
            return Result.Failure<int>("Hedef sayfa bulunamadı.");
        if (targets.Any(t => t.Code != source.Code))
            return Result.Failure<int>("Yalnız aynı içerik koduna sahip sayfalara kopyalanabilir.");
        if (targets.Any(t => t.Id == source.Id))
            return Result.Failure<int>("Kaynak sayfa hedef olarak seçilemez.");

        var kopyalanan = 0;
        foreach (var target in targets)
        {
            // Aynı tipteki bölümleri sıra düzeninde eşle
            var sourceByType = source.Sections.OrderBy(s => s.SortOrder)
                .GroupBy(s => s.SectionType.Code).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var grup in target.Sections.OrderBy(s => s.SortOrder).GroupBy(s => s.SectionType.Code))
            {
                if (!sourceByType.TryGetValue(grup.Key, out var kaynaklar)) continue;
                var hedefler = grup.ToList();

                for (var i = 0; i < hedefler.Count && i < kaynaklar.Count; i++)
                {
                    var k = kaynaklar[i];
                    var h = hedefler[i];

                    h.TitleI18n = k.TitleI18n is null ? null : new Dictionary<string, string>(k.TitleI18n);
                    h.Settings = new Dictionary<string, object>(k.Settings);
                    h.UpdatedAt = DateTime.UtcNow;
                    h.UpdatedBy = request.UpdatedBy;

                    // Öğeler: mevcutlar soft-delete, kaynaktakiler klonlanır
                    foreach (var eski in h.Items.Where(x => !x.IsDeleted))
                    {
                        eski.IsDeleted = true;
                        eski.DeletedAt = DateTime.UtcNow;
                        eski.DeletedBy = request.UpdatedBy;
                    }
                    foreach (var ki in k.Items.OrderBy(x => x.SortOrder))
                    {
                        db.PageSectionItems.Add(new PageSectionItem
                        {
                            SectionId = h.Id,
                            ItemType = ki.ItemType,
                            TitleI18n = ki.TitleI18n is null ? null : new Dictionary<string, string>(ki.TitleI18n),
                            SubtitleI18n = ki.SubtitleI18n is null ? null : new Dictionary<string, string>(ki.SubtitleI18n),
                            DescriptionI18n = ki.DescriptionI18n is null ? null : new Dictionary<string, string>(ki.DescriptionI18n),
                            ImageUrl = ki.ImageUrl,
                            LinkType = ki.LinkType,
                            LinkUrl = ki.LinkUrl,
                            SortOrder = ki.SortOrder,
                            IsActive = ki.IsActive,
                            CreatedBy = request.UpdatedBy
                        });
                    }
                    kopyalanan++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(kopyalanan);
    }
}
