using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.AddProductGroupAttribute;

public record AddProductGroupAttributeCommand(
    Guid ProductGroupId,
    Guid AttributeTypeId,
    bool IsVariant,
    bool IsRequired,
    int SortOrder
) : IRequest<Result<Guid>>;

public class AddProductGroupAttributeCommandHandler : IRequestHandler<AddProductGroupAttributeCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public AddProductGroupAttributeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(AddProductGroupAttributeCommand request, CancellationToken ct)
    {
        var groupExists = await _db.ProductGroups.AnyAsync(pg => pg.Id == request.ProductGroupId, ct);
        if (!groupExists)
            return Result.Failure<Guid>("Ürün grubu bulunamadı.");

        var attrExists = await _db.AttributeTypes.AnyAsync(a => a.Id == request.AttributeTypeId, ct);
        if (!attrExists)
            return Result.Failure<Guid>("Özellik tipi bulunamadı.");

        var existing = await _db.ProductGroupAttributes
            .AnyAsync(pga => pga.ProductGroupId == request.ProductGroupId && pga.AttributeTypeId == request.AttributeTypeId, ct);
        if (existing)
            return Result.Failure<Guid>("Bu özellik zaten bu gruba ekli.");

        var pga = new ProductGroupAttribute
        {
            Id = Guid.NewGuid(),
            ProductGroupId = request.ProductGroupId,
            AttributeTypeId = request.AttributeTypeId,
            IsVariant = request.IsVariant,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductGroupAttributes.Add(pga);
        await _db.SaveChangesAsync(ct);

        return Result.Success(pga.Id);
    }
}
