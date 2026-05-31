using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetProductAxisSubAttributeValues;

public record AxisSubAttributeValueItem(Guid AttributeValueId, Guid SubAttributeTypeId, string Value);

public record SetProductAxisSubAttributeValuesCommand(
    Guid ProductId,
    List<AxisSubAttributeValueItem> Values
) : IRequest<Result<bool>>;

public class SetProductAxisSubAttributeValuesCommandHandler
    : IRequestHandler<SetProductAxisSubAttributeValuesCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public SetProductAxisSubAttributeValuesCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetProductAxisSubAttributeValuesCommand request, CancellationToken ct)
    {
        var existing = await _db.ProductAxisSubAttributeValues
            .Where(v => v.ProductId == request.ProductId)
            .ToListAsync(ct);

        var incoming = request.Values
            .Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .ToList();

        // Silinecekler: artık listede olmayan veya boş değerli kayıtlar
        var incomingKeys = incoming
            .Select(v => (v.AttributeValueId, v.SubAttributeTypeId))
            .ToHashSet();

        var toDelete = existing
            .Where(e => !incomingKeys.Contains((e.AttributeValueId, e.SubAttributeTypeId)))
            .ToList();

        foreach (var d in toDelete)
        {
            d.IsDeleted = true;
            d.DeletedAt = DateTime.UtcNow;
        }

        // Upsert: var olanı güncelle, yoksa ekle
        foreach (var item in incoming)
        {
            var found = existing.FirstOrDefault(
                e => e.AttributeValueId == item.AttributeValueId
                  && e.SubAttributeTypeId == item.SubAttributeTypeId);

            if (found is not null)
            {
                if (found.IsDeleted)
                {
                    found.IsDeleted = false;
                    found.DeletedAt = null;
                    found.DeletedBy = null;
                }
                found.Value = item.Value.Trim();
                found.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.ProductAxisSubAttributeValues.Add(new ProductAxisSubAttributeValue
                {
                    Id = Guid.NewGuid(),
                    ProductId = request.ProductId,
                    AttributeValueId = item.AttributeValueId,
                    SubAttributeTypeId = item.SubAttributeTypeId,
                    Value = item.Value.Trim(),
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
