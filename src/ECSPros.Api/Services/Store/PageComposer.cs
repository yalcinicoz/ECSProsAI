using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Shared.Contracts;
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
public class PageComposer(IMediator mediator, IPageBlockSourceResolver resolver, ICacheService cache) : IPageComposer
{
    // G7: anahtar versiyonu içerir — yeni yayın eski anahtarları kendiliğinden geçersiz
    // kılar (spec). TTL kısa: kompozisyon içinde ürün fiyat/puan gibi versiyondan
    // bağımsız tazelenen veri var. Redis kuralları: ICacheService hata-güvenli, Redis
    // yoksa NoOp — cache'siz de doğru çalışır (kod cache'e bağımlılık kurmaz).
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private sealed record BlokPaketi(List<ResolvedBlockDto> Blocks);
    private sealed record UrunPaketi(List<StoreProductDto> Items);

    public async Task<(int Version, List<ResolvedBlockDto> Blocks)> ComposeAsync(
        Guid firmPlatformId, string placement, CancellationToken ct = default)
    {
        var aktif = await AktifVersiyonAsync(firmPlatformId, ct);
        if (aktif.Version == 0) return (0, []);

        var anahtar = $"page:{placement}:{firmPlatformId}:v{aktif.Version}:{aktif.SnapshotId:N}";
        var hazir = await cache.GetAsync<BlokPaketi>(anahtar, ct);
        if (hazir is not null) return (aktif.Version, hazir.Blocks);

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

            // Kaynak zorunlu olmayan tiplerde de config'te varsa çözülür (banner "reklam"
            // kompoziti ürün şeridi taşır) — görünürlüğü yalnız zorunlu tiplerde bağlar.
            List<StoreProductDto>? urunler = null;
            var urunKaynagi = resolver.ParseProductSource(blok.Config);
            if (urunKaynagi is not null || def.RequiresProductSource)
            {
                urunler = urunKaynagi is null ? [] : await resolver.ResolveProductsAsync(firmPlatformId, urunKaynagi, 1, ct);
                if (def.RequiresProductSource && urunler.Count == 0) continue; // ürünsüz ürün bloğu basılmaz
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

        await cache.SetAsync(anahtar, new BlokPaketi(sonuc), CacheTtl, ct);
        return (snapshot.Version, sonuc);
    }

    public async Task<List<StoreProductDto>?> ResolveBlockProductsAsync(
        Guid firmPlatformId, Guid blockId, int page, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var aktif = await AktifVersiyonAsync(firmPlatformId, ct);
        if (aktif.Version == 0) return null;

        // Spec anahtarı: homepage-products:{version}:{blockId}:{segmentHash}:page:{n} — segment G-M2'de
        var anahtar = $"page-products:v{aktif.Version}:{aktif.SnapshotId:N}:{blockId}:page:{page}";
        var hazir = await cache.GetAsync<UrunPaketi>(anahtar, ct);
        if (hazir is not null) return hazir.Items;

        var snapshot = await AktifSnapshotAsync(firmPlatformId, ct);
        var blok = snapshot?.Blocks.FirstOrDefault(b => b.Id == blockId);
        if (blok is null) return null;

        var kaynak = resolver.ParseProductSource(blok.Config);
        if (kaynak is null) return null;
        var urunler = await resolver.ResolveProductsAsync(firmPlatformId, kaynak, page, ct);
        await cache.SetAsync(anahtar, new UrunPaketi(urunler), CacheTtl, ct);
        return urunler;
    }

    private async Task<SnapshotVersionDto> AktifVersiyonAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetActivePageSnapshotVersionQuery(firmPlatformId), ct);
        return sonuc.IsSuccess ? sonuc.Value! : new SnapshotVersionDto(0, Guid.Empty);
    }

    private async Task<PageSnapshotDto?> AktifSnapshotAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetActivePageSnapshotQuery(firmPlatformId), ct);
        return sonuc.IsSuccess ? sonuc.Value : null;
    }

    private static bool TarihPenceresinde(DateTime? baslangic, DateTime? bitis, DateTime simdi) =>
        (baslangic is null || baslangic <= simdi) && (bitis is null || bitis >= simdi);
}
