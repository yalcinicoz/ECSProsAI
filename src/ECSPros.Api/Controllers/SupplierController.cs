using ECSPros.Accounts.Application.Queries.GetCurrentAccountDetail;
using ECSPros.Catalog.Application.Queries.GetSupplierPanelProductDetail;
using ECSPros.Catalog.Application.Queries.GetSupplierPanelProducts;
using ECSPros.Iam.Application.Queries.GetSupplierUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
public class SupplierController(IMediator mediator) : ControllerBase
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
}
