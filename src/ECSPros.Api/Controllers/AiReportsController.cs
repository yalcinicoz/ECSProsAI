using System.Security.Claims;
using System.Text.Json;
using ECSPros.Api.Authorization;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Api.Services.Store;
using ECSPros.Iam.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ECSPros.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/ai")]
[RequirePermission(ReportDictionary.UsePermission)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiReportsController(IConfiguration configuration, IEtkinYetkiServisi authorization,
    StockReportExecutor executor, IVitrinAuditLogger audit, IReportAttributeCatalog attributeCatalog) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue("sub")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var permissions = await Permissions(ct);
        if (!ReportDictionary.CanReport(permissions)) return Denied();
        IReadOnlyList<ReportField> fields;
        IReadOnlyList<ReportField> attributes;
        var dynamicEnabled = configuration.GetValue<bool>("AiReporting:DynamicEnabled");
        var sources = ReportSourceCatalog.ForPermissions(permissions)
            .Where(s => s.Id is "stock" or "orders" || dynamicEnabled).ToArray();
        try
        {
            attributes = await attributeCatalog.LoadAsync(permissions, ct);
            fields = sources.SelectMany(s => ReportDictionary.ForSubject(s.Id, permissions, attributes)).ToArray();
        }
        catch (Exception ex) when (ex is ReportCatalogException or NpgsqlException)
        { return StatusCode(503, new { success = false, error = "Rapor özellik sözlüğü alınamadı. Daha sonra tekrar deneyin." }); }
        return Ok(new { success = true, data = new
        {
            version = ReportDictionary.Version,
            enabled = configuration.GetValue<bool>("AiReporting:Enabled"),
            naturalLanguageEnabled = configuration.GetValue<bool>("AiReporting:NaturalLanguageEnabled"),
            dynamicEnabled = configuration.GetValue<bool>("AiReporting:DynamicEnabled"),
            dynamicFields = configuration.GetValue<bool>("AiReporting:DynamicEnabled") ? DynamicReportMetadata.Fields(permissions) : Array.Empty<ReportField>(),
            dynamicDetailFields = configuration.GetValue<bool>("AiReporting:DynamicEnabled") ? ReportBusinessDictionary.Orders.DescribeDetails(permissions) : Array.Empty<ReportField>(),
            dynamicSources = sources.Where(s => dynamicEnabled && DynamicReportPlan.SupportsSource(s.Id)).ToDictionary(s => s.Id,
                s => new { fields = DynamicReportMetadata.Fields(permissions, s.Id, attributes), detailFields = DynamicReportMetadata.Details(permissions, s.Id, attributes) }),
            fields,
            subjects = sources.Select(s => s.Id),
            sources,
            maxRows = ReportDefinitionValidator.MaxRows
        }});
    }

    [HttpGet("firms")]
    public async Task<IActionResult> Firms([FromServices] IOpenAiFirmSettingsProvider settingsProvider, CancellationToken ct)
    {
        if (!ReportDictionary.CanReport(await Permissions(ct))) return Denied();
        try { return Ok(new { success = true, data = await settingsProvider.ListFirmsAsync(UserId, ct) }); }
        catch (UnauthorizedAccessException) { return Denied(); }
        catch (NpgsqlException)
        { return StatusCode(503, new { success = false, error = "Firma listesi alınamadı." }); }
    }

    [HttpPost("interpret")]
    [RequestSizeLimit(32_768)]
    public async Task<IActionResult> Interpret([FromBody] InterpretReportRequest request,
        [FromServices] IOpenAiFirmSettingsProvider settingsProvider,
        [FromServices] OpenAiReportInterpreter interpreter, [FromServices] AiReportQuota quota, CancellationToken ct)
    {
        var permissions = await Permissions(ct);
        if (ReportDictionary.ForSubject(request.Subject, permissions).Count == 0) return Denied();
        if (request.PlanVersion is not (1 or 2) || request.PlanVersion == 2 && !DynamicReportPlan.SupportsSource(request.Subject)
            || request.Subject is MovementReportSource.Id or ReturnReportSource.Id or CustomerReportSource.Id && request.PlanVersion != 2)
            return BadRequest(new { success = false, error = "Rapor plan sürümü desteklenmiyor." });
        if (request.PlanVersion == 2 && !configuration.GetValue<bool>("AiReporting:DynamicEnabled"))
            return StatusCode(503, new { success = false, error = "Dinamik rapor planı henüz etkinleştirilmedi." });
        if (!configuration.GetValue<bool>("AiReporting:NaturalLanguageEnabled"))
            return StatusCode(503, new { success = false, error = "OpenAI rapor yorumlama henüz etkin değil." });
        ApprovedReportPrompt prompt;
        ReportConversation conversation;
        try
        {
            prompt = ApprovedReportPrompt.Create(request.Prompt, request.TransmissionConfirmed);
            conversation = ReportConversation.Create(request.History, prompt, request.TransmissionConfirmed);
        }
        catch (ArgumentException ex) { return BadRequest(new { success = false, error = ex.Message }); }
        try
        {
            var settings = await settingsProvider.GetAsync(UserId, request.FirmId, ct);
            var attributes = request.Subject != "stock" ? Array.Empty<ReportField>() : await attributeCatalog.LoadForPromptAsync(permissions,
                string.Join(" ", (request.History ?? []).Select(t => t.Prompt).Append(request.Prompt)), ct);
            AiQuotaDecision reservation;
            try { reservation = await quota.ReserveAsync(UserId, settings.FirmId, ct); }
            catch (Exception ex) when (ex is StackExchange.Redis.RedisException or TimeoutException or InvalidOperationException)
            { return StatusCode(503, new { success = false, error = "AI kullanım kotası doğrulanamadı. İstek gönderilmedi." }); }
            if (!reservation.Allowed)
            {
                Response.Headers.RetryAfter = reservation.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return StatusCode(429, new { success = false, error = "AI kullanım sınırına ulaşıldı. Daha sonra tekrar deneyin." });
            }
            ct.ThrowIfCancellationRequested();
            var result = await interpreter.InterpretAsync(settings.ApiKey, settings.Model, prompt, permissions, ct, attributes, conversation, request.Subject, request.PlanVersion);
            // Never calculate a report here. Only return a proposal for a separate confirmation/run.
            var current = await Permissions(ct);
            if (ReportDictionary.ForSubject(request.Subject, current).Count == 0) return Denied();
            if (result.DynamicPlan is not null)
            {
                if (request.PlanVersion != 2) return Denied();
                var effective = await authorization.GetirAsync(UserId, ct);
                await HttpContext.RequestServices.GetRequiredService<OrderReportExecutor>().ValidateDynamicPlanAsync(
                    JsonSerializer.Serialize(result.DynamicPlan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), effective, ct);
                var bindingQuestion = await attributeCatalog.CheckBindingsAsync(result.DynamicPlan, current, ct);
                if (bindingQuestion is not null)
                    result = new("clarify", bindingQuestion, null, "stock_attribute");
            }
            if (result.Definition is not null && !ReportDefinitionValidator.Parse(
                JsonSerializer.Serialize(result.Definition, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                current, attributes: attributes).IsValid) return Denied();
            await audit.LogAsync(HttpContext, "ReportInterpreted", "AiStockReport", Guid.NewGuid(), null,
                new { status = result.Status, errorCode = result.ErrorCode, integrationId = settings.IntegrationId }, Guid.Empty, "Rapor taslağı", ct);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException) { return Denied(); }
        catch (InvalidOperationException)
        { return BadRequest(new { success = false, error = "Firma OpenAI entegrasyonunu, aktif durumunu ve tekil kaydını kontrol edin." }); }
        catch (ArgumentException ex)
        {
            // Diagnose validation failures without logging prompts, plans, literals or provider payloads.
            HttpContext.RequestServices.GetService<ILogger<AiReportsController>>()
                ?.LogWarning("AI report validation rejected ({ValidationCode}). Stack: {ValidationStack}", ex.Message switch
                {
                    "Rapor alanı desteklenmiyor." => "predicate_field",
                    "Rapor koşulu biçimi geçerli değil." => "predicate_shape",
                    "Rapor ilişkisi desteklenmiyor." => "predicate_relation",
                    "Rapor koşul grubu geçerli değil." => "predicate_group",
                    _ => "validation"
                }, ex.StackTrace);
            return BadRequest(new { success = false, error = "AI isteği veya planı doğrulanamadı; rapor çalıştırılmadı." });
        }
        catch (NpgsqlException)
        { return StatusCode(503, new { success = false, error = "Firma ayarları alınamadı." }); }
        catch (ReportCatalogException)
        { return StatusCode(503, new { success = false, error = "Rapor özellik sözlüğü alınamadı; istek gönderilmedi." }); }
    }

    [HttpPost("run")]
    [RequestSizeLimit(ReportDefinitionValidator.MaxPayloadBytes)]
    public Task<IActionResult> Run([FromBody] JsonElement recipe, CancellationToken ct) => RunCore(recipe, ct);

    [HttpPost("grid")]
    [RequestSizeLimit(32_768)]
    public Task<IActionResult> Grid([FromBody] GridReportRequest request, CancellationToken ct) =>
        request.Grid is null ? Task.FromResult<IActionResult>(BadRequest(new { success = false, error = "Tablo ayarları eksik." }))
            : RunCore(request.Definition, ct, request.Grid);

    [HttpPost("export")]
    [RequestSizeLimit(32_768)]
    public Task<IActionResult> Export([FromBody] GridReportRequest request, CancellationToken ct) =>
        request.Grid is null ? Task.FromResult<IActionResult>(BadRequest())
            : RunCore(request.Definition, ct, ReportGridState.ForExport(request.Grid), true);

    private async Task<IActionResult> RunCore(JsonElement recipe, CancellationToken ct, ReportGridState? grid = null, bool forExport = false)
    {
        var permissions = await Permissions(ct);
        if (!ReportDictionary.CanReport(permissions)) return Denied();
        if (forExport && !await CanExport(ct)) return Denied();
        // Explicit rollout gate: no live query before database/load acceptance.
        if (!configuration.GetValue<bool>("AiReporting:Enabled"))
            return StatusCode(503, new { success = false, error = "Rapor hesaplama henüz etkinleştirilmedi." });
        if (recipe.ValueKind != JsonValueKind.Object) return BadRequest(new { success = false, error = "Rapor tarifi eksik." });
        var json = recipe.GetRawText();
        try
        {
            if (recipe.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var number) && number == 2)
            {
                if (!configuration.GetValue<bool>("AiReporting:DynamicEnabled"))
                    return StatusCode(503, new { success = false, error = "Dinamik rapor planı henüz etkinleştirilmedi." });
                if (grid is null) return BadRequest(new { success = false, error = "Dinamik rapor sayfalı tablo ucundan çalıştırılmalıdır." });
                StockReportResult dynamicResult;
                try
                {
                    var dynamicPlan = DynamicReportPlan.Parse(json);
                    if (ReportDictionary.ForSubject(dynamicPlan.Source!, permissions).Count == 0) return Denied();
                    dynamicResult = await HttpContext.RequestServices.GetRequiredService<OrderReportExecutor>().ExecuteDynamicAsync(UserId, json, grid, ct);
                }
                catch (ArgumentException ex) { return BadRequest(new { success = false, error = ex.Message }); }
                if (forExport) return await ExportFile(dynamicResult, ct);
                await audit.LogAsync(HttpContext, "ReportExecuted", "AiDynamicReport", Guid.NewGuid(), null,
                    new { version = 2, rowCount = dynamicResult.Rows.Count }, Guid.Empty, "Dinamik rapor", ct);
                return Ok(new { success = true, data = dynamicResult });
            }
            var isOrders = recipe.TryGetProperty("subject", out var subjectValue) && subjectValue.ValueKind == JsonValueKind.String
                && subjectValue.GetString() == OrderReportRecipe.Subject;
            var attributes = isOrders ? Array.Empty<ReportField>() : await attributeCatalog.LoadAsync(permissions, ct);
            var validation = ReportDefinitionValidator.Parse(json, permissions, attributes: attributes);
            if (!validation.IsValid) return BadRequest(new { success = false, error = validation.Error });
            if (validation.Definition!.Subject == OrderReportRecipe.Subject)
            {
                if (grid is null) return BadRequest(new { success = false, error = "Sipariş raporu sayfalı tablo ucundan çalıştırılmalıdır." });
                var plan = OrderReportRecipe.Parse(validation.Definition, permissions);
                try { OrderReportExecutor.ValidateGrid(validation.Definition, grid, plan.Details); }
                catch (ArgumentException ex) { return BadRequest(new { success = false, error = ex.Message }); }
                var orderResult = await HttpContext.RequestServices.GetRequiredService<OrderReportExecutor>().ExecuteAsync(UserId, json, grid, ct);
                if (forExport) return await ExportFile(orderResult, ct);
                await audit.LogAsync(HttpContext, "ReportExecuted", "AiOrderReport", Guid.NewGuid(), null,
                    new { version = 1, rowCount = orderResult.Rows.Count }, Guid.Empty, "Sipariş raporu", ct);
                return Ok(new { success = true, data = orderResult });
            }
            // Compile before opening the executor so invalid grid input returns 400, not a false permission error.
            if (grid is not null)
            {
                try { using var check = ReportGridQuery.Create(json, permissions, attributes, grid); }
                catch (ArgumentException ex) { return BadRequest(new { success = false, error = ex.Message }); }
            }
            var result = await executor.ExecuteAsync(UserId, json, ct, grid);
            if (forExport) return await ExportFile(result, ct);
            // Existing audit service records metadata only, no filters, prompts or result values.
            await audit.LogAsync(HttpContext, "ReportExecuted", "AiStockReport", Guid.NewGuid(),
                null, new { version = ReportDictionary.Version, rowCount = result.Rows.Count },
                Guid.Empty, "Stok raporu", ct);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException) { return Denied(); }
        catch (ECSPros.Shared.Kernel.Grid.GridException ex) { return BadRequest(new { success = false, error = ex.Message }); }
        catch (ArgumentException) { return Denied(); } // permissions may have changed during the request
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(503, new { success = false, error = "Rapor hesaplama süresi aşıldı. Tarih veya filtre kapsamını daraltın." }); }
        catch (ReportCatalogException)
        { return StatusCode(503, new { success = false, error = "Rapor özellik sözlüğü alınamadı." }); }
        catch (InvalidOperationException)
        {
            return StatusCode(422, new { success = false, error = "Rapor sınırı aşıldı veya sistem meşgul. Filtreyi daraltıp tekrar deneyin." });
        }
        catch (NpgsqlException)
        {
            return StatusCode(503, new { success = false, error = "Rapor verisi alınamadı. Daha sonra tekrar deneyin." });
        }
    }

    private async Task<IReadOnlySet<string>> Permissions(CancellationToken ct) =>
        UserId == Guid.Empty ? new HashSet<string>()
            : StockReportExecutor.ResolvePermissions(await authorization.GetirAsync(UserId, ct));

    private async Task<bool> CanExport(CancellationToken ct)
    {
        if (UserId == Guid.Empty) return false;
        var rights = await authorization.GetirAsync(UserId, ct);
        return rights.Var(ReportDictionary.ExportPermission) && rights.Kanallar(ReportDictionary.ExportPermission) is null;
    }

    private async Task<IActionResult> ExportFile(StockReportResult result, CancellationToken ct)
    {
        if (!await CanExport(ct)) return Denied();
        try { ReportExcelExport.Validate(result); }
        catch (ArgumentException ex) { return UnprocessableEntity(new { success = false, error = ex.Message }); }
        await audit.LogAsync(HttpContext, "ReportExportPrepared", "AiReport", Guid.NewGuid(), null,
            new { rowCount = result.Rows.Count }, Guid.Empty, "Rapor dışa aktarma", ct);
        var stream = await ECSPros.Api.Grid.GridExportWriter.WriteToTempAsync(result.Rows,
            ReportExcelExport.Columns(result), "Rapor", ct);
        return File(stream, ECSPros.Api.Grid.GridExportWriter.XlsxMime, ECSPros.Api.Grid.GridExportWriter.FileName("ai-rapor"));
    }

    private ObjectResult Denied() => StatusCode(403, new { success = false, error = "Bu rapor için yetkiniz yok." });
}

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record InterpretReportRequest(string? Prompt, bool TransmissionConfirmed, Guid? FirmId,
    ReportConversationTurn[]? History = null, string Subject = "stock", int PlanVersion = 1);

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record GridReportRequest(JsonElement Definition, ReportGridState? Grid);
