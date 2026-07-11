using System.Text.Json;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.PublishPageSnapshot;

/// <summary>
/// G4/G6: "Yayınla" — platformun aktif taslak bloklarını PageBlockCatalog'a karşı
/// doğrular, versiyonlu JSON snapshot üretir, önceki aktif snapshot'ı superseded yapar
/// ve publish_logs'a yazar. Doğrulama hatasında HİÇBİR ŞEY yayınlanmaz (mevcut yayın
/// bozulmaz), deneme failed olarak loglanır ve hatalar mesajda döner (spec: yarım
/// değişiklik siteyi bozamaz).
/// </summary>
public record PublishPageSnapshotCommand(
    Guid FirmPlatformId,
    Guid? PublishedBy = null,
    string? Note = null) : IRequest<Result<int>>; // yeni versiyon numarası

public class PublishPageSnapshotCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<PublishPageSnapshotCommand, Result<int>>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<Result<int>> Handle(PublishPageSnapshotCommand request, CancellationToken ct)
    {
        var bloklar = await db.PageBlocks
            .AsNoTracking()
            .Include(b => b.Items)
            .Where(b => b.FirmPlatformId == request.FirmPlatformId && b.IsActive)
            .OrderBy(b => b.Placement).ThenBy(b => b.SortOrder).ThenBy(b => b.Priority)
            .ToListAsync(ct);

        var oncekiAktif = await db.PublishedSnapshots
            .FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId && s.IsActive, ct);
        var sonVersiyon = await db.PublishedSnapshots
            .IgnoreQueryFilters()
            .Where(s => s.FirmPlatformId == request.FirmPlatformId)
            .MaxAsync(s => (int?)s.Version, ct) ?? 0;
        var yeniVersiyon = sonVersiyon + 1;

        // ── Validasyon (katalog tek doğruluk kaynağı) ──
        var hatalar = new List<string>();
        foreach (var b in bloklar)
        {
            var ad = $"{b.Placement}/{b.BlockType} '{BlokAdi(b)}'";
            var def = PageBlockCatalog.Find(b.BlockType);
            if (def is null) { hatalar.Add($"{ad}: bilinmeyen blok tipi."); continue; }
            if (!PageBlockCatalog.IsValidPlacement(b.Placement))
                hatalar.Add($"{ad}: geçersiz yerleşim.");
            if (!PageBlockCatalog.IsValidTemplate(b.BlockType, b.Template))
                hatalar.Add($"{ad}: geçersiz şablon '{b.Template}'.");
            if (def.RequiresProductSource && !ConfigDugumVar(b.ConfigJson, "productSource"))
                hatalar.Add($"{ad}: ürün kaynağı tanımsız (config.productSource).");
            if (def.RequiresCollectionSource && !ConfigDugumVar(b.ConfigJson, "collectionSource"))
                hatalar.Add($"{ad}: koleksiyon kaynağı tanımsız (config.collectionSource).");
            if (b.RuleJson is not null && !PageBlockCatalog.AllowsBlockRules(b.BlockType))
                hatalar.Add($"{ad}: bu tipte blok kuralı verilemez (kural öğe seviyesinde).");
            // G10: kural İÇERİĞİ de yayın engelidir — runtime bozuk kuralı gizleyerek
            // karşılar ama bozuk kural yayına hiç girmemeli (katalog tek doğruluk kaynağı).
            foreach (var h in PageBlockCatalog.ValidateRule(b.RuleJson))
                hatalar.Add($"{ad}: {h}");
            foreach (var i in b.Items.Where(i => i.IsActive && i.RuleJson is not null))
            {
                if (!PageBlockCatalog.AllowsItemRules(b.BlockType))
                    hatalar.Add($"{ad}: bu tipte öğe kuralı verilemez (spec: banner öğeleri kuralsız).");
                foreach (var h in PageBlockCatalog.ValidateRule(i.RuleJson))
                    hatalar.Add($"{ad}: öğe kuralı — {h}");
            }
        }

        if (hatalar.Count > 0)
        {
            db.PublishLogs.Add(new PublishLog
            {
                FirmPlatformId = request.FirmPlatformId,
                Version = yeniVersiyon,
                PreviousVersion = oncekiAktif?.Version,
                PublishedBy = request.PublishedBy,
                PublishedAt = DateTime.UtcNow,
                Status = "failed",
                ErrorMessage = string.Join(" | ", hatalar.Take(20)),
                Note = request.Note,
            });
            await db.SaveChangesAsync(ct);
            return Result.Failure<int>("Yayınlanamadı: " + string.Join(" | ", hatalar.Take(5)));
        }

        // ── Snapshot üretimi (yalnız aktif blok/öğeler; tarih penceresi runtime'da) ──
        var simdi = DateTime.UtcNow;
        var snapshot = new PageSnapshotDto(yeniVersiyon, simdi, bloklar.Select(b => new SnapshotBlockDto(
            b.Id, b.Placement, b.BlockType, b.Template, b.TitleI18n, b.SubtitleI18n,
            b.SortOrder, b.Priority, b.StartAt, b.EndAt, b.RuleJson, b.ConfigJson,
            b.Items.Where(i => i.IsActive && !i.IsDeleted)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Priority)
                .Select(i => new SnapshotItemDto(
                    i.Id, i.TitleI18n, i.SubtitleI18n, i.ImageUrl, i.MobileImageUrl, i.VideoUrl,
                    i.LinkUrl, i.OpenInNewTab, i.ButtonTextI18n, i.BadgeLabel,
                    i.SortOrder, i.Priority, i.StartAt, i.EndAt, i.RuleJson, i.ConfigJson))
                .ToList())).ToList());

        if (oncekiAktif is not null)
        {
            oncekiAktif.IsActive = false;
            oncekiAktif.Status = "superseded";
        }

        db.PublishedSnapshots.Add(new PublishedSnapshot
        {
            FirmPlatformId = request.FirmPlatformId,
            Version = yeniVersiyon,
            JsonData = JsonSerializer.Serialize(snapshot, JsonOpts),
            PublishedAt = simdi,
            PublishedBy = request.PublishedBy,
            IsActive = true,
            Status = "published",
            Note = request.Note,
        });

        db.PublishLogs.Add(new PublishLog
        {
            FirmPlatformId = request.FirmPlatformId,
            Version = yeniVersiyon,
            PreviousVersion = oncekiAktif?.Version,
            PublishedBy = request.PublishedBy,
            PublishedAt = simdi,
            Status = "success",
            Note = request.Note,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(yeniVersiyon);
    }

    private static string BlokAdi(PageBlock b) =>
        b.TitleI18n.TryGetValue("tr", out var ad) ? ad : b.TitleI18n.Values.FirstOrDefault() ?? b.Id.ToString("N")[..8];

    private static bool ConfigDugumVar(string? configJson, string dugum)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.TryGetProperty(dugum, out var el) && el.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }
}
