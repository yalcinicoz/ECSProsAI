using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Store;

/// <summary>Önizleme satırı: taslak bloğun bu segmentte görünüp görünmeyeceği + nedeni
/// (spec: "hangi blokların neden göründüğü veya neden gizlendiği de gösterilebilir").</summary>
public record PreviewBlockDto(
    Guid Id,
    string BlockType,
    string? Template,
    Dictionary<string, string> Title,
    int SortOrder,
    bool IsActive,
    bool Visible,
    string Reason,
    int ItemTotal,
    int ItemVisible,
    int? ProductCount);

public interface IPagePreviewService
{
    Task<List<PreviewBlockDto>> PreviewAsync(
        Guid firmPlatformId, string placement, VisitorSegment segment, CancellationToken ct = default);
}

/// <summary>
/// G12: admin önizlemesi — TASLAK veri üzerinden (spec; canlı site yalnız aktif snapshot
/// okur, yayınlanmamış değişiklik önizlemede görünür). PageComposer'ın görünürlük karar
/// noktalarını AYNI SIRAYLA yürütür (tarih penceresi → blok kuralı → öğe süzme → kaynak
/// boşluğu) ama gizleneni atlamak yerine nedeniyle listeler. Cache'e yazmaz, canlıyı
/// etkilemez; ürün sayıları canlı katalogdan (önizleme anındaki gerçek sonuç).
/// </summary>
public class PagePreviewService(IStorefrontDbContext db, IPageBlockSourceResolver resolver) : IPagePreviewService
{
    public async Task<List<PreviewBlockDto>> PreviewAsync(
        Guid firmPlatformId, string placement, VisitorSegment segment, CancellationToken ct = default)
    {
        var bloklar = await db.PageBlocks.AsNoTracking()
            .Include(b => b.Items.Where(i => !i.IsDeleted))
            .Where(b => b.FirmPlatformId == firmPlatformId && b.Placement == placement)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Priority)
            .ToListAsync(ct);

        var simdi = DateTime.UtcNow;
        var sonuc = new List<PreviewBlockDto>();

        foreach (var blok in bloklar)
        {
            var aktifOgeler = blok.Items.Where(i => i.IsActive).ToList();
            var satir = await DegerlendirAsync(firmPlatformId, blok, aktifOgeler, segment, simdi, ct);
            sonuc.Add(satir);
        }
        return sonuc;
    }

    private async Task<PreviewBlockDto> DegerlendirAsync(
        Guid firmPlatformId, Storefront.Domain.Entities.PageBlock blok,
        List<Storefront.Domain.Entities.PageBlockItem> ogeler,
        VisitorSegment segment, DateTime simdi, CancellationToken ct)
    {
        PreviewBlockDto Satir(bool gorunur, string neden, int gorunenOge = 0, int? urun = null) => new(
            blok.Id, blok.BlockType, blok.Template, blok.TitleI18n, blok.SortOrder,
            blok.IsActive, gorunur, neden, ogeler.Count, gorunenOge, urun);

        if (!blok.IsActive)
            return Satir(false, "Blok pasif — hiçbir koşulda gösterilmez.");

        var def = PageBlockCatalog.Find(blok.BlockType);
        if (def is null)
            return Satir(false, $"Bilinmeyen blok tipi '{blok.BlockType}'.");

        if (!Pencerede(blok.StartAt, blok.EndAt, simdi))
            return Satir(false, "Tarih penceresi dışında.");

        if (!PageRuleEvaluator.Matches(blok.RuleJson, segment))
            return Satir(false, "Blok kuralı segmente uymuyor.");

        var gorunenler = ogeler
            .Where(i => Pencerede(i.StartAt, i.EndAt, simdi) && PageRuleEvaluator.Matches(i.RuleJson, segment))
            .ToList();
        if (def.SupportsItems && gorunenler.Count == 0)
            return Satir(false, ogeler.Count == 0
                ? "Hiç öğe tanımlı değil."
                : $"Görünür öğe kalmadı ({ogeler.Count} öğeden 0 eşleşti — öğe kuralları/tarih).");

        int? urunSayisi = null;
        var kaynak = resolver.ParseProductSource(blok.ConfigJson);
        if (kaynak is not null || def.RequiresProductSource)
        {
            // H10: üye bağlamlı kaynak önizlemede çözülemez (admin oturumu üye değil) —
            // dürüst gerekçeyle "üyeye göre" işaretlenir, gizli sayılmaz.
            if (kaynak is not null && PageBlockSourceResolver.UyeBaglamli(kaynak.Source))
                return Satir(true, $"Üye bağlamlı kaynak ({kaynak.Source}) — içerik ziyaretçinin kendi verisiyle dolar; misafirde/verisiz üyede blok basılmaz.", gorunenler.Count, null);

            urunSayisi = kaynak is null ? 0
                : (await resolver.ResolveProductsAsync(firmPlatformId, kaynak, 1, ct: ct)).Count;
            if (def.RequiresProductSource && urunSayisi == 0)
                return Satir(false, "Ürün kaynağı boş — ürünsüz ürün bloğu basılmaz.", gorunenler.Count, 0);
        }

        if (def.RequiresCollectionSource)
        {
            var koleksiyonKaynak = resolver.ParseCollectionSource(blok.ConfigJson) ?? new BlockCollectionSource();
            var koleksiyonlar = await resolver.ResolveCollectionsAsync(firmPlatformId, koleksiyonKaynak, ct);
            if (koleksiyonlar.Count == 0)
                return Satir(false, "Koleksiyon kaynağı boş.", gorunenler.Count);
        }

        var neden = def.SupportsItems
            ? $"{gorunenler.Count} öğe eşleşti" + (blok.RuleJson is null ? " (blok kuralsız)." : " (blok kuralı uydu).")
            : (urunSayisi is not null ? $"{urunSayisi} ürün bulundu" : "İçerik hazır")
              + (blok.RuleJson is null ? " (kuralsız — herkese)." : " (kural segmente uydu).");
        return Satir(true, neden, gorunenler.Count, urunSayisi);
    }

    private static bool Pencerede(DateTime? baslangic, DateTime? bitis, DateTime simdi) =>
        (baslangic is null || baslangic <= simdi) && (bitis is null || bitis >= simdi);
}
