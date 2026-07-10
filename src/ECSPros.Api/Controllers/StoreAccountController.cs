using ECSPros.Crm.Application.Commands.AddMemberAddress;
using ECSPros.Crm.Application.Commands.DeleteMemberAddress;
using ECSPros.Crm.Application.Commands.UpdateMemberProfile;
using ECSPros.Crm.Application.Queries.GetMemberAddresses;
using ECSPros.Crm.Application.Queries.GetMemberDetail;
using ECSPros.Crm.Application.Queries.GetMemberLoyalty;
using ECSPros.Crm.Application.Queries.GetMemberWallet;
using ECSPros.Order.Application.Queries.GetOrderDetail;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Queries.GetReturnDetail;
using ECSPros.Order.Application.Queries.GetReturns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/account")]
[Authorize(Policy = "MemberOnly")]
public class StoreAccountController(IMediator mediator) : ControllerBase
{
    private Guid GetMemberId() =>
        Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    // Profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberDetailQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateMemberProfileCommand(
            GetMemberId(), req.FirstName, req.LastName, req.Phone, req.Gender, req.BirthDate, req.CityId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E2: duyuru tercihleri (kampanya e-posta/SMS/telefon izinleri).</summary>
    [HttpPut("marketing-consents")]
    public async Task<IActionResult> UpdateMarketingConsents([FromBody] MarketingConsentsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.UpdateMemberMarketingConsents.UpdateMemberMarketingConsentsCommand(
                GetMemberId(), req.Email, req.Sms, req.Phone), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E2: Aktif Cihazlar + Giriş Geçmişi — üyenin son oturumları.</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Queries.GetMemberSessions.GetMemberSessionsQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Addresses
    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberAddressesQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddMemberAddressCommand req, CancellationToken ct)
    {
        var cmd = req with { MemberId = GetMemberId() };
        var result = await mediator.Send(cmd, ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { addressId = result.Value } });
    }

    /// <summary>E3: adres güncelleme (C4'te ertelenmişti).</summary>
    [HttpPut("addresses/{addressId}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] ECSPros.Crm.Application.Commands.UpdateMemberAddress.UpdateMemberAddressCommand req, CancellationToken ct)
    {
        var cmd = req with { MemberId = GetMemberId(), AddressId = addressId };
        var result = await mediator.Send(cmd, ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>E3: adresi varsayılan yap — önceki varsayılanlar düşer.</summary>
    [HttpPost("addresses/{addressId}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ECSPros.Crm.Application.Commands.SetDefaultMemberAddress.SetDefaultMemberAddressCommand(GetMemberId(), addressId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    [HttpDelete("addresses/{addressId}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteMemberAddressCommand(GetMemberId(), addressId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // C7 (K9): TCKN kaydı — format + kontrol basamağı algoritması sunucuda doğrulanır
    [HttpPost("identity")]
    public async Task<IActionResult> SetIdentity([FromBody] SetIdentityRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ECSPros.Crm.Application.Commands.SetMemberIdentity.SetMemberIdentityCommand(
            GetMemberId(), req.IdentityNumber, req.BirthDate), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // Orders
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetOrdersQuery(status, GetMemberId(), null, page, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Returns
    [HttpGet("returns")]
    public async Task<IActionResult> GetReturns([FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetReturnsQuery(null, GetMemberId(), status, page, 20), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("returns/{returnId}")]
    public async Task<IActionResult> GetReturn(Guid returnId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetReturnDetailQuery(returnId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Wallet
    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberWalletQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // Loyalty
    [HttpGet("loyalty")]
    public async Task<IActionResult> GetLoyalty(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberLoyaltyQuery(GetMemberId()), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    Guid? CityId = null);   // E2: yaşadığı şehir (G9 segmenti)

public record MarketingConsentsRequest(bool Email, bool Sms, bool Phone);

public record SetIdentityRequest(string IdentityNumber, DateOnly? BirthDate = null);
