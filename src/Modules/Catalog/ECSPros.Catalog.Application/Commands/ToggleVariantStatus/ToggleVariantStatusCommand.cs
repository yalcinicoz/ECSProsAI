using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.ToggleVariantStatus;

public record ToggleVariantStatusCommand(Guid VariantId, bool IsActive, Guid UpdatedBy) : IRequest<Result<bool>>;

public class ToggleVariantStatusCommandHandler : IRequestHandler<ToggleVariantStatusCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public ToggleVariantStatusCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ToggleVariantStatusCommand request, CancellationToken ct)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, ct);

        if (variant is null)
            return Result.Failure<bool>("Varyant bulunamadı.");

        variant.IsActive = request.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;
        variant.UpdatedBy = request.UpdatedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
