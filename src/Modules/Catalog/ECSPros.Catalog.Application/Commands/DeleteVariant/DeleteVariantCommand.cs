using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteVariant;

public record DeleteVariantCommand(Guid VariantId, Guid DeletedBy) : IRequest<Result<bool>>;

public class DeleteVariantCommandHandler : IRequestHandler<DeleteVariantCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteVariantCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteVariantCommand request, CancellationToken ct)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, ct);

        if (variant is null)
            return Result.Failure<bool>("Varyant bulunamadı.");

        variant.IsDeleted = true;
        variant.DeletedAt = DateTime.UtcNow;
        variant.DeletedBy = request.DeletedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
