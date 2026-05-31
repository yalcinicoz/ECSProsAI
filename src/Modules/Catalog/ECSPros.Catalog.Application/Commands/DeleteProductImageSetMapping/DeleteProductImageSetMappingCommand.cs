using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteProductImageSetMapping;

public record DeleteProductImageSetMappingCommand(Guid ProductId, Guid ForSetId) : IRequest<Result<bool>>;

public class DeleteProductImageSetMappingCommandHandler
    : IRequestHandler<DeleteProductImageSetMappingCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteProductImageSetMappingCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteProductImageSetMappingCommand request, CancellationToken ct)
    {
        var mapping = await _db.ProductImageSetMappings
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.ForSetId == request.ForSetId, ct);

        if (mapping is null)
            return Result.Failure<bool>("Mapping bulunamadı.");

        mapping.IsDeleted = true;
        mapping.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
