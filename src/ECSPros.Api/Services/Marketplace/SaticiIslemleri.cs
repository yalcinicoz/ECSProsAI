using ECSPros.Crm.Application.Services;
using ECSPros.Fulfillment.Application.Commands.EnsureSupplierPackage;
using ECSPros.Fulfillment.Application.Commands.SetPackageShipment;
using ECSPros.Fulfillment.Application.Queries.GetSupplierPackages;
using ECSPros.Order.Application.Commands.CreateSupplierShipment;
using ECSPros.Order.Application.Commands.TryMarkOrderShipped;
using ECSPros.Order.Application.Queries.GetSupplierOrders;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace;

/// <summary>
/// Satıcı sipariş/kargo işlemlerinin ORTAK kompozisyonu (2026-08-11) — partner API
/// (makine) ve satıcı paneli (insan) aynı komut zincirini kullanır; iki yüzey arasında
/// davranış farkı OLUŞAMAZ (kullanıcı şartı). Şehir/ilçe adları CRM'den, paketler
/// Fulfillment'tan iliştirilir; kargo bildirimi paket-garanti → shipment → bağ → sipariş
/// geneli kargolama adımlarını tek yerden yürütür.
/// </summary>
public sealed class SaticiIslemleri(IMediator mediator, ICrmDbContext crmDb)
{
    public sealed record KargoBildirimi(string CarrierName, string TrackingNumber, string? TrackingUrl);
    public sealed record KargoSonucu(string PackageNumber, string ShipmentNumber, string TrackingNumber, bool OrderFullyShipped);

    public async Task<List<object>> SiparisleriZenginlestirAsync(
        Guid supplierId, List<SupplierOrderDto> siparisler, CancellationToken ct)
    {
        if (siparisler.Count == 0) return [];

        var geoIdler = siparisler.Select(s => s.Shipping.CityId)
            .Concat(siparisler.Select(s => s.Shipping.DistrictId)).Distinct().ToList();
        var sehirler = await crmDb.Cities.AsNoTracking()
            .Where(c => geoIdler.Contains(c.Id)).Select(c => new { c.Id, c.NameI18n }).ToListAsync(ct);
        var ilceler = await crmDb.Districts.AsNoTracking()
            .Where(d => geoIdler.Contains(d.Id)).Select(d => new { d.Id, d.NameI18n }).ToListAsync(ct);
        static string? Ad(Dictionary<string, string>? i18n) =>
            i18n is null ? null : (i18n.TryGetValue("tr", out var tr) ? tr : i18n.Values.FirstOrDefault());
        var sehirAd = sehirler.ToDictionary(x => x.Id, x => Ad(x.NameI18n));
        var ilceAd = ilceler.ToDictionary(x => x.Id, x => Ad(x.NameI18n));

        var paketSonuc = await mediator.Send(new GetSupplierPackagesQuery(
            supplierId, siparisler.Select(s => s.OrderId).Distinct().ToList()), ct);
        var paketByOrder = (paketSonuc.IsSuccess ? paketSonuc.Value : new List<SupplierPackageDto>())
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return siparisler.Select(s =>
        {
            s.Shipping.CityName = sehirAd.GetValueOrDefault(s.Shipping.CityId);
            s.Shipping.DistrictName = ilceAd.GetValueOrDefault(s.Shipping.DistrictId);
            var paketler = paketByOrder.GetValueOrDefault(s.OrderId) ?? [];
            return (object)new
            {
                s.OrderNumber, s.Status, s.PaymentStatus, s.CurrencyCode, s.CreatedAt, s.UpdatedAt,
                shipping = s.Shipping,
                items = s.Items,
                packages = paketler.Select(p => new { p.PackageNumber, p.Status, p.PackedAt, items = p.Items })
            };
        }).ToList();
    }

    /// <summary>"Kargoladım" bildirimi — P2 zinciri: sahiplik/durum doğrulaması → paket
    /// garanti → shipment (paket başına TEK) → paket bağı + dış kargo kodu → sipariş geneli
    /// kargolama denemesi. Hata metinleri Result üzerinden aynen yüzeye taşınır.</summary>
    public async Task<Result<KargoSonucu>> KargoBildirAsync(
        Guid supplierId, string orderNumber, KargoBildirimi bildirim, Guid actorId, CancellationToken ct)
    {
        var siparis = await mediator.Send(new GetSupplierOrderDetailQuery(supplierId, orderNumber), ct);
        if (siparis.IsFailure) return Result.Failure<KargoSonucu>(siparis.Error!);

        var paket = await mediator.Send(new EnsureSupplierPackageCommand(
            siparis.Value.OrderId, supplierId, actorId), ct);
        if (paket.IsFailure) return Result.Failure<KargoSonucu>(paket.Error!);

        var gonderi = await mediator.Send(new CreateSupplierShipmentCommand(
            supplierId, siparis.Value.OrderId, paket.Value.PackageId, paket.Value.PackageNumber,
            bildirim.CarrierName.Trim(), bildirim.TrackingNumber.Trim(), bildirim.TrackingUrl,
            actorId), ct);
        if (gonderi.IsFailure) return Result.Failure<KargoSonucu>(gonderi.Error!);

        await mediator.Send(new SetPackageShipmentCommand(
            paket.Value.PackageId, gonderi.Value.ShipmentId, bildirim.TrackingNumber.Trim()), ct);

        var tamami = await mediator.Send(new TryMarkOrderShippedCommand(siparis.Value.OrderId, actorId), ct);

        return Result.Success(new KargoSonucu(
            gonderi.Value.PackageNumber, gonderi.Value.ShipmentNumber,
            gonderi.Value.TrackingNumber, tamami.IsSuccess && tamami.Value));
    }
}
