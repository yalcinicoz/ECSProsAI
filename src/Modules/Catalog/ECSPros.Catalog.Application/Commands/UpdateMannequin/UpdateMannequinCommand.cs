using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateMannequin;

public record UpdateMannequinCommand(
    Guid Id,
    string? Code,
    string FirstName,
    string? LastName,
    string? Gender,
    int? HeightCm,
    int? WeightKg,
    int? ChestCm,
    int? WaistCm,
    int? HipCm,
    string? DefaultWornSize,
    bool IsActive,
    string? Notes) : IRequest<Result<bool>>;

public class UpdateMannequinCommandHandler : IRequestHandler<UpdateMannequinCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateMannequinCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateMannequinCommand request, CancellationToken ct)
    {
        var mannequin = await _db.Mannequins.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (mannequin is null)
            return Result.Failure<bool>("Manken bulunamadı.");

        mannequin.Code = request.Code;
        mannequin.FirstName = request.FirstName;
        mannequin.LastName = request.LastName;
        mannequin.Gender = request.Gender;
        mannequin.HeightCm = request.HeightCm;
        mannequin.WeightKg = request.WeightKg;
        mannequin.ChestCm = request.ChestCm;
        mannequin.WaistCm = request.WaistCm;
        mannequin.HipCm = request.HipCm;
        mannequin.DefaultWornSize = request.DefaultWornSize;
        mannequin.IsActive = request.IsActive;
        mannequin.Notes = request.Notes;
        mannequin.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
