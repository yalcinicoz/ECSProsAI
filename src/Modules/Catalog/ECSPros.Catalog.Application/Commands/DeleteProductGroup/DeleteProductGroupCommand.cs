using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteProductGroup;

public record DeleteProductGroupCommand(Guid Id, Guid DeletedBy) : IRequest<Result<bool>>;

public class DeleteProductGroupCommandHandler : IRequestHandler<DeleteProductGroupCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteProductGroupCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteProductGroupCommand request, CancellationToken ct)
    {
        var group = await _db.ProductGroups
            .Include(g => g.Attributes)
            .Include(g => g.AxisSubAttributes)
            .FirstOrDefaultAsync(g => g.Id == request.Id, ct);

        if (group is null)
            return Result.Failure<bool>("Ürün grubu bulunamadı.");

        var hasProducts = await _db.Products.AnyAsync(p => p.ProductGroupId == request.Id, ct);
        if (hasProducts)
            return Result.Failure<bool>("Bu gruba atanmış ürünler bulunduğu için silinemez.");

        var now = DateTime.UtcNow;

        foreach (var attr in group.Attributes)
        {
            attr.IsDeleted = true;
            attr.DeletedAt = now;
            attr.DeletedBy = request.DeletedBy;
        }

        foreach (var sub in group.AxisSubAttributes)
        {
            sub.IsDeleted = true;
            sub.DeletedAt = now;
            sub.DeletedBy = request.DeletedBy;
        }

        group.IsDeleted = true;
        group.DeletedAt = now;
        group.DeletedBy = request.DeletedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
