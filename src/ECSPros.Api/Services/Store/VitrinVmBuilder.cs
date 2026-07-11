using System.Text.Json;
using ECSPros.Api.Models.Store;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Queries.GetShowcaseCollections;
using MediatR;

namespace ECSPros.Api.Services.Store;

public interface IVitrinVmBuilder
{
    Task<List<VitrinBlokVm>> KurAsync(
        Guid firmPlatformId, IReadOnlyList<ResolvedBlockDto> blocks, CancellationToken ct = default);
}

/// <summary>
/// G5: çözülmüş blokları Razor görünüm modeline çevirir. Config alanlarını açar
/// (tema/seeAllUrl/endsAt/mobileCarousel/gorunum/images/productCount/frames),
/// ürünleri UrunKartMap'le karta indirir, koleksiyon kartlarını kapak görselleri
/// (kolaj kodları → canlı kart verisi) ve maskeli üye adıyla (E7 deseni) zenginleştirir.
/// </summary>
public class VitrinVmBuilder(IMediator mediator, IMemberService memberService) : IVitrinVmBuilder
{
    public async Task<List<VitrinBlokVm>> KurAsync(
        Guid firmPlatformId, IReadOnlyList<ResolvedBlockDto> blocks, CancellationToken ct = default)
    {
        var sonuc = new List<VitrinBlokVm>();
        foreach (var blok in blocks)
        {
            using var config = ConfigAc(blok.Config);
            var kok = config?.RootElement;

            sonuc.Add(new VitrinBlokVm(
                blok.Id, blok.BlockType, blok.Template,
                TrAd(blok.Title), TrAdVeyaNull(blok.Subtitle),
                Metin(kok, "tema"),
                Metin(kok, "seeAllUrl"),
                Tarih(kok, "endsAt"),
                Bayrak(kok, "mobileCarousel"),
                Metin(kok, "gorunum"),
                blok.Items.Select(OgeKur).ToList(),
                (blok.Products ?? []).Select(UrunKartMap.KartaCevir).ToList(),
                blok.Collections is { Count: > 0 }
                    ? await KoleksiyonKartlariKurAsync(firmPlatformId, blok.Collections, ct)
                    : []));
        }
        return sonuc;
    }

    private static VitrinBlokOgeVm OgeKur(ResolvedItemDto oge)
    {
        using var config = ConfigAc(oge.Config);
        var kok = config?.RootElement;
        return new VitrinBlokOgeVm(
            oge.Id, TrAd(oge.Title), TrAdVeyaNull(oge.Subtitle),
            oge.ImageUrl, oge.MobileImageUrl, oge.VideoUrl, oge.LinkUrl, oge.OpenInNewTab,
            oge.ButtonText is null ? null : TrAd(oge.ButtonText),
            oge.BadgeLabel,
            (oge.Products ?? []).Select(UrunKartMap.KartaCevir).ToList(),
            MetinListesi(kok, "images"),
            Metin(kok, "productCount"),
            HamJson(kok, "frames"));
    }

    private async Task<List<VitrinKoleksiyonKartVm>> KoleksiyonKartlariKurAsync(
        Guid firmPlatformId, IReadOnlyList<ShowcaseCollectionDto> koleksiyonlar, CancellationToken ct)
    {
        // Kapak kolajı: tüm koleksiyonların önizleme kodları tek kart sorgusuyla çözülür
        var kodlar = koleksiyonlar.SelectMany(k => k.PreviewProductCodes).Distinct().ToList();
        var kartlar = new Dictionary<string, UrunKartVm>();
        if (kodlar.Count > 0)
        {
            var kartSonucu = await mediator.Send(new GetStoreProductsQuery(
                firmPlatformId, PageSize: kodlar.Count, ProductCodes: kodlar), ct);
            if (kartSonucu.IsSuccess)
                foreach (var dto in kartSonucu.Value!.Items)
                    kartlar[dto.Code] = UrunKartMap.KartaCevir(dto);
        }

        var uyeAdlari = new Dictionary<Guid, string>();
        foreach (var uyeId in koleksiyonlar.Select(k => k.MemberId).Distinct())
        {
            var uye = await memberService.GetMemberAsync(uyeId, ct);
            uyeAdlari[uyeId] = uye?.FullName ?? "Üye";
        }

        return koleksiyonlar.Select(k =>
        {
            var kapak = k.PreviewProductCodes
                .Where(kod => kartlar.TryGetValue(kod, out var kart) && kart.GorselUrl is not null)
                .Take(3)
                .Select(kod => new KoleksiyonKapakVm(kartlar[kod].GorselUrl!, kartlar[kod].Ad, kartlar[kod].Url))
                .ToList();
            var ad = uyeAdlari[k.MemberId];
            return new VitrinKoleksiyonKartVm(
                k.Name, k.Description, AdiMaskele(ad), BasHarfler(ad),
                k.ItemCount, k.ViewCount, kapak,
                Math.Max(0, k.ItemCount - kapak.Count), k.ShareCode);
        }).ToList();
    }

    // ── yardımcılar ──
    private static JsonDocument? ConfigAc(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
    }

    private static string? Metin(JsonElement? kok, string ad) =>
        kok?.ValueKind == JsonValueKind.Object && kok.Value.TryGetProperty(ad, out var el)
            ? el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                _ => null,
            }
            : null;

    private static bool Bayrak(JsonElement? kok, string ad) =>
        kok?.ValueKind == JsonValueKind.Object && kok.Value.TryGetProperty(ad, out var el)
        && el.ValueKind == JsonValueKind.True;

    private static DateTime? Tarih(JsonElement? kok, string ad) =>
        Metin(kok, ad) is { } metin && DateTime.TryParse(metin, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t : null;

    private static List<string> MetinListesi(JsonElement? kok, string ad) =>
        kok?.ValueKind == JsonValueKind.Object && kok.Value.TryGetProperty(ad, out var el)
        && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!).ToList()
            : [];

    private static string? HamJson(JsonElement? kok, string ad) =>
        kok?.ValueKind == JsonValueKind.Object && kok.Value.TryGetProperty(ad, out var el)
        && el.ValueKind == JsonValueKind.Array
            ? el.GetRawText() : null;

    private static string TrAd(Dictionary<string, string> i18n) => UrunKartMap.TrAd(i18n);
    private static string? TrAdVeyaNull(Dictionary<string, string>? i18n) =>
        i18n is null ? null : (UrunKartMap.TrAd(i18n) is { Length: > 0 } ad ? ad : null);

    /// <summary>"Ayşe Yılmaz" → "Ayşe Y***" (E7 maskeli ad deseni).</summary>
    private static string AdiMaskele(string tamAd)
    {
        var parcalar = tamAd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parcalar.Length < 2) return tamAd;
        return parcalar[0] + " " + parcalar[^1][0] + "***";
    }

    private static string BasHarfler(string tamAd)
    {
        var parcalar = tamAd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parcalar.Length switch
        {
            0 => "MS",
            1 => parcalar[0][..1].ToUpperInvariant(),
            _ => (parcalar[0][..1] + parcalar[^1][..1]).ToUpperInvariant(),
        };
    }
}
