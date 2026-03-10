using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetFirmDetail;

public record GetFirmDetailQuery(Guid Id) : IRequest<Result<FirmDetailDto>>;

public record FirmDetailDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    string PriceType,
    decimal? PriceMultiplier,
    Guid? InvoiceIntegratorId,
    bool IsActive,
    DateTime CreatedAt,
    List<FirmPlatformSummaryDto> Platforms,
    List<FirmIntegrationSummaryDto> Integrations
);

public record FirmPlatformSummaryDto(Guid Id, string Code, Dictionary<string, string> NameI18n, bool IsActive);
public record FirmIntegrationSummaryDto(Guid Id, string? Name, string ServiceType, bool IsActive);

public class GetFirmDetailQueryHandler : IRequestHandler<GetFirmDetailQuery, Result<FirmDetailDto>>
{
    private readonly ICoreDbContext _db;

    public GetFirmDetailQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<FirmDetailDto>> Handle(GetFirmDetailQuery request, CancellationToken ct)
    {
        var firm = await _db.Firms
            .Include(f => f.FirmPlatforms)
            .Include(f => f.FirmIntegrations).ThenInclude(fi => fi.IntegrationService)
            .FirstOrDefaultAsync(f => f.Id == request.Id, ct);

        if (firm is null)
            return Result.Failure<FirmDetailDto>("Firma bulunamadı.");

        var dto = new FirmDetailDto(
            firm.Id, firm.Code, firm.NameI18n, firm.TaxOffice, firm.TaxNumber,
            firm.Address, firm.Phone, firm.Email, firm.IsMain, firm.PriceType,
            firm.PriceMultiplier, firm.InvoiceIntegratorId, firm.IsActive, firm.CreatedAt,
            firm.FirmPlatforms.Where(p => !p.IsDeleted)
                .Select(p => new FirmPlatformSummaryDto(p.Id, p.Code, p.NameI18n, p.IsActive)).ToList(),
            firm.FirmIntegrations.Where(i => !i.IsDeleted)
                .Select(i => new FirmIntegrationSummaryDto(i.Id, i.Name, i.IntegrationService.ServiceType, i.IsActive)).ToList()
        );

        return Result.Success<FirmDetailDto>(dto);
    }
}
