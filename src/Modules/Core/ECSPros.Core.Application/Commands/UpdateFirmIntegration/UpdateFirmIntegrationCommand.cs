using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateFirmIntegration;

public record UpdateFirmIntegrationCommand(
    Guid Id,
    string? Name,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings,
    bool IsActive,
    string? ContractNumber,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    Dictionary<string, object>? Terms,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    string? DocumentUrl
) : IRequest<Result<bool>>;

public class UpdateFirmIntegrationCommandHandler : IRequestHandler<UpdateFirmIntegrationCommand, Result<bool>>
{
    private readonly ICoreDbContext _db;

    public UpdateFirmIntegrationCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateFirmIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _db.FirmIntegrations.FirstOrDefaultAsync(fi => fi.Id == request.Id, ct);
        if (integration is null)
            return Result.Failure<bool>("Firma entegrasyonu bulunamadı.");

        integration.Name = request.Name;
        integration.Credentials = request.Credentials;
        integration.Settings = request.Settings;
        integration.IsActive = request.IsActive;
        integration.ContractNumber = request.ContractNumber;
        integration.StartDate = AsUtc(request.StartDate);
        integration.EndDate = AsUtc(request.EndDate);
        integration.Status = request.Status;
        integration.Terms = request.Terms;
        integration.ContactName = request.ContactName;
        integration.ContactPhone = request.ContactPhone;
        integration.ContactEmail = request.ContactEmail;
        integration.DocumentUrl = request.DocumentUrl;
        integration.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success<bool>(true);
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
