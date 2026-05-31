using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateAxisSubAttribute;

public record UpdateAxisSubAttributeCommand(
    Guid Id,
    bool IsRequired,
    int SortOrder
) : IRequest<Result<bool>>;

public class UpdateAxisSubAttributeCommandHandler : IRequestHandler<UpdateAxisSubAttributeCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateAxisSubAttributeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateAxisSubAttributeCommand request, CancellationToken ct)
    {
        var entry = await _db.ProductGroupAxisSubAttributes
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (entry is null)
            return Result.Failure<bool>("Eksen alt özelliği bulunamadı.");

        entry.IsRequired = request.IsRequired;
        entry.SortOrder = request.SortOrder;
        entry.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
