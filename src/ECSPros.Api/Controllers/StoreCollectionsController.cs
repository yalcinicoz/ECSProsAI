using ECSPros.Storefront.Application.Commands.AddCollectionItems;
using ECSPros.Storefront.Application.Commands.CreateCollection;
using ECSPros.Storefront.Application.Commands.DeleteCollection;
using ECSPros.Storefront.Application.Commands.ToggleQuickSave;
using ECSPros.Storefront.Application.Commands.UpdateCollection;
using ECSPros.Storefront.Application.Queries.GetMemberCollections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// E6: Koleksiyonlar (üye tarafı) — oluşturma modalı + kart/detay bookmark'ının
/// "Kaydedilenler" hızlı koleksiyonu. Koleksiyonlar pending doğar (admin onayı
/// /api/collections'ta); Faz G blokları yalnız approved+public olanları kullanır.
/// </summary>
[ApiController]
[Route("api/store/collections")]
[Authorize(Policy = "MemberOnly")]
public class StoreCollectionsController(IMediator mediator) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberCollectionsQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StoreCollectionCreateRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateCollectionCommand(
            req.FirmPlatformId, MemberId, req.Name, req.Description,
            req.IsPublic, req.IsShareable, req.ProductCodes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { collectionId = result.Value } });
    }

    /// <summary>2026-07-17: koleksiyon düzenleme — ürün listesi verilen kodlarla eşitlenir.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] StoreCollectionCreateRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateCollectionCommand(
            req.FirmPlatformId, MemberId, id, req.Name, req.Description,
            req.IsPublic, req.IsShareable, req.ProductCodes), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { collectionId = result.Value } });
    }

    /// <summary>2026-07-17: koleksiyon silme (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCollectionCommand(firmPlatformId, MemberId, id), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Var olan koleksiyona ürün ekle — ortak "Koleksiyona Ekle" modalının Varolan
    /// akışı buraya POST atar (2026-07-17'ye dek route eksikti, akış sessizce 404'lüyordu).</summary>
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItems(Guid id, [FromBody] StoreCollectionItemsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AddCollectionItemsCommand(
            req.FirmPlatformId, MemberId, id, req.ProductCodes ?? []), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { added = result.Value } });
    }

    /// <summary>Kart/detay bookmark'ı: "Kaydedilenler" hızlı koleksiyonuna ekle/çıkar.</summary>
    [HttpPost("saved/toggle")]
    public async Task<IActionResult> ToggleSaved([FromBody] StoreQuickSaveRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ToggleQuickSaveCommand(
            req.FirmPlatformId, MemberId, req.ProductCode, req.Add), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record StoreCollectionCreateRequest(
    Guid FirmPlatformId, string Name, string? Description,
    bool IsPublic, bool IsShareable, List<string>? ProductCodes = null);
public record StoreQuickSaveRequest(Guid FirmPlatformId, string ProductCode, bool Add);
public record StoreCollectionItemsRequest(Guid FirmPlatformId, List<string>? ProductCodes);
