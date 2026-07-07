using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.CreateMannequin;

public record CreateMannequinCommand(
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
    string? Notes) : IRequest<Result<Guid>>;

public class CreateMannequinCommandHandler : IRequestHandler<CreateMannequinCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateMannequinCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateMannequinCommand request, CancellationToken ct)
    {
        var mannequin = new Mannequin
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Gender = request.Gender,
            HeightCm = request.HeightCm,
            WeightKg = request.WeightKg,
            ChestCm = request.ChestCm,
            WaistCm = request.WaistCm,
            HipCm = request.HipCm,
            DefaultWornSize = request.DefaultWornSize,
            Notes = request.Notes,
            IsActive = true
        };

        _db.Mannequins.Add(mannequin);
        await _db.SaveChangesAsync(ct);
        return Result.Success(mannequin.Id);
    }
}
