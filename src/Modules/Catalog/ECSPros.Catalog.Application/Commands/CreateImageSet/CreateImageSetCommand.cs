using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.CreateImageSet;

public record CreateImageSetCommand(
    string Code,
    string Name,
    bool IsDefault,
    Guid? FallbackSetId,
    int SortPriority) : IRequest<Result<Guid>>;

public class CreateImageSetCommandHandler : IRequestHandler<CreateImageSetCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateImageSetCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateImageSetCommand request, CancellationToken ct)
    {
        var imageSet = new ImageSet
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            IsDefault = request.IsDefault,
            FallbackSetId = request.FallbackSetId,
            SortPriority = request.SortPriority,
            IsActive = true
        };

        _db.ImageSets.Add(imageSet);
        await _db.SaveChangesAsync(ct);
        return Result.Success(imageSet.Id);
    }
}
