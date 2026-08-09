using ECSPros.Order.Application.Queries.GetInvoicePrint;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// OP2 (2026-08-09): yazdırma sayfaları — masa tabletindeki gizli iframe bu sayfaları açıp
/// otomatik window.print() çağırır (Chrome --kiosk-printing ile diyalogsuz basar, K-3).
/// Sayfalar GUID bilinmeden erişilemez; iframe JWT taşıyamadığından [AllowAnonymous]
/// (iç ağ kullanımı; noindex). Panel yazdırma butonları da aynı sayfaları kullanabilir.
/// </summary>
[Route("yazdir")]
[AllowAnonymous]
public sealed class YazdirController(IMediator mediator,
    ECSPros.Fulfillment.Application.Services.IFulfillmentDbContext fulDb) : Controller
{
    [HttpGet("fatura/{invoiceId:guid}")]
    public async Task<IActionResult> Fatura(Guid invoiceId, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetInvoicePrintQuery(invoiceId), ct);
        if (sonuc.IsFailure) return NotFound(sonuc.Error);
        var dto = sonuc.Value!;

        // Paket faturası: yalnız paketin kalemleri basılır (K-17 çoklu paket/fatura)
        if (dto.PackageId is { } pid)
        {
            var paketKalemIdleri = await fulDb.PackageItems.AsNoTracking()
                .Where(pi => pi.PackageId == pid)
                .Select(pi => pi.OrderItemId)
                .ToListAsync(ct);
            if (paketKalemIdleri.Count > 0)
                dto = dto with { Items = dto.Items.Where(i => paketKalemIdleri.Contains(i.OrderItemId)).ToList() };
        }
        return View("~/Views/Yazdir/Fatura.cshtml", dto);
    }

    [HttpGet("paket-etiket/{packageId:guid}")]
    public async Task<IActionResult> PaketEtiket(Guid packageId,
        [FromServices] ECSPros.Fulfillment.Application.Services.IOrderPackagingReader orderReader,
        CancellationToken ct)
    {
        var paket = await fulDb.Packages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId, ct);
        if (paket is null) return NotFound("Paket bulunamadı.");
        ViewData["Siparis"] = await orderReader.GetOrderAsync(paket.OrderId, ct);
        return View("~/Views/Yazdir/PaketEtiket.cshtml", paket);
    }
}
