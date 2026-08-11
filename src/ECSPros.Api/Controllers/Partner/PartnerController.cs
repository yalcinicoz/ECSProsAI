using System.Text.Json;
using ECSPros.Api.Authorization;
using ECSPros.Catalog.Application.Commands.SubmitPartnerProduct;
using ECSPros.Catalog.Application.Commands.UpdateSupplierProductPrices;
using ECSPros.Catalog.Application.Queries.GetPartnerGroupSchema;
using ECSPros.Catalog.Application.Queries.GetPartnerSubmissions;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using ECSPros.Catalog.Application.Queries.GetSupplierProductVariants;
using ECSPros.Crm.Application.Services;
using ECSPros.Fulfillment.Application.Commands.EnsureSupplierPackage;
using ECSPros.Fulfillment.Application.Commands.SetPackageShipment;
using ECSPros.Fulfillment.Application.Queries.GetSupplierPackages;
using ECSPros.Inventory.Application.Commands.UpsertSupplierStock;
using ECSPros.Order.Application.Commands.CreateSupplierShipment;
using ECSPros.Order.Application.Commands.TryMarkOrderShipped;
using ECSPros.Order.Application.Queries.GetSupplierOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers.Partner;

/// <summary>
/// Partner API façade (§0/§3.5) — /api/partner/v1. Kaba taneli, görev odaklı; YALNIZ API hesabı
/// token'ıyla (type=api_client) + scope ile erişilir. İç uçlardan (admin panel/storefront) ayrıdır
/// ve ayrı bir swagger dokümanında yayınlanır.
/// </summary>
[ApiController]
[Route("api/partner/v1")]
public class PartnerController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICrmDbContext _crmDb;

    public PartnerController(IMediator mediator, ICrmDbContext crmDb)
    {
        _mediator = mediator;
        _crmDb = crmDb;
    }

    /// <summary>Token introspection — çağıran API hesabının kimliği ve etkin scope'ları.
    /// Entegratör token'ının hangi yetkilerle geldiğini doğrulamak için.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "ApiClientOnly")]
    public IActionResult Me()
    {
        var data = new
        {
            clientId = User.FindFirst("client_id")?.Value,
            name = User.FindFirst("name")?.Value,
            ownerType = User.FindFirst("owner_type")?.Value,
            ownerId = User.FindFirst("owner_id")?.Value,
            scopes = User.FindAll("scope").Select(c => c.Value).OrderBy(s => s).ToList()
        };
        return Ok(new { success = true, data });
    }

    /// <summary>Keşif: ürün grupları + varyant eksenleri + ürün-seviyesi izinli özellikler.
    /// Gönderilecek ürün paketinde geçerli `group` kodunu ve eksen/özellik kodlarını verir (§3.6).</summary>
    [HttpGet("groups")]
    [RequireScope("catalog.read")]
    public async Task<IActionResult> Groups(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductGroupsQuery(ActiveOnly: true), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        var data = result.Value.Select(g => new
        {
            code = g.Code,
            name = g.NameI18n,
            variantAxes = g.Attributes.Where(a => a.IsVariant)
                .OrderBy(a => a.SortOrder)
                .Select(a => new { code = a.AttributeTypeCode, name = a.AttributeTypeNameI18n, primary = a.IsPrimaryAxis, required = a.IsRequired })
                .ToList(),
            attributes = g.Attributes.Where(a => !a.IsVariant)
                .OrderBy(a => a.SortOrder)
                .Select(a => new { code = a.AttributeTypeCode, name = a.AttributeTypeNameI18n, required = a.IsRequired })
                .ToList()
        }).ToList();

        return Ok(new { success = true, data });
    }

    /// <summary>Keşif: bir grubun eksen/özellikleri + her biri için izin verilen DEĞER HAVUZU.
    /// Gönderilecek pakette bu değerler kullanılmalı (Kapı 1 buna göre doğrular, §3.8).</summary>
    [HttpGet("groups/{code}")]
    [RequireScope("catalog.read")]
    public async Task<IActionResult> GroupSchema(string code, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPartnerGroupSchemaQuery(code), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün kartını tek pakette gönder (§3.8). Kapı 1 doğrulaması geçerse `pending`
    /// gönderim oluşur (onaya düşer); patlarsa gerekçe listesiyle 422 döner.</summary>
    [HttpPost("products")]
    [RequireScope("catalog.write")]
    public async Task<IActionResult> SubmitProduct([FromBody] PartnerProductBody body, CancellationToken ct)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var canSetPrice = HasScope("pricing.write");
        // API geneli camelCase — saklanan gövde de camelCase (panel/detay ucu tutarlı tüketsin).
        var rawJson = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = await _mediator.Send(
            new SubmitPartnerProductCommand(supplierId, GetApiClientId(), canSetPrice, body, rawJson), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        var r = result.Value;
        if (!r.Accepted)
            return UnprocessableEntity(new { success = false, errors = r.Errors });

        return Ok(new { success = true, data = new { submissionId = r.SubmissionId, supplierProductCode = r.SupplierProductCode, status = r.Status } });
    }

    /// <summary>Owner-scoped: çağıran tedarikçinin ürün gönderimleri + durumları.</summary>
    [HttpGet("products")]
    [RequireScope("catalog.read")]
    public async Task<IActionResult> MyProducts([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var result = await _mediator.Send(new GetPartnerSubmissionsQuery(supplierId, status, page, pageSize), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Onaylı ürünün stoğunu MUTLAK olarak bildir (§3.6 — onaya düşmez, direkt uygulanır).
    /// Stok "Tedarikçi Stokları" deposunda bu tedarikçinin kısmına yazılır; online mevcudiyete sayılır.</summary>
    [HttpPut("products/{code}/stock")]
    [RequireScope("stock.write")]
    public async Task<IActionResult> UpdateStock(string code, [FromBody] StockUpdateRequest request, CancellationToken ct)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        if (request.Items is null || request.Items.Count == 0)
            return UnprocessableEntity(new { success = false, errors = new[] { new { field = "items", code = "required", message = "En az bir stok kalemi gereklidir." } } });

        // 1) Ürünü + varyantlarını çöz (owner-scoped, canlı olmalı)
        var resolve = await _mediator.Send(new GetSupplierProductVariantsQuery(supplierId, code), ct);
        if (resolve.IsFailure)
            return NotFound(new { success = false, error = resolve.Error });

        var bySku = resolve.Value.Variants.ToDictionary(v => v.Sku, v => v.VariantId);

        // 2) İstek sku'larını variantId'ye eşle; bilinmeyen sku → 422
        var errors = new List<object>();
        var items = new List<SupplierStockItem>();
        foreach (var it in request.Items)
        {
            if (string.IsNullOrWhiteSpace(it.Sku) || !bySku.TryGetValue(it.Sku, out var vid))
            { errors.Add(new { field = $"items.{it.Sku}", code = "unknown_sku", message = $"'{it.Sku}' bu ürüne ait bir SKU değil." }); continue; }
            if (it.Quantity < 0)
            { errors.Add(new { field = $"items.{it.Sku}", code = "invalid_quantity", message = "Miktar negatif olamaz." }); continue; }
            items.Add(new SupplierStockItem(vid, it.Quantity));
        }
        if (errors.Count > 0)
            return UnprocessableEntity(new { success = false, errors });

        // 3) Inventory'ye yaz (tedarikçi kısmı)
        var upsert = await _mediator.Send(new UpsertSupplierStockCommand(supplierId, items), ct);
        if (upsert.IsFailure)
            return BadRequest(new { success = false, error = upsert.Error });

        return Ok(new { success = true, data = new { productCode = resolve.Value.ProductCode, updated = upsert.Value } });
    }

    /// <summary>P1a (2026-08-11): fiyat güncelleme — pricing.write (yalnız supplier_merchant
    /// tipinde vardır), onay KAPISIZ. Kalemler SKU ile; tümü-ya-da-hiçbiri uygulanır.
    /// Listelerde ~10 dk önbellek TTL'i vardır — fiyat siteye en geç o sürede yansır.</summary>
    [HttpPut("products/{code}/prices")]
    [RequireScope("pricing.write")]
    public async Task<IActionResult> UpdatePrices(string code, [FromBody] PriceUpdateRequest request, CancellationToken ct)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        if (request.Items is null || request.Items.Count == 0)
            return UnprocessableEntity(new { success = false, errors = new[] { new { field = "items", code = "required", message = "En az bir fiyat kalemi gereklidir." } } });

        var result = await _mediator.Send(new UpdateSupplierProductPricesCommand(
            supplierId, code,
            request.Items.Select(i => new SupplierPriceItem(i.Sku, i.Price)).ToList()), ct);

        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });

        if (result.Value.HasErrors)
            return UnprocessableEntity(new { success = false, errors = result.Value.Errors.Select(e => new { field = e.Field, code = e.Code, message = e.Message }) });

        return Ok(new { success = true, data = new { productCode = result.Value.ProductCode, updated = result.Value.Updated } });
    }

    /// <summary>P1b (2026-08-11): satıcıya düşen siparişler — sayfalı; `since` (ISO-8601, UTC)
    /// son değişiklik zamanına göre artımlı çekim (K6 v1 polling), `status` sipariş durumu
    /// (confirmed/processing/shipped/delivered/cancelled). Müşteriden yalnız ad-soyad + teslimat
    /// adresi paylaşılır; kalemler yalnız çağıran satıcınındır. Paketler operasyonda oluşur —
    /// henüz paketlenmemiş siparişte `packages` boş döner.</summary>
    [HttpGet("orders")]
    [RequireScope("order.read")]
    public async Task<IActionResult> MyOrders([FromQuery] DateTime? since, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var result = await _mediator.Send(new GetSupplierOrdersQuery(
            supplierId, since?.ToUniversalTime(), status, page, pageSize), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        var sayfa = result.Value;
        var zengin = await SiparisleriZenginlestir(supplierId, sayfa.Items.ToList(), ct);
        return Ok(new { success = true, data = new { items = zengin, totalCount = sayfa.TotalCount, page = sayfa.Page, pageSize = sayfa.PageSize } });
    }

    /// <summary>P1b: tek siparişin satıcı görünümü — sipariş numarasıyla.</summary>
    [HttpGet("orders/{orderNumber}")]
    [RequireScope("order.read")]
    public async Task<IActionResult> MyOrder(string orderNumber, CancellationToken ct)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var result = await _mediator.Send(new GetSupplierOrderDetailQuery(supplierId, orderNumber), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });

        var zengin = await SiparisleriZenginlestir(supplierId, [result.Value], ct);
        return Ok(new { success = true, data = zengin[0] });
    }

    /// <summary>P2 (2026-08-11): "kargoladım" bildirimi — satıcı KENDİ taşıyıcısıyla gönderdi
    /// (K3 mod 2; fulfillment.write yalnız FulfillmentMode=supplier hesaplarda vardır).
    /// Satıcının kalemleri için paket yoksa kanal serisinden oluşturulur; takip no 'external'
    /// kaynaklı kargo kodu olarak pakete yazılır; Shipment kaydı açılır (taşıyıcıya bizden
    /// istek GİTMEZ). Sipariş, tüm kalemleri kargolandığında 'shipped' olur (karma siparişte
    /// bizim kalemler beklenir). Paket başına TEK bildirim kabul edilir.</summary>
    [HttpPost("orders/{orderNumber}/shipment")]
    [RequireScope("fulfillment.write")]
    public async Task<IActionResult> ReportShipment(string orderNumber, [FromBody] ShipmentReportRequest request, CancellationToken ct)
    {
        if (!TryGetOwnerId(out var supplierId))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, error = "Bu API hesabı bir tedarikçiye bağlı değil (owner yok)." });

        var errors = new List<object>();
        if (string.IsNullOrWhiteSpace(request.CarrierName))
            errors.Add(new { field = "carrierName", code = "required", message = "Taşıyıcı adı gereklidir." });
        if (string.IsNullOrWhiteSpace(request.TrackingNumber))
            errors.Add(new { field = "trackingNumber", code = "required", message = "Takip numarası gereklidir." });
        if (errors.Count > 0)
            return UnprocessableEntity(new { success = false, errors });

        // 1) Sahiplik + durum: satıcının siparişi mi (OrderId'yi de buradan alırız)
        var siparis = await _mediator.Send(new GetSupplierOrderDetailQuery(supplierId, orderNumber), ct);
        if (siparis.IsFailure)
            return NotFound(new { success = false, error = siparis.Error });

        // 2) Paket garanti (idempotent — kanal serisinden numara)
        var paket = await _mediator.Send(new EnsureSupplierPackageCommand(
            siparis.Value.OrderId, supplierId, GetApiClientId() ?? Guid.Empty), ct);
        if (paket.IsFailure)
            return BadRequest(new { success = false, error = paket.Error });

        // 3) Shipment kaydı (paket başına tek bildirim; durum/sahiplik yeniden doğrulanır)
        var gonderi = await _mediator.Send(new CreateSupplierShipmentCommand(
            supplierId, siparis.Value.OrderId, paket.Value.PackageId, paket.Value.PackageNumber,
            request.CarrierName.Trim(), request.TrackingNumber.Trim(), request.TrackingUrl,
            GetApiClientId() ?? Guid.Empty), ct);
        if (gonderi.IsFailure)
            return Conflict(new { success = false, error = gonderi.Error });

        // 4) Paket ↔ shipment bağı + dış kargo kodu
        await _mediator.Send(new SetPackageShipmentCommand(
            paket.Value.PackageId, gonderi.Value.ShipmentId, request.TrackingNumber.Trim()), ct);

        // 5) Siparişin tamamı kargolandıysa 'shipped' (stok düşümü OrderShippedEvent'te)
        var tamami = await _mediator.Send(new TryMarkOrderShippedCommand(
            siparis.Value.OrderId, GetApiClientId() ?? Guid.Empty), ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                packageNumber = gonderi.Value.PackageNumber,
                shipmentNumber = gonderi.Value.ShipmentNumber,
                trackingNumber = gonderi.Value.TrackingNumber,
                orderFullyShipped = tamami.IsSuccess && tamami.Value
            }
        });
    }

    /// <summary>Kompozisyon: şehir/ilçe adları (CRM geo) + satıcının paketleri (Fulfillment)
    /// sipariş görünümüne iliştirilir — modüller arası birleştirme host'ta yapılır.</summary>
    private async Task<List<object>> SiparisleriZenginlestir(Guid supplierId, List<SupplierOrderDto> siparisler, CancellationToken ct)
    {
        if (siparisler.Count == 0) return [];

        var sehirIdler = siparisler.Select(s => s.Shipping.CityId)
            .Concat(siparisler.Select(s => s.Shipping.DistrictId)).Distinct().ToList();
        var sehirler = await _crmDb.Cities.AsNoTracking()
            .Where(c => sehirIdler.Contains(c.Id)).Select(c => new { c.Id, c.NameI18n }).ToListAsync(ct);
        var ilceler = await _crmDb.Districts.AsNoTracking()
            .Where(d => sehirIdler.Contains(d.Id)).Select(d => new { d.Id, d.NameI18n }).ToListAsync(ct);
        static string? Ad(Dictionary<string, string>? i18n) =>
            i18n is null ? null : (i18n.TryGetValue("tr", out var tr) ? tr : i18n.Values.FirstOrDefault());
        var sehirAd = sehirler.ToDictionary(x => x.Id, x => Ad(x.NameI18n));
        var ilceAd = ilceler.ToDictionary(x => x.Id, x => Ad(x.NameI18n));

        var paketSonuc = await _mediator.Send(new GetSupplierPackagesQuery(
            supplierId, siparisler.Select(s => s.OrderId).Distinct().ToList()), ct);
        var paketByOrder = (paketSonuc.IsSuccess ? paketSonuc.Value : [])
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

    private bool TryGetOwnerId(out Guid ownerId)
        => Guid.TryParse(User.FindFirst("owner_id")?.Value, out ownerId);

    private Guid? GetApiClientId()
        => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

    private bool HasScope(string scope)
        => User.FindAll("scope").Any(c => c.Value == scope);
}

public record StockUpdateRequest(List<StockUpdateItem> Items);
public record StockUpdateItem(string Sku, int Quantity);
public record PriceUpdateRequest(List<PriceUpdateItem> Items);
public record PriceUpdateItem(string Sku, decimal Price);
public record ShipmentReportRequest(string CarrierName, string TrackingNumber, string? TrackingUrl);
