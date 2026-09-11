using System.Text.Json;
using System.Text.Json.Serialization;
using ECSPros.Api.Extensions;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Iam.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Internal server configuration, never an API response or log payload.</summary>
public sealed class OpenAiFirmSettings
{
    public Guid FirmId { get; }
    public Guid IntegrationId { get; }
    public string Model { get; }
    [JsonIgnore] public string ApiKey { get; }
    private OpenAiFirmSettings(Guid firmId, Guid integrationId, string model, string apiKey)
        => (FirmId, IntegrationId, Model, ApiKey) = (firmId, integrationId, model, apiKey);
    public override string ToString() => "OpenAI firma ayarları (gizli bilgiler gösterilmez)";

    public static OpenAiFirmSettings From(FirmPlatformIntegration integration)
    {
        static string? Text(Dictionary<string, object> values, string key) =>
            values.TryGetValue(key, out var value) ? value switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null
            } : null;
        var key = Text(integration.Credentials, "apiKey");
        var model = Text(integration.Settings, "model");
        if (string.IsNullOrWhiteSpace(key) || key == "•••" || key.Length > 4096
            || key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            || string.IsNullOrWhiteSpace(model) || model.Length > 128 || model.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new InvalidOperationException("OpenAI API anahtarı veya model bilgisi eksik/geçersiz.");
        return new(integration.FirmId, integration.Id, model, key);
    }
}

public sealed record ReportFirmOption(Guid Id, string Code, Dictionary<string, string> NameI18n);

public interface IOpenAiFirmSettingsProvider
{
    Task<List<ReportFirmOption>> ListFirmsAsync(Guid userId, CancellationToken ct);
    Task<OpenAiFirmSettings> GetAsync(Guid userId, Guid? selectedFirmId, CancellationToken ct);
}

/// <summary>No global cache or cross-firm fallback. Credentials never appear in firm options.</summary>
public sealed class OpenAiFirmSettingsProvider(ICoreDbContext core, IIamDbContext iam, IConfiguration configuration) : IOpenAiFirmSettingsProvider
{
    // Explicit operator choice; never infer billing authority from the first integration.
    private Guid? SharedAccountFirm() => ParseSharedAccount(configuration["AiReporting:AccountFirmId"]);

    public static Guid? ParseSharedAccount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            throw new InvalidOperationException("Ortak AI hesabı yapılandırması geçersiz.");
        return id;
    }

    public static Guid ResolveAccount(Guid? sharedFirmId, Guid? userFirmId, bool superAdmin, Guid? selectedFirmId)
    {
        if (sharedFirmId is not { } shared) return ResolveFirm(userFirmId, superAdmin, selectedFirmId);
        if (shared == Guid.Empty) throw new InvalidOperationException("Ortak AI hesabı yapılandırması geçersiz.");
        if (selectedFirmId.HasValue && selectedFirmId != shared)
            throw new UnauthorizedAccessException("Yönetici tarafından belirlenen AI hesabı değiştirilemez.");
        return shared;
    }
    public async Task<List<ReportFirmOption>> ListFirmsAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        var user = await iam.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive && !u.IsDeleted)
            .Select(u => new { u.FirmId, u.IsSuperAdmin }).SingleOrDefaultAsync(ct);
        if (user is null) throw new UnauthorizedAccessException();
        var shared = SharedAccountFirm();
        return await FirmOptions(core.Firms.AsNoTracking(), shared ?? user.FirmId,
            shared is null && user.IsSuperAdmin).ToListAsync(ct);
    }

    public static IQueryable<ReportFirmOption> FirmOptions(IQueryable<Firm> firms, Guid? userFirmId, bool superAdmin) =>
        firms.Where(f => f.IsActive && !f.IsDeleted && (superAdmin || f.Id == userFirmId))
            .OrderBy(f => f.Code).ThenBy(f => f.Id)
            .Select(f => new ReportFirmOption(f.Id, f.Code, f.NameI18n));

    public async Task<OpenAiFirmSettings> GetAsync(Guid userId, Guid? selectedFirmId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        var user = await iam.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive && !u.IsDeleted)
            .Select(u => new { u.FirmId, u.IsSuperAdmin }).SingleOrDefaultAsync(ct);
        if (user is null) throw new UnauthorizedAccessException();
        var firmId = ResolveAccount(SharedAccountFirm(), user.FirmId, user.IsSuperAdmin, selectedFirmId);
        var eligible = Eligible(core.FirmPlatformIntegrations.AsNoTracking(), firmId, DateTime.UtcNow);
        // Check uniqueness before retrieving/decrypting credentials.
        var ids = await eligible.Select(x => x.Id).Take(2).ToArrayAsync(ct);
        var id = ChooseUnique(ids);
        var integration = await eligible.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (integration is null) throw new InvalidOperationException("OpenAI entegrasyonu artık kullanılamıyor.");
        return OpenAiFirmSettings.From(integration);
    }

    public static Guid ResolveFirm(Guid? userFirmId, bool superAdmin, Guid? selectedFirmId)
    {
        var firmId = selectedFirmId ?? userFirmId;
        if (!firmId.HasValue || firmId == Guid.Empty
            || (!superAdmin && userFirmId != firmId))
            throw new UnauthorizedAccessException("OpenAI hesabını kullanacak firma doğrulanamadı.");
        return firmId.Value;
    }

    public static Guid ChooseUnique(IReadOnlyCollection<Guid> ids) => ids.Count == 1
        ? ids.Single() : throw new InvalidOperationException(ids.Count == 0
            ? "Aktif firma-geneli OpenAI entegrasyonu bulunamadı."
            : "Birden fazla aktif OpenAI entegrasyonu var; kayıtları netleştirin.");

    public static IQueryable<FirmPlatformIntegration> Eligible(IQueryable<FirmPlatformIntegration> source,
        Guid firmId, DateTime now) => source.Where(x =>
            x.FirmId == firmId && x.FirmPlatformId == null && !x.IsDeleted && x.IsActive && x.Status == "active"
            && (!x.StartDate.HasValue || x.StartDate <= now) && (!x.EndDate.HasValue || x.EndDate > now)
            && x.Firm.IsActive && !x.Firm.IsDeleted
            && x.IntegrationService.Code == OpenAiReportingCatalog.Code
            && x.IntegrationService.ServiceType == OpenAiReportingCatalog.ServiceType
            && x.IntegrationService.IsAvailable && !x.IntegrationService.IsDeleted);
}
