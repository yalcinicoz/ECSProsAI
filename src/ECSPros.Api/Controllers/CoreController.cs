using ECSPros.Api.Authorization;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Application.Commands.CreateCargoRule;
using ECSPros.Core.Application.Commands.CreateExpenseType;
using ECSPros.Core.Application.Commands.CreateFirm;
using ECSPros.Core.Application.Commands.CreateFirmPlatformIntegration;
using ECSPros.Core.Application.Commands.CreateIntegrationService;
using ECSPros.Core.Application.Commands.ManageCargoBarcodeRange;
using ECSPros.Core.Application.Commands.UpdateIntegrationService;
using ECSPros.Core.Application.Commands.CreateFirmPlatform;
using ECSPros.Core.Application.Commands.CreatePlatformType;
using ECSPros.Core.Application.Commands.UpdateFirm;
using ECSPros.Core.Application.Commands.UpdateFirmPlatformIntegration;
using ECSPros.Core.Application.Commands.UpdateFirmPlatform;
using ECSPros.Core.Application.Commands.UpdatePlatformType;
using ECSPros.Core.Application.Commands.UpsertUiTranslations;
using ECSPros.Core.Application.Queries.GetUiTranslations;
using ECSPros.Core.Application.Queries.GetCargoRules;
using ECSPros.Core.Application.Queries.GetExpenseTypes;
using ECSPros.Core.Application.Queries.GetFirmDetail;
using ECSPros.Core.Application.Queries.GetFirmPlatformIntegrations;
using ECSPros.Core.Application.Queries.GetFirmPlatforms;
using ECSPros.Core.Application.Queries.GetFirms;
using ECSPros.Core.Application.Queries.GetIntegrationServices;
using ECSPros.Core.Application.Queries.GetLanguages;
using ECSPros.Core.Application.Queries.GetOrderStatuses;
using ECSPros.Core.Application.Queries.GetPaymentMethods;
using ECSPros.Core.Application.Queries.GetPlatformTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/core")]
[Authorize]
public class CoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoreController(IMediator mediator) => _mediator = mediator;

    // ── Diller ────────────────────────────────────────────────────────────────

    /// <summary>Aktif dilleri listeler.</summary>
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLanguagesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    // ── Referans Veriler ───────────────────────────────────────────────────────

    /// <summary>Sipariş durumlarını listeler.</summary>
    [HttpGet("order-statuses")]
    public async Task<IActionResult> GetOrderStatuses([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrderStatusesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ödeme yöntemlerini listeler.</summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPaymentMethodsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Platform tiplerini listeler (trendyol, hepsiburada, site vb.).</summary>
    [HttpGet("platform-types")]
    public async Task<IActionResult> GetPlatformTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPlatformTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni platform tipi oluşturur.</summary>
    [HttpPost("platform-types")]
    public async Task<IActionResult> CreatePlatformType([FromBody] CreatePlatformTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreatePlatformTypeCommand(request.Code, request.NameI18n, request.IsMarketplace, request.SettingsSchema), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Platform tipini günceller.</summary>
    [HttpPut("platform-types/{id:guid}")]
    public async Task<IActionResult> UpdatePlatformType(Guid id, [FromBody] UpdatePlatformTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdatePlatformTypeCommand(id, request.NameI18n, request.IsMarketplace, request.IsActive, request.SettingsSchema), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Entegrasyon servislerini listeler.</summary>
    [HttpGet("integration-services")]
    public async Task<IActionResult> GetIntegrationServices([FromQuery] string? serviceType = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetIntegrationServicesQuery(serviceType), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Servis kataloğuna yeni servis tanımı ekler — yalnız platform yönetimi
    /// (definition şeması: geliştirici firma doldurur, kullanıcı firma yalnız okur).</summary>
    [HttpPost("integration-services")]
    [RequirePermission(Permissions.DefinitionManage)]
    public async Task<IActionResult> CreateIntegrationService([FromBody] CreateIntegrationServiceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateIntegrationServiceCommand(request.Code, request.NameI18n, request.ServiceType,
                request.IsAvailable, request.LogoUrl, request.TrackingUrlTemplate, request.SettingsSchema,
                request.CargoCodeStrategy, request.CargoCodeMinLength,
                request.CargoCodeMaxLength, request.CargoCodeCharset), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Servis tanımını günceller (kod ve tip değiştirilemez) — yalnız platform yönetimi.</summary>
    [HttpPut("integration-services/{id:guid}")]
    [RequirePermission(Permissions.DefinitionManage)]
    public async Task<IActionResult> UpdateIntegrationService(Guid id, [FromBody] UpdateIntegrationServiceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateIntegrationServiceCommand(id, request.NameI18n, request.IsAvailable,
                request.LogoUrl, request.TrackingUrlTemplate, request.SettingsSchema,
                request.CargoCodeStrategy, request.CargoCodeMinLength,
                request.CargoCodeMaxLength, request.CargoCodeCharset), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ── Kargo Barkod Aralıkları (F3 — range stratejisi, örn. PTT) ──────────────

    /// <summary>Kargo barkod aralıklarını listeler (doluluk bilgisiyle).</summary>
    [HttpGet("cargo-barcode-ranges")]
    public async Task<IActionResult> GetCargoBarcodeRanges(
        [FromQuery] Guid? firmPlatformIntegrationId = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCargoBarcodeRangesQuery(firmPlatformIntegrationId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kargo entegrasyonuna tahsisli barkod aralığı tanımlar.</summary>
    [HttpPost("cargo-barcode-ranges")]
    public async Task<IActionResult> CreateCargoBarcodeRange(
        [FromBody] CreateCargoBarcodeRangeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCargoBarcodeRangeCommand(
            request.FirmPlatformIntegrationId, request.RangeStart, request.RangeEnd), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Aralığı aktif/pasif yapar — sınırlar ve sayaç değiştirilemez
    /// (tahsis edilen barkod havuza geri dönmez).</summary>
    [HttpPut("cargo-barcode-ranges/{id:guid}/active")]
    public async Task<IActionResult> SetCargoBarcodeRangeActive(
        Guid id, [FromBody] SetCargoBarcodeRangeActiveRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetCargoBarcodeRangeActiveCommand(id, request.IsActive), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }


    /// <summary>Masraf tiplerini listeler.</summary>
    [HttpGet("expense-types")]
    public async Task<IActionResult> GetExpenseTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetExpenseTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni masraf tipi oluşturur.</summary>
    [HttpPost("expense-types")]
    public async Task<IActionResult> CreateExpenseType([FromBody] CreateExpenseTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateExpenseTypeCommand(request.Code, request.NameI18n, request.IsItemLevel, request.DefaultTaxRate, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    // ── Firmalar ───────────────────────────────────────────────────────────────

    /// <summary>Firma listesini döner.</summary>
    [HttpGet("firms")]
    public async Task<IActionResult> GetFirms([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFirmsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firma detayını döner (platformlar + entegrasyonlar dahil).</summary>
    [HttpGet("firms/{id:guid}")]
    public async Task<IActionResult> GetFirmDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFirmDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni firma oluşturur.</summary>
    [HttpPost("firms")]
    public async Task<IActionResult> CreateFirm([FromBody] CreateFirmRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateFirmCommand(request.Code, request.NameI18n, request.TaxOffice, request.TaxNumber,
                request.Address, request.Phone, request.Email, request.IsMain), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Firma bilgilerini günceller.</summary>
    [HttpPut("firms/{id:guid}")]
    public async Task<IActionResult> UpdateFirm(Guid id, [FromBody] UpdateFirmRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateFirmCommand(id, request.NameI18n, request.TaxOffice, request.TaxNumber,
                request.Address, request.Phone, request.Email, request.IsMain, request.IsActive), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ── Firma Platformları ─────────────────────────────────────────────────────

    /// <summary>Firmaya ait platformları listeler.</summary>
    [HttpGet("firms/{firmId:guid}/platforms")]
    public async Task<IActionResult> GetFirmPlatforms(Guid firmId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFirmPlatformsQuery(firmId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firmaya yeni platform ekler.</summary>
    [HttpPost("firms/{firmId:guid}/platforms")]
    public async Task<IActionResult> CreateFirmPlatform(Guid firmId, [FromBody] CreateFirmPlatformRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateFirmPlatformCommand(firmId, request.PlatformTypeId, request.Code, request.NameI18n,
                request.PriceType, request.PriceMultiplier,
                request.Credentials ?? new(), request.Settings ?? new()), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Firma platformunu günceller.</summary>
    [HttpPut("firm-platforms/{id:guid}")]
    public async Task<IActionResult> UpdateFirmPlatform(Guid id, [FromBody] UpdateFirmPlatformRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateFirmPlatformCommand(id, request.NameI18n, request.PriceType, request.PriceMultiplier,
                request.Credentials ?? new(), request.Settings ?? new(), request.IsActive), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ── Firma-Platform Entegrasyonları ─────────────────────────────────────────
    // FirmPlatformId null → firma geneli; dolu → yalnız o platform. Credentials
    // yanıtlarda maskeli döner, DB'de şifreli tutulur.

    /// <summary>Firmaya ait servis entegrasyonlarını listeler (credentials maskeli).</summary>
    [HttpGet("firms/{firmId:guid}/integrations")]
    public async Task<IActionResult> GetFirmPlatformIntegrations(Guid firmId, [FromQuery] string? serviceType = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFirmPlatformIntegrationsQuery(firmId, serviceType), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firmaya yeni servis entegrasyonu ekler.</summary>
    [HttpPost("firms/{firmId:guid}/integrations")]
    public async Task<IActionResult> CreateFirmPlatformIntegration(Guid firmId, [FromBody] CreateFirmPlatformIntegrationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateFirmPlatformIntegrationCommand(firmId, request.IntegrationServiceId, request.Name,
                request.Credentials ?? new(), request.Settings ?? new(), request.FirmPlatformId,
                request.StartDate, request.EndDate, request.Status ?? "draft", request.Terms), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Servis entegrasyonunu günceller (maskeli credential alanları korunur).</summary>
    [HttpPut("firm-integrations/{id:guid}")]
    public async Task<IActionResult> UpdateFirmPlatformIntegration(Guid id, [FromBody] UpdateFirmPlatformIntegrationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateFirmPlatformIntegrationCommand(id, request.Name, request.Credentials ?? new(), request.Settings ?? new(),
                request.IsActive, request.FirmPlatformId, request.StartDate, request.EndDate,
                request.Status ?? "draft", request.Terms), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ── Kargo Kuralları ────────────────────────────────────────────────────────

    /// <summary>Firmaya ait kargo kurallarını listeler.</summary>
    [HttpGet("firms/{firmId:guid}/cargo-rules")]
    public async Task<IActionResult> GetCargoRules(Guid firmId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCargoRulesQuery(firmId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Mahalle-kargo atama ekranı (2026-07-22): bir kapsamın (firma geneli
    /// "default" / tek mahalle "neighborhood") kural listesini komple değiştirir.</summary>
    [HttpPut("firms/{firmId:guid}/cargo-rules")]
    public async Task<IActionResult> UpsertCargoRules(Guid firmId, [FromBody] UpsertCargoRulesRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ECSPros.Core.Application.Commands.UpsertCargoRules.UpsertCargoRulesCommand(
            firmId, request.RuleType, request.NeighborhoodId,
            (request.Items ?? []).Select(i => new ECSPros.Core.Application.Commands.UpsertCargoRules.UpsertCargoRuleItem(
                i.FirmPlatformIntegrationId, i.Priority)).ToList()), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { count = result.Value } });
    }

    /// <summary>Firmaya yeni kargo kuralı ekler.</summary>
    [HttpPost("firms/{firmId:guid}/cargo-rules")]
    public async Task<IActionResult> CreateCargoRule(Guid firmId, [FromBody] CreateCargoRuleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateCargoRuleCommand(firmId, request.FirmPlatformIntegrationId, request.RuleType,
                request.PaymentType, request.NeighborhoodId, request.CityId, request.Priority), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    // ── UI Çevirileri ──────────────────────────────────────────────────────────

    /// <summary>Statik metin çevirilerini listeler.</summary>
    [HttpGet("ui-translations")]
    public async Task<IActionResult> GetUiTranslations(
        [FromQuery] string? @namespace,
        [FromQuery] string? lang,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetUiTranslationsQuery(@namespace, lang), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Çevirileri toplu ekler veya günceller.</summary>
    [HttpPut("ui-translations/batch")]
    public async Task<IActionResult> UpsertUiTranslations(
        [FromBody] UpsertUiTranslationsRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new UiTranslationItem(i.Namespace, i.Key, i.Lang, i.Value))
            .ToList();
        var result = await _mediator.Send(new UpsertUiTranslationsCommand(items), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { changed = result.Value } });
    }

    // ── O1 (2026-08-04): Bildirim şablonları + sipariş onay politikası ──────

    /// <summary>Tip koduna göre bildirim şablonları (panel Bildirim Şablonları ekranı).</summary>
    [HttpGet("notification-templates")]
    public async Task<IActionResult> GetNotificationTemplates([FromQuery] string typeCode = "siparis_onay", CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Queries.GetNotificationTemplates.GetNotificationTemplatesQuery(typeCode), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Bildirim şablonu ekle/güncelle — (tip, kanal, dil) başına tek kayıt.</summary>
    [HttpPut("notification-templates")]
    public async Task<IActionResult> UpsertNotificationTemplate([FromBody] UpsertNotificationTemplateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Commands.UpsertNotificationTemplate.UpsertNotificationTemplateCommand(
                request.TypeCode, request.Channel, request.LanguageCode ?? "tr",
                request.Subject, request.Body, request.IsActive), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Ürün kartı görünüm ayarları — platform Settings'e yalnız "productCard" anahtarı merge edilir.</summary>
    [HttpPut("firm-platforms/{id:guid}/product-card-settings")]
    public async Task<IActionResult> UpdateProductCardSettings(Guid id, [FromBody] ProductCardSettingsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Commands.UpdateProductCardSettings.UpdateProductCardSettingsCommand(
                id, request.VideoBadge, request.SponsorBadge, request.ColorBadge, request.GalleryDots,
                request.FavoriteButton, request.CollectionButton, request.Rating,
                request.DiscountRow, request.CampaignPriceRow, request.Areas, request.CartButton,
                request.HoverEffect, request.SimilarButton), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Ürün listesi sıralama seçenekleri — platform Settings'e yalnız "productList" anahtarı merge edilir.</summary>
    [HttpPut("firm-platforms/{id:guid}/product-list-settings")]
    public async Task<IActionResult> UpdateProductListSettings(Guid id, [FromBody] ProductListSettingsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Commands.UpdateProductListSettings.UpdateProductListSettingsCommand(
                id, request.SortOptions ?? new()), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Site navigasyon ayarları (mega menü hover) — platform Settings'e yalnız "navigation" anahtarı merge edilir.</summary>
    [HttpPut("firm-platforms/{id:guid}/navigation-settings")]
    public async Task<IActionResult> UpdateNavigationSettings(Guid id, [FromBody] NavigationSettingsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Commands.UpdateNavigationSettings.UpdateNavigationSettingsCommand(
                id, request.MegaMenuHover), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Takip/çerez kanal ayarları (İE-1, 2026-08-22) — platform Settings'e yalnız "tracking"
    /// anahtarı merge edilir. purchaseAt: confirmed|created. Consent banner/varsayılan sabittir
    /// (EU kararı: banner açık, default deny) — istekte alınmaz.</summary>
    [HttpPut("firm-platforms/{id:guid}/tracking-settings")]
    public async Task<IActionResult> UpdateTrackingSettings(Guid id, [FromBody] TrackingSettingsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Commands.UpdateTrackingSettings.UpdateTrackingSettingsCommand(
                id, request.PurchaseAt ?? "confirmed", request.BannerTitle, request.BannerText, request.PolicyUrl, request.PolicyLabel), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Sipariş onay politikası — platform Settings'e yalnız ilgili anahtarlar merge edilir.</summary>
    [HttpPut("firm-platforms/{id:guid}/order-confirm-settings")]
    public async Task<IActionResult> UpdateOrderConfirmSettings(Guid id, [FromBody] OrderConfirmSettingsRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ECSPros.Core.Application.Commands.UpdateOrderConfirmSettings.UpdateOrderConfirmSettingsCommand(
                id, request.Cod, request.Card, request.LinkHours), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record UpsertNotificationTemplateRequest(
    string TypeCode, string Channel, string? LanguageCode, string? Subject, string Body, bool IsActive = true);

public record OrderConfirmSettingsRequest(string Cod, string Card, int LinkHours);

public record ProductListSettingsRequest(Dictionary<string, bool>? SortOptions);

public record NavigationSettingsRequest(bool MegaMenuHover = false);
public record TrackingSettingsRequest(string? PurchaseAt = "confirmed", string? BannerTitle = null, string? BannerText = null, string? PolicyUrl = null, string? PolicyLabel = null);

public record ProductCardSettingsRequest(
    bool VideoBadge = true,
    bool SponsorBadge = true,
    bool ColorBadge = true,
    bool GalleryDots = true,
    bool FavoriteButton = true,
    bool CollectionButton = true,
    bool Rating = true,
    bool DiscountRow = true,
    bool CampaignPriceRow = true,
    Dictionary<string, ECSPros.Core.Application.Commands.UpdateProductCardSettings.ProductCardAreaSetting>? Areas = null,
    bool CartButton = true,
    string? HoverEffect = null,
    bool SimilarButton = true);

// ── Request Modelleri ──────────────────────────────────────────────────────────

public record CreateFirmRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain
);

public record UpdateFirmRequest(
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    bool IsActive
);

public record CreateFirmPlatformRequest(
    Guid PlatformTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    Dictionary<string, object>? Credentials = null,
    Dictionary<string, object>? Settings = null
);

public record UpdateFirmPlatformRequest(
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    bool IsActive,
    Dictionary<string, object>? Credentials = null,
    Dictionary<string, object>? Settings = null
);

public record CreateFirmPlatformIntegrationRequest(
    Guid IntegrationServiceId,
    string? Name,
    Dictionary<string, object>? Credentials,
    Dictionary<string, object>? Settings,
    Guid? FirmPlatformId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Status = null,
    Dictionary<string, object>? Terms = null
);

public record UpdateFirmPlatformIntegrationRequest(
    string? Name,
    Dictionary<string, object>? Credentials,
    Dictionary<string, object>? Settings,
    bool IsActive,
    Guid? FirmPlatformId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Status = null,
    Dictionary<string, object>? Terms = null
);

public record CreateIntegrationServiceRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string ServiceType,
    bool IsAvailable = true,
    string? LogoUrl = null,
    string? TrackingUrlTemplate = null,
    List<PlatformSchemaField>? SettingsSchema = null,
    string? CargoCodeStrategy = null,
    int? CargoCodeMinLength = null,
    int? CargoCodeMaxLength = null,
    string? CargoCodeCharset = null
);

public record UpdateIntegrationServiceRequest(
    Dictionary<string, string> NameI18n,
    bool IsAvailable,
    string? LogoUrl = null,
    string? TrackingUrlTemplate = null,
    List<PlatformSchemaField>? SettingsSchema = null,
    string? CargoCodeStrategy = null,
    int? CargoCodeMinLength = null,
    int? CargoCodeMaxLength = null,
    string? CargoCodeCharset = null
);

public record CreateCargoBarcodeRangeRequest(
    Guid FirmPlatformIntegrationId,
    long RangeStart,
    long RangeEnd);

public record SetCargoBarcodeRangeActiveRequest(bool IsActive);

public record CreateCargoRuleRequest(
    Guid FirmPlatformIntegrationId,
    string RuleType,
    string? PaymentType,
    Guid? NeighborhoodId,
    Guid? CityId,
    int Priority
);

public record UpsertCargoRulesRequest(
    string RuleType,               // default | neighborhood
    Guid? NeighborhoodId,
    List<UpsertCargoRuleItemRequest>? Items
);

public record UpsertCargoRuleItemRequest(Guid FirmPlatformIntegrationId, int Priority);

public record CreatePlatformTypeRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsMarketplace,
    List<PlatformSchemaField>? SettingsSchema = null
);

public record UpdatePlatformTypeRequest(
    Dictionary<string, string> NameI18n,
    bool IsMarketplace,
    bool IsActive,
    List<PlatformSchemaField>? SettingsSchema = null
);

public record CreateExpenseTypeRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsItemLevel,
    decimal DefaultTaxRate,
    int SortOrder
);

public record UpsertUiTranslationsRequest(List<UiTranslationItemRequest> Items);
public record UiTranslationItemRequest(string Namespace, string Key, string Lang, string Value);
