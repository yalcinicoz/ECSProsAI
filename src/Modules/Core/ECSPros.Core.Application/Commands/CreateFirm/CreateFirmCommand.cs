using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Commands.CreateFirm;

public record CreateFirmCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    string PriceType,
    decimal? PriceMultiplier
) : IRequest<Result<Guid>>;

public class CreateFirmCommandHandler : IRequestHandler<CreateFirmCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;

    public CreateFirmCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateFirmCommand request, CancellationToken ct)
    {
        var firm = new Firm
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToLowerInvariant(),
            NameI18n = request.NameI18n,
            TaxOffice = request.TaxOffice,
            TaxNumber = request.TaxNumber,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsMain = request.IsMain,
            PriceType = request.PriceType,
            PriceMultiplier = request.PriceMultiplier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Firms.Add(firm);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(firm.Id);
    }
}
