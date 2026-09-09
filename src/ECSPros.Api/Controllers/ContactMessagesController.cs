using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Api.Authorization;
using ECSPros.Storefront.Application.Commands.UpdateContactMessageStatus;
using ECSPros.Storefront.Application.Queries.GetContactMessages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>P5: iletişim formu gelen kutusu (admin) — F3 mesajları DB'ye düşer, burada okunur.</summary>
[ApiController]
[Route("api/contact-messages")]
[Authorize]
[RequirePermission(Permissions.StorefrontModerationView)]   // Y2: sayfa yetkisi
public class ContactMessagesController(IMediator mediator) : ControllerBase
{
    /// <summary>Gelen kutusu (DataGrid: f.* filtreleri + sort/dir + arama). Y3: kanal kapsamı uygulanır.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromQuery] string? status = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var kapsam = await kanalKapsami.KanallarAsync(Permissions.StorefrontModerationView, ct);
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, kapsam, defaultPageSize: 20);
        var result = await mediator.Send(
            new GetContactMessagesQuery(status, firmPlatformId, search, grid.Page, grid.PageSize, kapsam, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Mesajları Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: status.
    /// Y3: kapsam gövdeden DEĞİL, kullanıcının yetkisinden çözülür.</summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> Export(
        [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<ContactMessagesController> logger, CancellationToken ct)
    {
        var kapsam = await kanalKapsami.KanallarAsync(Permissions.StorefrontModerationView, ct);
        var filters = new ContactMessageFilters(body.NamedValue("status"), Search: body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "contact-messages", "iletisim-mesajlari", "İletişim Mesajları",
            ECSPros.Api.Grid.ContactMessageExportColumns.All,
            max => mediator.Send(new ExportContactMessagesQuery(filters, body.ToGridRequest(kapsam), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpPatch("{id}/status")]
    [RequirePermission(Permissions.StorefrontModerationManage)]   // Y2
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateContactMessageStatusRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateContactMessageStatusCommand(id, req.Status), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record UpdateContactMessageStatusRequest(string Status);
