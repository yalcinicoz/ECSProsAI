using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Queries.GetCollectionByShareCode;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// H10: public koleksiyon sayfası — /koleksiyon/{shareCode} (anonim SSR).
/// Hesabım'daki "Paylaş" butonunun kopyaladığı link buraya düşer; vitrin collection
/// kartındaki "Koleksiyonu Aç" da buraya bağlanır. Yalnız onaylı + paylaşıma açık
/// koleksiyon görünür (query kapısı); aksi 404. Kartlar liste sayfasıyla aynı
/// (_UrunKarti + UrunKartMap) — silinen/satışa kapalı ürünler kendiliğinden düşer.
/// </summary>
public class KoleksiyonController(IMediator mediator, IStoreContext storeContext) : StorePageController
{
    [HttpGet("/koleksiyon/{shareCode}")]
    public async Task<IActionResult> Index(string shareCode, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null) return NotFound();

        var sonuc = await mediator.Send(new GetCollectionByShareCodeQuery(platform.Id, shareCode), ct);
        if (sonuc.IsFailure) return NotFound();
        var koleksiyon = sonuc.Value!;

        var kartlar = new List<UrunKartVm>();
        if (koleksiyon.ItemCodes.Count > 0)
        {
            var urunler = await mediator.Send(new GetStoreProductsQuery(
                platform.Id, ProductCodes: koleksiyon.ItemCodes, PageSize: koleksiyon.ItemCodes.Count), ct);
            if (urunler.IsSuccess)
            {
                var kartMap = urunler.Value!.Items.ToDictionary(p => p.Code, UrunKartMap.KartaCevir);
                // Koleksiyondaki ekleme sırası korunur
                kartlar = koleksiyon.ItemCodes
                    .Where(kartMap.ContainsKey)
                    .Select(kod => kartMap[kod])
                    .ToList();
            }
        }

        ViewData["Title"] = $"{koleksiyon.Name} — Koleksiyon";
        ViewData["MetaDescription"] = koleksiyon.Description
            ?? $"{koleksiyon.Name} koleksiyonundaki {kartlar.Count} ürünü inceleyin.";
        ViewData["MsKoleksiyonVm"] = new PublicKoleksiyonVm(
            koleksiyon.Name, koleksiyon.Description, kartlar.Count,
            koleksiyon.ViewCount, shareCode.Trim(), kartlar);

        return View("~/Views/Koleksiyon/Index.cshtml");
    }
}

/// <summary>Public koleksiyon sayfası görünüm modeli (H10).</summary>
public sealed record PublicKoleksiyonVm(
    string Ad,
    string? Aciklama,
    int UrunSayisi,
    int Goruntulenme,
    string ShareCode,
    IReadOnlyList<UrunKartVm> Kartlar);
