using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Handlers;

public class GetStoreProductDetailHandler(ICatalogDbContext db, IInventoryDbContext invDb,
    IChannelPricingService pricingService, IChannelProductFlagService flagService,
    IProductCampaignResolver campaignResolver, IMediator mediator)
    : IRequestHandler<GetStoreProductDetailQuery, Result<StoreProductDetailDto>>
{
    public async Task<Result<StoreProductDetailDto>> Handle(GetStoreProductDetailQuery request, CancellationToken ct)
    {
        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.VariantAttributes).ThenInclude(va => va.AttributeType)
            .Include(p => p.Variants).ThenInclude(v => v.VariantAttributes).ThenInclude(va => va.AttributeValue)
            .AsSplitQuery()   // Faz 2: kardeş koleksiyon Include kartezyeni önlenir
            .FirstOrDefaultAsync(p => p.Code == request.ProductCode && p.IsSaleOpen, ct);

        if (product is null)
            return Result.Failure<StoreProductDetailDto>("Ürün bulunamadı.");

        // Faz 2 P0: kanal fiyatları yalnız BU ürünün varyantları için (tam platform çekimi kaldırıldı).
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(
            request.FirmPlatformId, product.Variants.Select(v => v.Id).ToList(), ct);

        // Kanal seçimi/durdurma (M2/M3): bu kanalda çıkarılan/durdurulan ürün detaydan da düşer
        // (Failure → UrunDetayController 301 ile kategoriye/ana sayfaya yönlendirir).
        var kanalDisi = await flagService.GetChannelExcludedProductIdsAsync(request.FirmPlatformId, ct);
        if (kanalDisi.Contains(product.Id))
            return Result.Failure<StoreProductDetailDto>("Ürün bu kanalda satışa kapalı.");

        var activeVariantIds = product.Variants.Where(v => v.IsActive).Select(v => v.Id).ToList();

        // Hex kodları — AttributeValue.HexCode direkt kullanılır (A3 2026-09-07: renk ekseninde de varsa).
        var hexByValueId = product.Variants
            .SelectMany(v => v.VariantAttributes)
            .Where(va => va.AttributeType.Code is "filtre_rengi" or "renk" && va.AttributeValue.HexCode != null)
            .GroupBy(va => va.AttributeValue.Id)
            .ToDictionary(g => g.Key, g => g.First().AttributeValue.HexCode!);
        // A3: renk ekseni 'renk' — filtre_rengi (aile) olmayan üründe (1.338 ürün) IsColor hiç true olmuyordu.
        // Kural: 'renk' varsa renk ekseni odur; yoksa filtre_rengi renk sayılır.
        var renkEkseniVar = product.Variants.Any(v => v.VariantAttributes.Any(va => va.AttributeType.Code == "renk"));

        // Görseller — FileName+VariantId bazlı deduplicate (DB'de aynı resim çift kayıtlı olabilir)
        var allImgs = (await db.ProductImages.AsNoTracking()
            .Where(img => img.ProductId == product.Id && img.Status == ProductImageStatus.Active)
            .OrderBy(img => img.SortOrder)
            .Select(img => new { img.Id, img.FileName, img.SortOrder, img.IsProductCover, img.VariantId })
            .ToListAsync(ct))
            .GroupBy(i => (i.FileName, i.VariantId))
            .Select(g => g.First())
            .ToList();

        var rawByVariantId = allImgs
            .Where(i => i.VariantId.HasValue)
            .GroupBy(i => i.VariantId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Renk grubu = varyantın RENK EKSENİ değeri (her rengin kendi kimliği) — 2026-09-06 kullanıcı kararı.
        // filtre_rengi kaba bir renk AİLESİdir (katalog filtresi + renk noktası için); gruplama anahtarı
        // olarak kullanılınca aynı aileye düşen iki gerçek renk (Mavi + İndigo → filtre Mavi) tek seçeneğe
        // çöküp galerileri karışıyordu (P-00022295). Yalnız renk ekseni olmayan katalogda filtre_rengi'ne düşülür.
        var variantColorValue = product.Variants.Where(v => v.IsActive)
            .Select(v => new
            {
                VariantId = v.Id,
                ColorValueId = v.VariantAttributes
                    .Where(va => va.AttributeType.Code == "renk")
                    .Select(va => (Guid?)va.AttributeValue.Id).FirstOrDefault()
                    ?? v.VariantAttributes
                    .Where(va => va.AttributeType.Code == "filtre_rengi")
                    .Select(va => (Guid?)va.AttributeValue.Id).FirstOrDefault()
            })
            .Where(x => x.ColorValueId.HasValue)
            .ToDictionary(x => x.VariantId, x => x.ColorValueId!.Value);

        var colorToVariantIds = variantColorValue
            .GroupBy(kv => kv.Value, kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Aynı renkteki varyantların görselleri birleşirken dosya adına göre teke iner:
        // aynı fotoğraf birden çok varyanta ayrı kayıtla bağlı olabilir.
        var imgsByColor = colorToVariantIds.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .SelectMany(vid => rawByVariantId.TryGetValue(vid, out var imgs) ? imgs : [])
                .GroupBy(i => i.FileName).Select(g => g.First())
                .OrderBy(i => i.SortOrder)
                .Select(i => new StoreVariantImageDto(i.Id, cdnBase + i.FileName, i.SortOrder, i.IsProductCover))
                .ToList());

        var imgsByVariantId = rawByVariantId.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .Select(i => new StoreVariantImageDto(i.Id, cdnBase + i.FileName, i.SortOrder, i.IsProductCover))
                .ToList());

        var productImages = allImgs
            .Where(i => !i.VariantId.HasValue)
            .GroupBy(i => i.FileName).Select(g => g.First())
            .OrderBy(i => i.SortOrder)
            .Select(i => new StoreVariantImageDto(i.Id, cdnBase + i.FileName, i.SortOrder, i.IsProductCover))
            .ToList();

        // Stok — cutover (2026-07-14): yalnız satışa-AÇIK kısımların (aktif depo) serbest stoğu.
        var stockByVariant = activeVariantIds.Count > 0
            ? await (from s in invDb.Stocks.AsNoTracking()
                     where activeVariantIds.Contains(s.VariantId) && s.BinId != null
                     join sec in invDb.WarehouseSections on s.SectionId equals sec.Id
                     join w in invDb.Warehouses on s.WarehouseId equals w.Id
                     where sec.IsSellableOnline && w.IsActive
                     group (s.Quantity - s.ReservedQuantity) by s.VariantId into g
                     select new { VariantId = g.Key, Total = g.Sum() })
                .ToDictionaryAsync(x => x.VariantId, x => x.Total, ct)
            : new Dictionary<Guid, int>();

        // i18n değer okuma (tr → ilk değer → yedek): variantInfo metni için.
        static string TrDeger(Dictionary<string, string> i18n, string yedek)
            => i18n.TryGetValue("tr", out var tr) && !string.IsNullOrWhiteSpace(tr)
                ? tr : i18n.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? yedek;

        var variants = product.Variants.Where(v => v.IsActive).Select(v =>
        {
            channelPrices.TryGetValue(v.Id, out var channelPrice);
            var attrs = v.VariantAttributes.Select(a => new StoreVariantAttributeDto(
                a.AttributeType.Code, a.AttributeType.NameI18n,
                a.AttributeValue.Id, a.AttributeValue.NameI18n,
                IsColor: renkEkseniVar ? a.AttributeType.Code == "renk" : a.AttributeType.Code == "filtre_rengi",
                HexCode: hexByValueId.GetValueOrDefault(a.AttributeValue.Id),
                ValueSortOrder: a.AttributeValue.SortOrder)).ToList();

            // Öncelik: renk grubu görselleri (renk ekseni; yedek filtre_rengi) → varyantın kendi görselleri
            // (VariantId doğrudan eşleşen) → ürün düzeyinde ortak görseller (VariantId=null).
            // Renk ekseni hiç olmayan kataloglarda ikinci adım olmasa varyanta özel görseller
            // sessizce kaybolurdu. Ürün düzeyi havuz yalnızca üründe hiç varyant-bağlı görsel
            // yoksa devreye girer: migrasyonda bu havuz eşleşemeyen eski varyantların TÜM
            // renklerinin görsellerini içeriyor — görselsiz bir renge verilirse galeri her pozu
            // renk sayısı kadar tekrar gösteriyor. Boş dönmek daha iyi; UI görseli olan ilk
            // varyanta düşüyor.
            List<StoreVariantImageDto> variantImages;
            if (variantColorValue.TryGetValue(v.Id, out var colorId)
                && imgsByColor.TryGetValue(colorId, out var cImgs) && cImgs.Count > 0)
                variantImages = cImgs;
            else if (imgsByVariantId.TryGetValue(v.Id, out var ownImgs) && ownImgs.Count > 0)
                variantImages = ownImgs;
            else if (rawByVariantId.Count == 0)
                variantImages = productImages;
            else
                variantImages = [];

            // M3 (2026-09-09, mobil isteği): hazır seçenek metni — sepet/yorum uçlarıyla AYNI
            // kuraldan (VaryantSecenekMetni): iç filtre ekseni dışarıda, sıra renk → beden.
            var variantInfo = ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur(attrs.Select(a =>
                (a.AttributeTypeCode,
                 TrDeger(a.AttributeTypeNameI18n, a.AttributeTypeCode),
                 TrDeger(a.AttributeValueNameI18n, ""))));

            return new StoreVariantDto(
                v.Id, v.Sku, v.BasePrice, channelPrice?.Price, channelPrice?.CompareAtPrice,
                v.IsActive, variantImages, attrs,
                stockByVariant.GetValueOrDefault(v.Id, 0),
                variantInfo);
        }).ToList();

        // Ürün seviyesi özellikler (cinsiyet, kumaş türü vb.) — detay sayfası "Öne Çıkan
        // Özellikler" bölümü için. Çoklu değer olabilir (aynı tipte birden çok satır).
        var productAttrs = await db.ProductAttributes.AsNoTracking()
            .Where(a => a.ProductId == product.Id && a.AttributeValueId != null)
            .Select(a => new StoreProductAttributeDto(
                a.AttributeType.Code, a.AttributeType.NameI18n, a.AttributeValue!.NameI18n))
            .ToListAsync(ct);

        var groupName = await db.ProductGroups.AsNoTracking()
            .Where(g => g.Id == product.ProductGroupId)
            .Select(g => g.NameI18n)
            .FirstOrDefaultAsync(ct);

        // M3 (mobil isteği): özellik tablosuna "Kategori Grubu" ve "Stok Durumu" satırları.
        // Bilgi yanıtta zaten vardı (productGroupNameI18n / variants[].stockQty) ama istemcinin
        // ayrı ayrı yorumlaması gerekiyordu; tabloya hazır satır olarak da eklenir.
        if (groupName is { Count: > 0 })
            productAttrs.Add(new StoreProductAttributeDto(
                "kategori_grubu",
                new Dictionary<string, string> { ["tr"] = "Kategori Grubu", ["en"] = "Category Group" },
                groupName));

        // Stok satırı hiç yoksa (stok takibi tutulmayan katalog) "Tükendi" yazmak yanlış olur —
        // bu durumda ürünün satış durumu esas alınır.
        var stokVar = product.IsSaleOpen
                      && (stockByVariant.Count == 0 || stockByVariant.Values.Any(q => q > 0));
        productAttrs.Add(new StoreProductAttributeDto(
            "stok_durumu",
            new Dictionary<string, string> { ["tr"] = "Stok Durumu", ["en"] = "Availability" },
            stokVar
                ? new Dictionary<string, string> { ["tr"] = "Stokta", ["en"] = "In stock" }
                : new Dictionary<string, string> { ["tr"] = "Tükendi", ["en"] = "Out of stock" }));

        // H5: aktif videolar — efektif URL: VideoUrl (K15 birincil) ?? video CDN tabanı +
        // FileName (taban ayarı yoksa dosya kayıtları atlanır; galeri video slaytı bundan beslenir).
        var videoBase = await CdnHelper.BuildVideoBaseAsync(db, ct);
        var videos = (await db.ProductVideos.AsNoTracking()
            .Where(v => v.ProductId == product.Id
                        && v.Status == ECSPros.Catalog.Domain.Entities.ProductImageStatus.Active)
            .OrderBy(v => v.SortOrder)
            .Select(v => new { v.VideoUrl, v.FileName, v.ThumbnailUrl })
            .ToListAsync(ct))
            .Select(v => new
            {
                Url = v.VideoUrl ?? (videoBase != null && v.FileName != "" ? videoBase + "/" + v.FileName : null),
                v.ThumbnailUrl
            })
            .Where(v => v.Url != null)
            .Select(v => new StoreProductVideoDto(v.Url!, v.ThumbnailUrl))
            .ToList();

        // A10: ürün seviyesi fiyat özeti — ★ 2026-09-11: KART kuralıyla aynı (KartFiyatGorunumu.KartTabanFiyati:
        // varyant efektif fiyatlarının EN YÜKSEĞİ; eskiden en düşük pozitifti → liste/detay ayrışması). `minPrice`
        // alan adı mobil sözleşme gereği korunur (B9: "satış fiyatı"). Varyant satırları kendi efektif fiyatını taşır.
        var enDusukPozitif = variants
            .SelectMany(v => new[] { v.PlatformPrice ?? 0m, v.BasePrice })
            .Where(f => f > 0).DefaultIfEmpty(product.BasePrice).Min();
        var minPrice = ECSPros.Shared.Contracts.KartFiyatGorunumu.KartTabanFiyati(variants.Select(v => (v.PlatformPrice ?? 0m, v.BasePrice)));
        if (minPrice <= 0) minPrice = enDusukPozitif;
        // Kanonik slug için temsilci varyant: gösterilen fiyatı taşıyan ilk varyant (yoksa ilk varyant).
        var fiyatliVaryant = variants.FirstOrDefault(v => (v.PlatformPrice is > 0 ? v.PlatformPrice.Value : v.BasePrice) == minPrice)
            ?? variants.FirstOrDefault();
        // ★ M3 (2026-09-09): çizili fiyat artık LİSTE ile AYNI kuralla bulunur — ürünün aktif
        // varyantlarındaki EN YÜKSEK çizili fiyat (satış fiyatından büyükse). Eskiden yalnız
        // "en ucuz varyantın" çizili fiyatına bakılıyordu; o varyantta çizili fiyat yoksa detay
        // indirimi HİÇ göstermiyor (P-00020538, P-00021410), başka varyant seçilince de listeden
        // FARKLI değer gösteriyordu (P-00021624: liste 799,99 ↔ detay 599,99).
        var enYuksekCizili = variants
            .Select(v => v.CompareAtPrice ?? 0m).Where(c => c > 0).DefaultIfEmpty(0m).Max();
        decimal? compareAt = enYuksekCizili > minPrice ? enYuksekCizili : null;

        string? kampanyaAdi = null; decimal? kampanyaFiyat = null;
        List<ECSPros.Shared.Contracts.CampaignBadge>? kampanyaRozetleri = null;
        try
        {
            // B9 (2026-09-08): detay da listeyle AYNI rozet bandını döner (tüm kapsayan kampanyalar); fiyat yalnız kazanandan.
            var kmpListe = (await campaignResolver.ResolveAllForProductsAsync(request.FirmPlatformId, [product.Id], ct))
                .GetValueOrDefault(product.Id);
            if (kmpListe is { Count: > 0 })
            {
                var kazanan = kmpListe[0];
                kampanyaAdi = kazanan.BadgeLabel ?? kazanan.Name;
                kampanyaFiyat = CampaignPricing.EffectivePrice(kazanan, minPrice);
                kampanyaRozetleri = kmpListe.Select(k => new ECSPros.Shared.Contracts.CampaignBadge(
                    k.BadgeLabel ?? k.Name, ECSPros.Shared.Contracts.CampaignBadgePalette.Resolve(k.BadgeColor))).ToList();
            }
        }
        catch { /* kampanya çözülemedi — kampanyasız detay */ }

        // B9: tek fiyat sözleşmesi (kural KartFiyatGorunumu'nda; Razor detayı kendi hesabını yapar, etkilenmez)
        var (satisFiyati, ciziliFiyat) = ECSPros.Shared.Contracts.KartFiyatGorunumu.Hesapla(minPrice, compareAt, kampanyaFiyat);

        // MK4 (kullanıcı kararı 2026-09-09): varyantın KENDİ çizili fiyatı yoksa ÜRÜN düzeyindeki
        // referans kopyalanır — mobil her varyantta indirim oranını hesaplayabilsin. Yalnız o
        // varyantın satış fiyatından büyükse yazılır (aksi hâlde "indirim" yanlış görünürdü).
        if (ciziliFiyat is { } urunCizili)
        {
            for (var i = 0; i < variants.Count; i++)
            {
                if (variants[i].CompareAtPrice is > 0) continue;
                var varyantSatis = variants[i].PlatformPrice ?? variants[i].BasePrice;
                if (varyantSatis > 0 && urunCizili > varyantSatis)
                    variants[i] = variants[i] with { CompareAtPrice = urunCizili };
            }
        }

        // A5/A10: kanal slug'ları (varyant → slug); kanonik slug = fiyatlı/ilk varyantın slug'ı.
        Dictionary<Guid, string>? variantSlugs = null; string? slug = null;
        try
        {
            var slugSonuc = await mediator.Send(new ECSPros.Storefront.Application.Queries.GetChannelVariantSlugs
                .GetChannelVariantSlugsQuery(request.FirmPlatformId, variants.Select(v => v.Id).ToList()), ct);
            if (slugSonuc.IsSuccess && slugSonuc.Value is { Count: > 0 } vs)
            {
                variantSlugs = vs;
                slug = (fiyatliVaryant is not null && vs.TryGetValue(fiyatliVaryant.Id, out var s1)) ? s1
                     : vs.Values.FirstOrDefault();
            }
        }
        catch { /* slug isteğe bağlı */ }

        return Result.Success(new StoreProductDetailDto(
            product.Id, product.Code, product.NameI18n, product.ShortDescriptionI18n,
            product.IsSaleOpen, variants,
            product.DescriptionI18n, productAttrs, groupName,
            videos.Count > 0 ? videos : null,
            minPrice, ciziliFiyat, kampanyaFiyat, kampanyaAdi,
            product.ProductGroupId, slug, variantSlugs,
            Price: satisFiyati, CampaignBadges: kampanyaRozetleri));
    }
}
