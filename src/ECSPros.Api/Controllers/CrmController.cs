using ECSPros.Crm.Application.Commands.AddMemberAddress;
using ECSPros.Crm.Application.Commands.CreateMember;
using ECSPros.Crm.Application.Commands.CreateMemberGroup;
using ECSPros.Crm.Application.Commands.DeleteMemberAddress;
using ECSPros.Crm.Application.Commands.UpdateMember;
using ECSPros.Crm.Application.Commands.UpdateMemberGroup;
using ECSPros.Crm.Application.Queries.GetCities;
using ECSPros.Crm.Application.Queries.GetCountries;
using ECSPros.Crm.Application.Queries.GetDistricts;
using ECSPros.Crm.Application.Queries.GetMemberAddresses;
using ECSPros.Crm.Application.Queries.GetMemberDetail;
using ECSPros.Crm.Application.Queries.GetMemberGroups;
using ECSPros.Crm.Application.Queries.GetMemberLoyalty;
using ECSPros.Crm.Application.Queries.GetMemberWallet;
using ECSPros.Crm.Application.Queries.GetMembers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/crm")]
[Authorize]
public class CrmController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrmController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Üyeleri sayfalı listeler.</summary>
    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMembersQuery(search, activeOnly, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Üye detayını döner.</summary>
    [HttpGet("members/{id:guid}")]
    public async Task<IActionResult> GetMemberDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMemberDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni üye oluşturur.</summary>
    [HttpPost("members")]
    public async Task<IActionResult> CreateMember([FromBody] CreateMemberRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateMemberCommand(
            request.MemberGroupId, request.FirstName, request.LastName,
            request.Email, request.Phone, request.Password,
            request.Gender, request.BirthDate,
            request.CompanyName, request.TaxNumber, request.TaxOffice), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/crm/members/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Üye bilgilerini günceller.</summary>
    [HttpPut("members/{id:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, [FromBody] UpdateMemberRequest request, CancellationToken ct)
    {
        var updatedBy = Guid.TryParse(User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;

        var result = await _mediator.Send(new UpdateMemberCommand(
            id, request.FirstName, request.LastName,
            request.Email, request.Phone, request.Gender, request.BirthDate,
            request.TaxOffice, request.TaxNumber, request.CompanyName,
            request.IsActive, request.MemberGroupId, updatedBy), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Member Addresses ──────────────────────────────────────────────────────

    /// <summary>Üye adreslerini listeler.</summary>
    [HttpGet("members/{id:guid}/addresses")]
    public async Task<IActionResult> GetMemberAddresses(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMemberAddressesQuery(id), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Üyeye adres ekler.</summary>
    [HttpPost("members/{id:guid}/addresses")]
    public async Task<IActionResult> AddMemberAddress(Guid id, [FromBody] AddMemberAddressRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddMemberAddressCommand(
            id, request.Title,
            request.CountryId, request.CountryName,
            request.CityId, request.CityName,
            request.DistrictId, request.DistrictName,
            request.NeighborhoodId, request.NeighborhoodName,
            request.AddressLine, request.PostalCode,
            request.RecipientName, request.RecipientPhone,
            request.DeliveryNotes, request.IsDefault), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/crm/members/{id}/addresses", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Üye adresini siler (soft delete).</summary>
    [HttpDelete("members/{memberId:guid}/addresses/{addressId:guid}")]
    public async Task<IActionResult> DeleteMemberAddress(Guid memberId, Guid addressId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteMemberAddressCommand(memberId, addressId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Wallet ────────────────────────────────────────────────────────────────

    /// <summary>Üye cüzdan bilgisi ve son işlemleri.</summary>
    [HttpGet("members/{id:guid}/wallet")]
    public async Task<IActionResult> GetMemberWallet(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMemberWalletQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Loyalty ───────────────────────────────────────────────────────────────

    /// <summary>Üye sadakat hesabı ve son işlemleri.</summary>
    [HttpGet("members/{id:guid}/loyalty")]
    public async Task<IActionResult> GetMemberLoyalty(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMemberLoyaltyQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Member Groups ─────────────────────────────────────────────────────────

    /// <summary>Üye gruplarını listeler.</summary>
    [HttpGet("member-groups")]
    public async Task<IActionResult> GetMemberGroups([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMemberGroupsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Üye grubu oluşturur.</summary>
    [HttpPost("member-groups")]
    public async Task<IActionResult> CreateMemberGroup([FromBody] CreateMemberGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateMemberGroupCommand(
            request.Code, request.NameI18n, request.IsDefault, request.IsWholesale,
            request.RequiresApproval, request.ShowPricesBeforeLogin,
            request.MinOrderAmount, request.PaymentTermsDays, request.SortOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created("/api/crm/member-groups", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Üye grubunu günceller.</summary>
    [HttpPut("member-groups/{id:guid}")]
    public async Task<IActionResult> UpdateMemberGroup(Guid id, [FromBody] UpdateMemberGroupRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateMemberGroupCommand(
            id, request.NameI18n, request.IsWholesale, request.RequiresApproval,
            request.ShowPricesBeforeLogin, request.MinOrderAmount, request.PaymentTermsDays,
            request.IsActive, request.SortOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    // ─── Address Definitions ───────────────────────────────────────────────────

    /// <summary>Ülke listesi.</summary>
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCountriesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>İl listesi (ülkeye göre).</summary>
    [HttpGet("countries/{countryId:guid}/cities")]
    public async Task<IActionResult> GetCities(Guid countryId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCitiesQuery(countryId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>İlçe listesi (ile göre).</summary>
    [HttpGet("cities/{cityId:guid}/districts")]
    public async Task<IActionResult> GetDistricts(Guid cityId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDistrictsQuery(cityId, activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }
}

public record CreateMemberRequest(
    Guid MemberGroupId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Password,
    string? Gender,
    DateOnly? BirthDate,
    string? CompanyName,
    string? TaxNumber,
    string? TaxOffice);

public record UpdateMemberRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    string? TaxOffice,
    string? TaxNumber,
    string? CompanyName,
    bool IsActive,
    Guid? MemberGroupId);

public record AddMemberAddressRequest(
    string Title,
    Guid? CountryId,
    string CountryName,
    Guid? CityId,
    string CityName,
    Guid? DistrictId,
    string DistrictName,
    Guid? NeighborhoodId,
    string? NeighborhoodName,
    string? AddressLine,
    string? PostalCode,
    string RecipientName,
    string RecipientPhone,
    string? DeliveryNotes,
    bool IsDefault = false);

public record CreateMemberGroupRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsDefault = false,
    bool IsWholesale = false,
    bool RequiresApproval = false,
    bool ShowPricesBeforeLogin = true,
    decimal? MinOrderAmount = null,
    int? PaymentTermsDays = null,
    int SortOrder = 0);

public record UpdateMemberGroupRequest(
    Dictionary<string, string> NameI18n,
    bool IsWholesale,
    bool RequiresApproval,
    bool ShowPricesBeforeLogin,
    decimal? MinOrderAmount,
    int? PaymentTermsDays,
    bool IsActive,
    int SortOrder);
