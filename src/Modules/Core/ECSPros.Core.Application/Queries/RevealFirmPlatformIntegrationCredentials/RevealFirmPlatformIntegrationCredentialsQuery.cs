using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.RevealFirmPlatformIntegrationCredentials;

/// <summary>Bir firma entegrasyonunun kimlik bilgilerini AÇIK METİN döner. Normal GET
/// maskeli döner (CredentialsMasking); bu sorgu yalnız
/// Permissions.IntegrationCredentialsReveal yetkili "Göster" düğmesi içindir ve
/// controller her çağrıyı audit_logs'a yazar. Yanıt asla cache'lenmez.</summary>
public record RevealFirmPlatformIntegrationCredentialsQuery(Guid Id)
    : IRequest<Result<RevealedCredentialsDto>>;

public record RevealedCredentialsDto(
    Guid Id,
    Guid FirmId,
    string ServiceCode,
    Dictionary<string, object> Credentials);

public class RevealFirmPlatformIntegrationCredentialsQueryHandler
    : IRequestHandler<RevealFirmPlatformIntegrationCredentialsQuery, Result<RevealedCredentialsDto>>
{
    private readonly ICoreDbContext _db;

    public RevealFirmPlatformIntegrationCredentialsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<RevealedCredentialsDto>> Handle(
        RevealFirmPlatformIntegrationCredentialsQuery request, CancellationToken ct)
    {
        var fi = await _db.FirmPlatformIntegrations
            .Include(x => x.IntegrationService)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (fi is null)
            return Result.Failure<RevealedCredentialsDto>("Entegrasyon bulunamadı.");

        return Result.Success(new RevealedCredentialsDto(
            fi.Id, fi.FirmId, fi.IntegrationService.Code, fi.Credentials));
    }
}
