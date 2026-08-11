using System.Text.Json;
using ECSPros.Accounts.Application.Queries.GetCurrentAccountDetail;
using ECSPros.Accounts.Application.Queries.GetSupplierSettlements;
using ECSPros.Accounts.Application.Services;
using ECSPros.Api.Services.Marketplace;
using ECSPros.Catalog.Application.Commands.SubmitPartnerProduct;
using ECSPros.Catalog.Application.Commands.UpdateSupplierProductPrices;
using ECSPros.Catalog.Application.Queries.GetPartnerGroupSchema;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using ECSPros.Catalog.Application.Queries.GetSupplierPanelProductDetail;
using ECSPros.Catalog.Application.Queries.GetSupplierPanelProducts;
using ECSPros.Catalog.Application.Queries.GetSupplierProductVariants;
using ECSPros.Fulfillment.Application.Commands.EnsureSupplierPackage;
using ECSPros.Fulfillment.Application.Commands.SetSupplierPackageInvoice;
using ECSPros.Iam.Application.Queries.GetSupplierUser;
using ECSPros.Iam.Application.Services;
using ECSPros.Inventory.Application.Commands.UpsertSupplierStock;
using ECSPros.Order.Application.Queries.GetSupplierOrders;
using ECSPros.Promotion.Application.Commands.SupplierCampaignParticipation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Satıcı paneli (satici/) ince-taneli veri yüzeyi — /api/supplier/*.
/// Yalnız SupplierUser (type=supplier_user) erişir; her uç owner-scope'tur
/// (token'daki owner_id = kendi cari kartı, başka carinin verisi asla dönmez).
/// Kimlik uçları SupplierAuthController'da (/api/supplier/auth/*).
/// </summary>
[ApiController]
[Route("api/supplier")]
[Authorize(Policy = "SupplierOnly")]
public class SupplierController(
    IMediator mediator,
    SaticiIslemleri saticiIslemleri,
    IAccountsDbContext accountsDb,
    IIamDbContext iamDb) : ControllerBase
{
    private Guid? OwnerId()
        => Guid.TryParse(User.FindFirst("owner_id")?.Value, out var id) ? id : null;

    private Guid? SupplierUserId()
        => Guid.TryParse(User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>
    /// Panel introspection — giriş yapan kullanıcı + bağlı cari kartın özeti.
    /// Panel açılışında çağrılır (S2); ekranlar bu bilgiyle kurulur.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = SupplierUserId();
        var ownerId = OwnerId();
        if (userId is null || ownerId is null)
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var userResult = await mediator.Send(new GetSupplierUserQuery(userId.Value), ct);
        if (userResult.IsFailure)
            return BadRequest(new { success = false, error = userResult.Error });
        var u = userResult.Value!;

        // Owner-scope güvencesi: token'daki owner_id ile kullanıcının kayıtlı carisi eşleşmeli.
        if (u.CurrentAccountId != ownerId.Value)
            return Forbid();

        var accResult = await mediator.Send(new GetCurrentAccountDetailQuery(ownerId.Value), ct);
        if (accResult.IsFailure)
            return BadRequest(new { success = false, error = accResult.Error });
        var a = accResult.Value!;

        return Ok(new
        {
            success = true,
            data = new
            {
                user = new { u.Id, u.Email, u.FullName, u.LastLoginAt },
                account = new
                {
                    a.Id,
                    a.Code,
                    a.Title,
                    a.SupplierKind,
                    a.Currency,
                    a.IsActive,
                    a.ContactName,
                    a.Email,
                    a.Phone
                }
            }
        });
    }

    /// <summary>
    /// Ürünlerim — birleşik liste (canlı ürün + gönderim, durum rozetli). S3a-1.
    /// status: live | pending | rejected | live_pending (revizyon bekleyen canlılar)
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> Products(
        [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await mediator.Send(
            new GetSupplierPanelProductsQuery(ownerId.Value, status, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ürün detayı — canlı ürün (varyantlarıyla) + gönderim geçmişi (red notları). S3a-1.</summary>
    [HttpGet("products/{supplierProductCode}")]
    public async Task<IActionResult> ProductDetail(string supplierProductCode, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await mediator.Send(
            new GetSupplierPanelProductDetailQuery(ownerId.Value, supplierProductCode), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Satıcı paneli tamamlama (2026-08-11): AŞAĞIDAKİ TÜM UÇLAR PARTNER API İLE
    // AYNI KOMUTLARI kullanır — panel ile API arasında davranış/kural farkı OLUŞAMAZ
    // (kullanıcı şartı). Panelde scope yerine sözleşme/owner kuralları geçerlidir.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Ürün ekleme formu için grup keşfi (partner GET /groups ile aynı).</summary>
    [HttpGet("catalog/groups")]
    public async Task<IActionResult> CatalogGroups(CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductGroupsQuery(ActiveOnly: true), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        var data = result.Value.Select(g => new
        {
            code = g.Code,
            name = g.NameI18n,
            variantAxes = g.Attributes.Where(a => a.IsVariant).OrderBy(a => a.SortOrder)
                .Select(a => new { code = a.AttributeTypeCode, name = a.AttributeTypeNameI18n, primary = a.IsPrimaryAxis, required = a.IsRequired }).ToList(),
            attributes = g.Attributes.Where(a => !a.IsVariant).OrderBy(a => a.SortOrder)
                .Select(a => new { code = a.AttributeTypeCode, name = a.AttributeTypeNameI18n, required = a.IsRequired }).ToList()
        }).ToList();
        return Ok(new { success = true, data });
    }

    /// <summary>Grubun eksen/özellik ŞEMASI + izinli değer havuzu (form buradan kurulur).</summary>
    [HttpGet("catalog/groups/{code}")]
    public async Task<IActionResult> CatalogGroupSchema(string code, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPartnerGroupSchemaQuery(code), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni ürün / revizyon gönderimi — partner POST /products ile AYNI Kapı-1 doğrulaması
    /// ve onay akışı (panelden de onay atlanmaz). Fiyat yazımı serbesttir (pazaryeri satıcısı).</summary>
    [HttpPost("products")]
    public async Task<IActionResult> SubmitProduct([FromBody] PartnerProductBody body, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var rawJson = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var result = await mediator.Send(
            new SubmitPartnerProductCommand(ownerId.Value, null, CanSetPrice: true, body, rawJson), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        if (!result.Value.Accepted)
            return UnprocessableEntity(new { success = false, errors = result.Value.Errors });
        return Ok(new { success = true, data = new { submissionId = result.Value.SubmissionId, supplierProductCode = result.Value.SupplierProductCode, status = result.Value.Status } });
    }

    /// <summary>Fiyat güncelleme — partner PUT /products/{code}/prices ile aynı komut (onay kapısız).</summary>
    [HttpPut("products/{supplierProductCode}/prices")]
    public async Task<IActionResult> UpdatePrices(string supplierProductCode, [FromBody] SupplierPriceRequest request, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        if (request.Items is null || request.Items.Count == 0)
            return UnprocessableEntity(new { success = false, errors = new[] { new { field = "items", code = "required", message = "En az bir fiyat kalemi gereklidir." } } });

        var result = await mediator.Send(new UpdateSupplierProductPricesCommand(
            ownerId.Value, supplierProductCode,
            request.Items.Select(i => new SupplierPriceItem(i.Sku, i.Price)).ToList()), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        if (result.Value.HasErrors)
            return UnprocessableEntity(new { success = false, errors = result.Value.Errors.Select(e => new { field = e.Field, code = e.Code, message = e.Message }) });
        return Ok(new { success = true, data = new { productCode = result.Value.ProductCode, updated = result.Value.Updated } });
    }

    /// <summary>Stok güncelleme — partner PUT /products/{code}/stock ile aynı akış (mutlak, onaysız).</summary>
    [HttpPut("products/{supplierProductCode}/stock")]
    public async Task<IActionResult> UpdateStock(string supplierProductCode, [FromBody] SupplierStockRequest request, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        if (request.Items is null || request.Items.Count == 0)
            return UnprocessableEntity(new { success = false, errors = new[] { new { field = "items", code = "required", message = "En az bir stok kalemi gereklidir." } } });

        var resolve = await mediator.Send(new GetSupplierProductVariantsQuery(ownerId.Value, supplierProductCode), ct);
        if (resolve.IsFailure) return NotFound(new { success = false, error = resolve.Error });
        var bySku = resolve.Value.Variants.ToDictionary(v => v.Sku, v => v.VariantId);

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
        if (errors.Count > 0) return UnprocessableEntity(new { success = false, errors });

        var upsert = await mediator.Send(new UpsertSupplierStockCommand(ownerId.Value, items), ct);
        if (upsert.IsFailure) return BadRequest(new { success = false, error = upsert.Error });
        return Ok(new { success = true, data = new { productCode = resolve.Value.ProductCode, updated = upsert.Value } });
    }

    /// <summary>Siparişlerim — partner GET /orders ile aynı görünüm (K2 alan kısıtları dahil).</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] DateTime? since, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await mediator.Send(new GetSupplierOrdersQuery(
            ownerId.Value, since?.ToUniversalTime(), status, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        var zengin = await saticiIslemleri.SiparisleriZenginlestirAsync(ownerId.Value, result.Value.Items.ToList(), ct);
        return Ok(new { success = true, data = new { items = zengin, totalCount = result.Value.TotalCount, page = result.Value.Page, pageSize = result.Value.PageSize } });
    }

    /// <summary>Sipariş detayı (satıcı görünümü) + paket fatura bilgileri.</summary>
    [HttpGet("orders/{orderNumber}")]
    public async Task<IActionResult> OrderDetail(string orderNumber, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await mediator.Send(new GetSupplierOrderDetailQuery(ownerId.Value, orderNumber), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        var zengin = await saticiIslemleri.SiparisleriZenginlestirAsync(ownerId.Value, [result.Value], ct);
        return Ok(new { success = true, data = zengin[0] });
    }

    /// <summary>"Kargoladım" — partner POST /orders/{no}/shipment ile AYNI zincir. Panelde scope
    /// yoktur; sözleşme kargo modu 'seller_ships' değilse reddedilir (K3 — gönderim bizdeyse
    /// satıcı takip no bildiremez).</summary>
    [HttpPost("orders/{orderNumber}/shipment")]
    public async Task<IActionResult> ReportShipment(string orderNumber, [FromBody] SupplierShipmentRequest request, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var errors = new List<object>();
        if (string.IsNullOrWhiteSpace(request.CarrierName))
            errors.Add(new { field = "carrierName", code = "required", message = "Taşıyıcı adı gereklidir." });
        if (string.IsNullOrWhiteSpace(request.TrackingNumber))
            errors.Add(new { field = "trackingNumber", code = "required", message = "Takip numarası gereklidir." });
        if (errors.Count > 0) return UnprocessableEntity(new { success = false, errors });

        var contract = await accountsDb.SupplierContracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CurrentAccountId == ownerId.Value && c.IsActive, ct);
        if (contract?.CargoMode != "seller_ships")
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                error = "Kargo modunuz 'satıcı gönderir' değil — gönderimler platform tarafından yapılır. Modu Hesabım sayfasından değiştirebilirsiniz."
            });

        var sonuc = await saticiIslemleri.KargoBildirAsync(ownerId.Value, orderNumber,
            new SaticiIslemleri.KargoBildirimi(request.CarrierName, request.TrackingNumber, request.TrackingUrl),
            SupplierUserId() ?? Guid.Empty, ct);
        if (sonuc.IsFailure) return Conflict(new { success = false, error = sonuc.Error });
        return Ok(new { success = true, data = sonuc.Value });
    }

    /// <summary>Paket için satıcı fatura bilgisi (no + isteğe bağlı görüntü linki). Paket yoksa
    /// kanal serisinden oluşturulur (kargodan önce fatura girilebilsin).</summary>
    [HttpPut("orders/{orderNumber}/invoice")]
    public async Task<IActionResult> SetInvoice(string orderNumber, [FromBody] SupplierInvoiceRequest request, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
            return UnprocessableEntity(new { success = false, errors = new[] { new { field = "invoiceNumber", code = "required", message = "Fatura numarası gereklidir." } } });

        var siparis = await mediator.Send(new GetSupplierOrderDetailQuery(ownerId.Value, orderNumber), ct);
        if (siparis.IsFailure) return NotFound(new { success = false, error = siparis.Error });

        var paket = await mediator.Send(new EnsureSupplierPackageCommand(
            siparis.Value.OrderId, ownerId.Value, SupplierUserId() ?? Guid.Empty), ct);
        if (paket.IsFailure) return BadRequest(new { success = false, error = paket.Error });

        var sonuc = await mediator.Send(new SetSupplierPackageInvoiceCommand(
            ownerId.Value, paket.Value.PackageId, request.InvoiceNumber.Trim(), request.InvoiceUrl), ct);
        if (sonuc.IsFailure) return BadRequest(new { success = false, error = sonuc.Error });
        return Ok(new { success = true, data = new { packageNumber = paket.Value.PackageNumber } });
    }

    /// <summary>Mali durum: hakediş satırları (katman izli) — partner GET /settlements ile aynı.</summary>
    [HttpGet("settlements")]
    public async Task<IActionResult> Settlements([FromQuery] string? status, [FromQuery] DateTime? since,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await mediator.Send(new GetSupplierSettlementsQuery(
            ownerId.Value, status, since?.ToUniversalTime(), page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Mali durum: hakediş bakiyesi + defter hareketleri.</summary>
    [HttpGet("account/statement")]
    public async Task<IActionResult> Statement([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await mediator.Send(new GetSupplierStatementQuery(ownerId.Value, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Bana açık kampanyalar + katılım durumum — partner GET /campaigns ile aynı.</summary>
    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await mediator.Send(new GetSupplierCampaignsQuery(ownerId.Value), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kampanyaya katıl (ürün seçimiyle) — partner POST /campaigns/{id}/join ile aynı.</summary>
    [HttpPost("campaigns/{id:guid}/join")]
    public async Task<IActionResult> JoinCampaign(Guid id, [FromBody] SupplierCampaignJoinRequest? request, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await mediator.Send(new JoinCampaignCommand(ownerId.Value, id, request?.ProductIds), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kampanya katılımını geri çek.</summary>
    [HttpDelete("campaigns/{id:guid}/join")]
    public async Task<IActionResult> LeaveCampaign(Guid id, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await mediator.Send(new LeaveCampaignCommand(ownerId.Value, id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Hesabım: sözleşme görünümü — kargo modu + hakediş koşulları (oranlar HARİÇ:
    /// oran yönetimi platformdadır, satıcı yalnız hakediş satırlarında uygulanan oranı görür).</summary>
    [HttpGet("account/settings")]
    public async Task<IActionResult> AccountSettings(CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var contract = await accountsDb.SupplierContracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CurrentAccountId == ownerId.Value, ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                cargoMode = contract?.CargoMode ?? "platform_contract",
                settlementDelayDays = contract?.SettlementDelayDays ?? 14,
                payoutPeriod = contract?.PayoutPeriod ?? "weekly",
                hasContract = contract is not null
            }
        });
    }

    /// <summary>Hesabım: kargo modu seçimi (K3 — satıcının kararı). Yalnız mod 1-2; mod 3
    /// (satıcı sözleşmesiyle biz göndeririz) taşıyıcı entegrasyonları tamamlanınca açılır.
    /// Sözleşmenin DİĞER alanlarına (oranlar, X, periyot) satıcı dokunamaz.</summary>
    [HttpPut("account/settings/cargo-mode")]
    public async Task<IActionResult> SetCargoMode([FromBody] SupplierCargoModeRequest request, CancellationToken ct)
    {
        var ownerId = OwnerId();
        if (ownerId is null) return Unauthorized(new { success = false, error = "Geçersiz token." });
        if (request.CargoMode is not ("platform_contract" or "seller_ships"))
            return BadRequest(new { success = false, error = "Geçersiz kargo modu. ('Satıcı sözleşmesiyle platform gönderir' modu kargo entegrasyonlarıyla birlikte açılacak.)" });

        var contract = await accountsDb.SupplierContracts
            .FirstOrDefaultAsync(c => c.CurrentAccountId == ownerId.Value, ct);
        if (contract is null)
        {
            contract = new ECSPros.Accounts.Domain.Entities.SupplierContract { CurrentAccountId = ownerId.Value };
            accountsDb.SupplierContracts.Add(contract);
        }
        contract.CargoMode = request.CargoMode;
        contract.UpdatedAt = DateTime.UtcNow;
        await accountsDb.SaveChangesAsync(ct);

        // K3 senkronu — API hesaplarının fulfillment yetkisi (CommissionController ile aynı kural)
        var hedefMod = request.CargoMode == "seller_ships" ? "supplier" : "platform";
        var apiClients = await iamDb.ApiClients
            .Where(c => c.OwnerType == "current_account" && c.OwnerId == ownerId.Value).ToListAsync(ct);
        foreach (var client in apiClients.Where(c => c.FulfillmentMode != hedefMod))
        { client.FulfillmentMode = hedefMod; client.UpdatedAt = DateTime.UtcNow; }
        if (apiClients.Count > 0) await iamDb.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }
}

public record SupplierPriceRequest(List<SupplierPriceRequestItem> Items);
public record SupplierPriceRequestItem(string Sku, decimal Price);
public record SupplierStockRequest(List<SupplierStockRequestItem> Items);
public record SupplierStockRequestItem(string Sku, int Quantity);
public record SupplierShipmentRequest(string CarrierName, string TrackingNumber, string? TrackingUrl);
public record SupplierInvoiceRequest(string InvoiceNumber, string? InvoiceUrl);
public record SupplierCampaignJoinRequest(List<Guid>? ProductIds);
public record SupplierCargoModeRequest(string CargoMode);
