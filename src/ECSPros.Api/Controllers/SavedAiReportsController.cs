using System.Security.Claims;
using System.Text.Json;
using ECSPros.Api.Authorization;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Api.Services.Store;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Iam.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

[ApiController, Authorize, Route("api/reports/ai/saved")]
[RequirePermission(ReportDictionary.UsePermission)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SavedAiReportsController(IIamDbContext db, IEtkinYetkiServisi authorization,
    IReportAttributeCatalog attributes, OrderReportExecutor orders, IConfiguration configuration) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    public const string Prefix = "reports.ai.saved.";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        var permissions = StockReportExecutor.ResolvePermissions(await authorization.GetirAsync(UserId, ct));
        if (!ReportDictionary.CanReport(permissions)) return Forbid();
        var prefs = await db.Users.AsNoTracking().Where(u => u.Id == UserId)
            .Select(u => u.Preferences).SingleOrDefaultAsync(ct);
        // Recipes only; loading never executes a report or reveals another owner's entries.
        return Ok(new { success = true, data = Enumerable.Range(0, 20)
            .Where(slot => prefs?.ContainsKey(Prefix + slot) == true)
            .Select(slot => new { slot, value = prefs![Prefix + slot] }).ToArray() });
    }

    [HttpPost("{slot:int}")]
    [RequestSizeLimit(20_480)]
    public Task<IActionResult> Save(int slot, [FromBody] SaveAiReport request, CancellationToken ct) => SaveCore(slot, request, null, ct);

    [HttpPut("{slot:int}")]
    [RequestSizeLimit(40_960)]
    public Task<IActionResult> Update(int slot, [FromBody] UpdateAiReport request, CancellationToken ct) =>
        request.Expected.ValueKind != JsonValueKind.Object ? Task.FromResult<IActionResult>(BadRequest())
            : SaveCore(slot, new(request.Name, request.Definition), request.Expected, ct);

    [HttpPost("{slot:int}/remove")]
    [RequestSizeLimit(20_480)]
    public async Task<IActionResult> Remove(int slot, [FromBody] RemoveAiReport request, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        if (slot is < 0 or >= 20 || request.Expected.ValueKind != JsonValueKind.Object) return BadRequest();
        if (!ReportDictionary.CanReport(StockReportExecutor.ResolvePermissions(await authorization.GetirAsync(UserId, ct)))) return Forbid();
        return await db.CompareExchangePreferenceAsync(UserId, Prefix + slot, request.Expected.GetRawText(), null, ct) == 1
            ? Ok(new { success = true }) : Conflict(new { success = false, error = "Rapor başka bir sekmede değişti. Listeyi yenileyin." });
    }

    private async Task<IActionResult> SaveCore(int slot, SaveAiReport request, JsonElement? expected, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        if (slot is < 0 or >= 20 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100
            || request.Definition.ValueKind != JsonValueKind.Object)
            return BadRequest(new { success = false, error = "Geçerli rapor adı ve tarif gerekli; en fazla 20 rapor saklanır." });
        var effective = await authorization.GetirAsync(UserId, ct);
        var permissions = StockReportExecutor.ResolvePermissions(effective);
        if (!ReportDictionary.CanReport(permissions)) return Forbid();
        try
        {
            await ValidateRecipe(request.Definition, effective, ct);
            var value = JsonSerializer.Serialize(new { name = request.Name.Trim(), definition = request.Definition, savedAtUtc = DateTime.UtcNow });
            // Insert-only slot avoids overwriting a report saved in another tab/node.
            var affected = expected is null
                ? await db.WritePreferenceAsync(UserId, Prefix + slot, value, true, ct)
                : await db.CompareExchangePreferenceAsync(UserId, Prefix + slot, expected.Value.GetRawText(), value, ct);
            if (affected != 1)
                return Conflict(new { success = false, error = "Bu kayıt yeri doldu. Listeyi yenileyip tekrar deneyin." });
            return Ok(new { success = true });
        }
        catch (ArgumentException) { return BadRequest(new { success = false, error = "Rapor tarifi veya yetkisi geçersiz." }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private async Task ValidateRecipe(JsonElement definition, EfektifYetkiler effective, CancellationToken ct)
    {
        if (definition.ValueKind != JsonValueKind.Object) throw new ArgumentException();
        var json = definition.GetRawText();
        if (definition.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var version) && version == 2)
        {
            if (!configuration.GetValue<bool>("AiReporting:DynamicEnabled")) throw new ArgumentException();
            await orders.ValidateDynamicPlanAsync(json, effective, ct);
        }
        else
        {
            var permissions = StockReportExecutor.ResolvePermissions(effective);
            var fields = await attributes.LoadAsync(permissions, ct);
            var validation = ReportDefinitionValidator.Parse(json, permissions, attributes: fields);
            if (!validation.IsValid) throw new ArgumentException();
        }
    }

    [HttpPost("{slot:int}/sharing")]
    [RequestSizeLimit(20_480)]
    public async Task<IActionResult> Sharing(int slot, [FromBody] ShareAiReport request,
        [FromServices] IReportShareDirectory directory, [FromServices] IVitrinAuditLogger audit, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        if (slot is < 0 or >= 20 || request.Expected.ValueKind != JsonValueKind.Object) return BadRequest();
        var effective = await authorization.GetirAsync(UserId, ct);
        // A user who lost share permission must still be able to revoke their own link.
        if (request.Enabled ? !ReportSharingPolicy.CanShare(effective) : !ReportDictionary.CanReport(StockReportExecutor.ResolvePermissions(effective))) return Forbid();
        var owner = await directory.FindAsync(UserId, ct);
        if (owner is null || !owner.IsActive) return Forbid();
        try
        {
            if (!request.Expected.TryGetProperty("definition", out var definition)) return BadRequest();
            if (request.Enabled) await ValidateRecipe(definition, effective, ct);
            var entry = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Expected.GetRawText())!;
            entry.Remove("shareToken");
            var token = request.Enabled ? ReportSharingPolicy.NewToken() : null;
            if (token is not null) entry["shareToken"] = JsonSerializer.SerializeToElement(token);
            if (await db.CompareExchangePreferenceAsync(UserId, Prefix + slot, request.Expected.GetRawText(), JsonSerializer.Serialize(entry), ct) != 1)
                return Conflict(new { success = false, error = "Kayıt değişti. Listeyi yenileyin." });
            await audit.LogAsync(HttpContext, request.Enabled ? "ReportShareEnabled" : "ReportShareRevoked", "AiReportRecipe", UserId,
                null, new { slot }, Guid.Empty, "Rapor tarifi paylaşımı", ct);
            return Ok(new { success = true, data = new { ownerId = UserId, slot, token } });
        }
        catch (ArgumentException) { return BadRequest(); }
        catch (JsonException) { return BadRequest(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("shared")]
    [RequestSizeLimit(1_024)]
    public async Task<IActionResult> Shared([FromBody] OpenSharedAiReport request,
        [FromServices] IReportShareDirectory directory, [FromServices] IVitrinAuditLogger audit, CancellationToken ct)
    {
        if (UserId == Guid.Empty) return Unauthorized();
        if (request.Slot is < 0 or >= 20 || request.Token?.Length != 64) return NotFound();
        var viewer = await directory.FindAsync(UserId, ct);
        var owner = await directory.FindAsync(request.OwnerId, ct);
        if (owner is null || viewer is null || !ReportSharingPolicy.ActiveParticipants(owner.IsActive, viewer.IsActive)
            || owner.Preferences?.TryGetValue(Prefix + request.Slot, out var stored) != true) return NotFound();
        try
        {
            var entry = JsonSerializer.SerializeToElement(stored);
            if (!entry.TryGetProperty("shareToken", out var token) || token.ValueKind != JsonValueKind.String
                || !ReportSharingPolicy.TokenMatches(token.GetString(), request.Token)) return NotFound();
            var ownerRights = await authorization.GetirAsync(request.OwnerId, ct);
            if (!ReportSharingPolicy.CanShare(ownerRights)) return NotFound();
            if (!entry.TryGetProperty("definition", out var definition)) return NotFound();
            // Recheck both sides; no report rows are executed or copied here.
            await ValidateRecipe(definition, ownerRights, ct);
            await ValidateRecipe(definition, await authorization.GetirAsync(UserId, ct), ct);
            await audit.LogAsync(HttpContext, "ReportShareOpened", "AiReportRecipe", request.OwnerId,
                null, new { slot = request.Slot }, Guid.Empty, "Rapor tarifi paylaşımı", ct);
            return Ok(new { success = true, data = new { definition } });
        }
        catch (ArgumentException) { return NotFound(); }
        catch (JsonException) { return NotFound(); }
        catch (InvalidOperationException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return NotFound(); }
    }
}

public sealed record SaveAiReport(string Name, JsonElement Definition);
public sealed record UpdateAiReport(string Name, JsonElement Definition, JsonElement Expected);
public sealed record RemoveAiReport(JsonElement Expected);
public sealed record ShareAiReport(JsonElement Expected, bool Enabled);
public sealed record OpenSharedAiReport(Guid OwnerId, int Slot, string Token);
