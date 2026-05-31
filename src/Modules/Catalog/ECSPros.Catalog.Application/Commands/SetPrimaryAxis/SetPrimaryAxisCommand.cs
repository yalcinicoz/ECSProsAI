using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetPrimaryAxis;

public record SetPrimaryAxisCommand(Guid ProductGroupId, Guid AttributeId) : IRequest<Result>;

public class SetPrimaryAxisCommandHandler : IRequestHandler<SetPrimaryAxisCommand, Result>
{
    private readonly ICatalogDbContext _db;

    public SetPrimaryAxisCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result> Handle(SetPrimaryAxisCommand request, CancellationToken ct)
    {
        var target = await _db.ProductGroupAttributes
            .FirstOrDefaultAsync(a => a.Id == request.AttributeId && a.ProductGroupId == request.ProductGroupId, ct);

        if (target is null)
            return Result.Failure("Özellik bulunamadı.");

        if (!target.IsVariant)
            return Result.Failure("Sadece varyant ekseni olan özellikler ana eksen olarak işaretlenebilir.");

        var hasProducts = await _db.Products.AnyAsync(p => p.ProductGroupId == request.ProductGroupId, ct);
        if (hasProducts)
            return Result.Failure("Bu gruba ait ürünler mevcut olduğundan ana eksen değiştirilemez.");

        // Unset all existing primary axes in the group
        var all = await _db.ProductGroupAttributes
            .Where(a => a.ProductGroupId == request.ProductGroupId && a.IsPrimaryAxis)
            .ToListAsync(ct);

        foreach (var a in all)
        {
            a.IsPrimaryAxis = false;
            a.UpdatedAt = DateTime.UtcNow;
        }

        target.IsPrimaryAxis = true;
        target.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
