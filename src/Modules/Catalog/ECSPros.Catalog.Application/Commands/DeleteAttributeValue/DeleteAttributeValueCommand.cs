using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteAttributeValue;

public record DeleteAttributeValueCommand(Guid ValueId, Guid DeletedBy) : IRequest<Result<bool>>;

public class DeleteAttributeValueCommandHandler : IRequestHandler<DeleteAttributeValueCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteAttributeValueCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteAttributeValueCommand request, CancellationToken ct)
    {
        var value = await _db.AttributeValues.FirstOrDefaultAsync(v => v.Id == request.ValueId, ct);
        if (value is null)
            return Result.Failure<bool>("Özellik değeri bulunamadı.");

        var usedInProducts = await _db.ProductAttributes
            .AnyAsync(pa => pa.AttributeValueId == request.ValueId, ct);
        if (!usedInProducts)
            usedInProducts = await _db.ProductVariantAttributes
                .AnyAsync(pva => pva.AttributeValueId == request.ValueId, ct);

        if (usedInProducts)
            return Result.Failure<bool>("Bu değer ürünlerde kullanılıyor; silinemiyor.");

        value.IsDeleted = true;
        value.DeletedAt = DateTime.UtcNow;
        value.DeletedBy = request.DeletedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
