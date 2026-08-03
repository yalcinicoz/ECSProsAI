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
    /// <summary>Aktif snapshot'tan bir yerleşimin, verilen segmentin görebileceği bloklarını
    /// çözer (yayın yoksa boş). Segment zorunludur — misafir için VisitorSegment.Misafir;
    /// G12 admin önizlemesi kurgu segment geçer.</summary>
    Task<(int Version, List<ResolvedBlockDto> Blocks)> ComposeAsync(
        Guid firmPlatformId, string placement, VisitorSegment segment, Guid? memberId = null, CancellationToken ct = default);

    /// <summary>Infinity devam yüklemesi: snapshot'taki ürün bloğunun N. sayfası
    /// (blok bu segmente kuraldan görünmüyorsa null — içerik sızmaz).</summary>
    Task<List<StoreProductDto>?> ResolveBlockProductsAsync(
        Guid firmPlatformId, Guid blockId, int page, VisitorSegment segment, Guid? memberId = null, CancellationToken ct = default);
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
        Guid firmPlatformId, string placement, VisitorSegment segment, Guid? memberId = null, CancellationToken ct = default)
    {
        var aktif = await AktifVersiyonAsync(firmPlatformId, ct);
        if (aktif.Version == 0) return (0, []);

        // G10/G11: anahtar segment hash'i içerir (spec) — aynı yayında farklı segmentler
        // farklı kompozisyon görebilir; kuralsız yayında hash misafir/üye başına aynı
        // içeriği tekrarlar (kabul: TTL 5 dk, paket küçük).
        // H10: üye bağlamlı kaynaklı bloklar (recently-viewed/favorites) pakete ÜRÜNSÜZ
        // yazılır — anahtar üyeyi ayırmaz; ürünleri her istekte UyeBloklariniDoldurAsync doldurur.
        // c2: StoreProductDto'ya CompareAtPrice (çizili fiyat) eklendi — eski serileştirilmiş
        // paketler bu alanı içermediğinden kod-şema sürüm işareti eklendi (eski kayıtlar atlanır).
        // c3: CampaignName/CampaignPrice (F3); c4: CampaignNames; c5: CampaignNames→CampaignBadges (ad+renk, 2026-08-03).
        var anahtar = $"page:c5:{placement}:{firmPlatformId}:v{aktif.Version}:{aktif.SnapshotId:N}:seg:{segment.CacheHash()}";
        var hazir = await cache.GetAsync<BlokPaketi>(anahtar, ct);
        if (hazir is not null)
            return (aktif.Version, await UyeBloklariniDoldurAsync(firmPlatformId, hazir.Blocks, memberId, ct));

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

            // G10: blok kuralı — uymayan ziyaretçiye blok hiç basılmaz (boşluk bırakmaz,
            // default aranmaz — spec). Kural yoksa herkese.
            if (!PageRuleEvaluator.Matches(blok.Rule, segment)) continue;

            var ogeler = blok.Items
                .Where(i => TarihPenceresinde(i.StartAt, i.EndAt, simdi)
                            && PageRuleEvaluator.Matches(i.Rule, segment)) // G10: öğe kuralı
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Priority)
                .ToList();
            if (def.SupportsItems && ogeler.Count == 0) continue; // içeriksiz öğeli blok basılmaz

            // Kaynak zorunlu olmayan tiplerde de config'te varsa çözülür (banner "reklam"
            // kompoziti ürün şeridi taşır) — görünürlüğü yalnız zorunlu tiplerde bağlar.
            List<StoreProductDto>? urunler = null;
            var urunKaynagi = resolver.ParseProductSource(blok.Config);
            if (urunKaynagi is not null || def.RequiresProductSource)
            {
                // H10: üye bağlamlı kaynak pakete boş yazılır (görünürlük kararı istek anında —
                // UyeBloklariniDoldurAsync misafir/verisiz üyede bloğu düşürür)
                if (urunKaynagi is not null && PageBlockSourceResolver.UyeBaglamli(urunKaynagi.Source))
                {
                    urunler = [];
                }
                else
                {
                    urunler = urunKaynagi is null ? [] : await resolver.ResolveProductsAsync(firmPlatformId, urunKaynagi, 1, ct: ct);
                    if (def.RequiresProductSource && urunler.Count == 0) continue; // ürünsüz ürün bloğu basılmaz
                }
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
                    // H10: tabs sekmelerinde üye bağlamlı kaynak desteklenmez (paket cache'i) — boş kalır
                    if (tabKaynak is not null && !PageBlockSourceResolver.UyeBaglamli(tabKaynak.Source))
                        ogeUrunleri = await resolver.ResolveProductsAsync(firmPlatformId, tabKaynak, 1, ct: ct);
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
        return (snapshot.Version, await UyeBloklariniDoldurAsync(firmPlatformId, sonuc, memberId, ct));
    }

    /// <summary>H10: üye bağlamlı kaynaklı blokların ürünlerini İSTEK ANINDA doldurur —
    /// cache'lenen paket bu blokları ürünsüz taşır (bir üyenin listesi başkasına sızmaz).
    /// Misafirde ya da verisiz üyede blok listeden düşer (ürünsüz blok basılmaz kuralı).
    /// Not: tabs SEKMELERİNDE üye bağlamlı kaynak desteklenmez (sekme ürünleri pakete
    /// cache'lendiğinden boş kalır) — bilinçli sınır.</summary>
    private async Task<List<ResolvedBlockDto>> UyeBloklariniDoldurAsync(
        Guid firmPlatformId, List<ResolvedBlockDto> bloklar, Guid? memberId, CancellationToken ct)
    {
        List<ResolvedBlockDto>? yeni = null; // üye bağlamlı blok yoksa liste aynen döner
        for (var i = 0; i < bloklar.Count; i++)
        {
            var blok = bloklar[i];
            var kaynak = resolver.ParseProductSource(blok.Config);
            if (kaynak is null || !PageBlockSourceResolver.UyeBaglamli(kaynak.Source)) continue;

            yeni ??= new List<ResolvedBlockDto>(bloklar);
            var urunler = memberId is null
                ? []
                : await resolver.ResolveProductsAsync(firmPlatformId, kaynak, 1, memberId, ct);
            yeni[i] = urunler.Count == 0 ? null! : blok with { Products = urunler };
        }
        return yeni is null ? bloklar : yeni.Where(b => b is not null).ToList();
    }

    public async Task<List<StoreProductDto>?> ResolveBlockProductsAsync(
        Guid firmPlatformId, Guid blockId, int page, VisitorSegment segment, Guid? memberId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var aktif = await AktifVersiyonAsync(firmPlatformId, ct);
        if (aktif.Version == 0) return null;

        // G11: spec anahtarı — segment hash'li. Ürün içeriği M2'de segmentten bağımsız,
        // ama blok görünürlüğü değil: cache miss'te kural denetlenir, hit o segment için
        // zaten doğrulanmış sonuçtur (gizli bloğun ürünleri bu segmente hiç yazılmaz).
        var anahtar = $"page-products:c5:v{aktif.Version}:{aktif.SnapshotId:N}:{blockId}:seg:{segment.CacheHash()}:page:{page}";
        // H10 notu: üye bağlamlı bloklar bu anahtara HİÇ yazılmaz (aşağıda bypass) —
        // hit her zaman üye-bağımsız bir bloğa aittir, snapshot yüklemeden dönmek güvenli.
        var hazir = await cache.GetAsync<UrunPaketi>(anahtar, ct);
        if (hazir is not null) return hazir.Items;

        var snapshot = await AktifSnapshotAsync(firmPlatformId, ct);
        var blok = snapshot?.Blocks.FirstOrDefault(b => b.Id == blockId);
        if (blok is null) return null;
        // G10: kuraldan görünmeyen bloğun devam sayfası da bu segmente kapalı (404 ile aynı yol)
        if (!PageRuleEvaluator.Matches(blok.Rule, segment)) return null;

        var kaynak = resolver.ParseProductSource(blok.Config);
        if (kaynak is null) return null;

        // H10: üye bağlamlı kaynak CACHE'E GİRMEZ — her istekte üyeye göre taze (sızma olmaz)
        if (PageBlockSourceResolver.UyeBaglamli(kaynak.Source))
            return await resolver.ResolveProductsAsync(firmPlatformId, kaynak, page, memberId, ct);

        var urunler = await resolver.ResolveProductsAsync(firmPlatformId, kaynak, page, ct: ct);
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
