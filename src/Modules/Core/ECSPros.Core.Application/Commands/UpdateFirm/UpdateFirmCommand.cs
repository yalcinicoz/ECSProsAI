using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateFirm;

public record UpdateFirmCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    string PriceType,
    decimal? PriceMultiplier,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateFirmCommandHandler : IRequestHandler<UpdateFirmCommand, Result<bool>>
{
    private readonly ICoreDbContext _db;

    public UpdateFirmCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateFirmCommand request, CancellationToken ct)
    {
        var firm = await _db.Firms.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (firm is null)
            return Result.Failure<bool>("Firma bulunamadı.");

        firm.NameI18n = request.NameI18n;
        firm.TaxOffice = request.TaxOffice;
        firm.TaxNumber = request.TaxNumber;
        firm.Address = request.Address;
        firm.Phone = request.Phone;
        firm.Email = request.Email;
        firm.IsMain = request.IsMain;
        firm.PriceType = request.PriceType;
        firm.PriceMultiplier = request.PriceMultiplier;
        firm.IsActive = request.IsActive;
        firm.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success<bool>(true);
    }
}
