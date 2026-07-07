using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateFirmIntegration;

public record CreateFirmIntegrationCommand(
    Guid FirmId,
    Guid IntegrationServiceId,
    string? Name,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings,
    string? ContractNumber = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string Status = "draft",
    Dictionary<string, object>? Terms = null,
    string? ContactName = null,
    string? ContactPhone = null,
    string? ContactEmail = null,
    string? DocumentUrl = null
) : IRequest<Result<Guid>>;

public class CreateFirmIntegrationCommandHandler : IRequestHandler<CreateFirmIntegrationCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;

    public CreateFirmIntegrationCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateFirmIntegrationCommand request, CancellationToken ct)
    {
        var firmExists = await _db.Firms.AnyAsync(f => f.Id == request.FirmId, ct);
        if (!firmExists)
            return Result.Failure<Guid>("Firma bulunamadı.");

        var serviceExists = await _db.IntegrationServices.AnyAsync(s => s.Id == request.IntegrationServiceId, ct);
        if (!serviceExists)
            return Result.Failure<Guid>("Entegrasyon servisi bulunamadı.");

        var integration = new FirmIntegration
        {
            Id = Guid.NewGuid(),
            FirmId = request.FirmId,
            IntegrationServiceId = request.IntegrationServiceId,
            Name = request.Name,
            Credentials = request.Credentials,
            Settings = request.Settings,
            IsActive = true,
            ContractNumber = request.ContractNumber,
            StartDate = AsUtc(request.StartDate),
            EndDate = AsUtc(request.EndDate),
            Status = request.Status,
            Terms = request.Terms,
            ContactName = request.ContactName,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            DocumentUrl = request.DocumentUrl,
            CreatedAt = DateTime.UtcNow
        };

        _db.FirmIntegrations.Add(integration);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(integration.Id);
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
