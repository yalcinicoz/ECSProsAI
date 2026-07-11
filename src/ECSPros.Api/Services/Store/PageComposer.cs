using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Commands.PublishPageSnapshot;
using ECSPros.Storefront.Application.Queries.GetActivePageSnapshot;
using ECSPros.Storefront.Application.Queries.GetShowcaseCollections;
using ECSPros.Storefront.Domain;
using MediatR;

namespace ECSPros.Api.Services.Store;

/// <summary>Müşteriye dönen çözülmüş blok — RuleJson bilinçli olarak DIŞARI VERİLMEZ
/// (hedefleme verisi iç bilgidir); Config frontend'in ihtiyacı için aynen geçer
/// (tema, flash bitiş zamanı, "tümünü gör" linki...).</summary>
public record ResolvedBlockDto(
    Guid Id,
    string BlockType,
    string? Template,
    Dictionary<string, string> Title,
    Dictionary<string, string>? Subtitle,
    int SortOrder,
    string? Config,
    List<ResolvedItemDto> Items,
    List<StoreProductDto>? Products,
    List<ShowcaseCollectionDto>? Collections);

public record ResolvedItemDto(
    Guid Id,
    Dictionary<string, string> Title,
    Dictionary<string, string>? Subtitle,
    string? ImageUrl,
    string? MobileImageUrl,
    string? VideoUrl,
    string? LinkUrl,
    bool OpenInNewTab,
    Dictionary<string, string>? ButtonText,
    string? BadgeLabel,
    string? Config,
    List<StoreProductDto>? Products); // tabs: sekmenin ürünleri

public interface IPageComposer
{
    /// <summary>Aktif snapshot'tan bir yerleşimin görünür bloklarını çözer (yayın yoksa boş).</summary>
    Task<(int Version, List<ResolvedBlockDto> Blocks)> ComposeAsync(
        Guid firmPlatformId, string placement, CancellationToken ct = default);

    /// <summary>Infinity devam yüklemesi: snapshot'taki ürün bloğunun N. sayfası.</summary>
    Task<List<StoreProductDto>?> ResolveBlockProductsAsync(
        Guid firmPlatformId, Guid blockId, int page, CancellationToken ct = default);
}

/// <summary>
/// G4: canlı kompozisyon — store API (mobil app dahil) ve Razor render (G5) aynı
/// serviste buluşur. Taslak tablolara asla gidilmez: girdi aktif snapshot'tır. G-M1'de
/// kurallar değerlendirilmez (herkese görünür — plan kararı; G-M2 kural motorunu bu
/// noktaya bağlar). Tarih pencereleri istek anında değerlendirilir. Öğeli tiplerde
/// görünür öğe kalmazsa, ürün/koleksiyon bloklarında kaynak boş dönerse blok hiç
/// verilmez (spec: boş blok basılmaz, boşluk bırakmaz). Versiyon bazlı cache G7'de
/// bu servisin önüne gelir.
/// </summary>
public class PageComposer(IMediator mediator, IPageBlockSourceResolver resolver) : IPageComposer
{
    public async Task<(int Version, List<ResolvedBlockDto> Blocks)> ComposeAsync(
        Guid firmPlatformId, string placement, CancellationToken ct = default)
    {
        var snapshot = await AktifSnapshotAsync(firmPlatformId, ct);
        if (snapshot is null) return (0, []);

        var simdi = DateTime.UtcNow;
        var sonuc = new List<ResolvedBlockDto>();

        foreach (var blok in snapshot.Blocks
            .Where(b => b.Placement == placement && TarihPenceresinde(b.StartAt, b.EndAt, simdi))
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Priority))
        {
            var def = PageBlockCatalog.Find(blok.BlockType);
            if (def is null) continue; // eski snapshot'ta artık tanınmayan tip — sessizce atla

            var ogeler = blok.Items
                .Where(i => TarihPenceresinde(i.StartAt, i.EndAt, simdi))
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Priority)
                .ToList();
            if (def.SupportsItems && ogeler.Count == 0) continue; // içeriksiz öğeli blok basılmaz

            List<StoreProductDto>? urunler = null;
            if (def.RequiresProductSource)
            {
                var kaynak = resolver.ParseProductSource(blok.Config);
                urunler = kaynak is null ? [] : await resolver.ResolveProductsAsync(firmPlatformId, kaynak, 1, ct);
                if (urunler.Count == 0) continue; // ürünsüz ürün bloğu basılmaz
            }

            List<ShowcaseCollectionDto>? koleksiyonlar = null;
            if (def.RequiresCollectionSource)
            {
                var kaynak = resolver.ParseCollectionSource(blok.Config) ?? new BlockCollectionSource();
                koleksiyonlar = await resolver.ResolveCollectionsAsync(firmPlatformId, kaynak, ct);
                if (koleksiyonlar.Count == 0) continue;
            }

            // Tabs: her sekmenin ürünleri kendi config'inden (öğesiz sekme boş kalabilir —
            // sekme başlığı yine gösterilir, içerik frontend'de "ürün yok" durumudur)
            var cozulmusOgeler = new List<ResolvedItemDto>();
            foreach (var oge in ogeler)
            {
                List<StoreProductDto>? ogeUrunleri = null;
                if (blok.BlockType == "tabs")
                {
                    var tabKaynak = resolver.ParseProductSource(oge.Config);
                    if (tabKaynak is not null)
                        ogeUrunleri = await resolver.ResolveProductsAsync(firmPlatformId, tabKaynak, 1, ct);
                }
                cozulmusOgeler.Add(new ResolvedItemDto(
                    oge.Id, oge.Title, oge.Subtitle, oge.ImageUrl, oge.MobileImageUrl, oge.VideoUrl,
                    oge.LinkUrl, oge.OpenInNewTab, oge.ButtonText, oge.BadgeLabel, oge.Config, ogeUrunleri));
            }

            sonuc.Add(new ResolvedBlockDto(
                blok.Id, blok.BlockType, blok.Template, blok.Title, blok.Subtitle,
                blok.SortOrder, blok.Config, cozulmusOgeler, urunler, koleksiyonlar));
        }

        return (snapshot.Version, sonuc);
    }

    public async Task<List<StoreProductDto>?> ResolveBlockProductsAsync(
        Guid firmPlatformId, Guid blockId, int page, CancellationToken ct = default)
    {
        var snapshot = await AktifSnapshotAsync(firmPlatformId, ct);
        var blok = snapshot?.Blocks.FirstOrDefault(b => b.Id == blockId);
        if (blok is null) return null;

        var kaynak = resolver.ParseProductSource(blok.Config);
        if (kaynak is null) return null;
        return await resolver.ResolveProductsAsync(firmPlatformId, kaynak, Math.Max(1, page), ct);
    }

    private async Task<PageSnapshotDto?> AktifSnapshotAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetActivePageSnapshotQuery(firmPlatformId), ct);
        return sonuc.IsSuccess ? sonuc.Value : null;
    }

    private static bool TarihPenceresinde(DateTime? baslangic, DateTime? bitis, DateTime simdi) =>
        (baslangic is null || baslangic <= simdi) && (bitis is null || bitis >= simdi);
}
