using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetAttributeValueProperties;

public record AttributeValuePropertyItem(Guid SubAttributeTypeId, string Value);

public record SetAttributeValuePropertiesCommand(
    Guid AttributeValueId,
    List<AttributeValuePropertyItem> Properties
) : IRequest<Result<bool>>;

public class SetAttributeValuePropertiesCommandHandler
    : IRequestHandler<SetAttributeValuePropertiesCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public SetAttributeValuePropertiesCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetAttributeValuePropertiesCommand request, CancellationToken ct)
    {
        var valueExists = await _db.AttributeValues.AnyAsync(v => v.Id == request.AttributeValueId, ct);
        if (!valueExists)
            return Result.Failure<bool>("Özellik değeri bulunamadı.");

        // Mevcut özellikleri yükle
        var existing = await _db.AttributeValueProperties
            .Where(p => p.AttributeValueId == request.AttributeValueId)
            .ToListAsync(ct);

        foreach (var item in request.Properties)
        {
            var trimmed = item.Value.Trim();
            var prop = existing.FirstOrDefault(p => p.SubAttributeTypeId == item.SubAttributeTypeId);

            if (prop is null)
            {
                // Yeni ekle (boş değer gönderilmişse atla)
                if (string.IsNullOrEmpty(trimmed)) continue;

                _db.AttributeValueProperties.Add(new AttributeValueProperty
                {
                    Id = Guid.NewGuid(),
                    AttributeValueId = request.AttributeValueId,
                    SubAttributeTypeId = item.SubAttributeTypeId,
                    Value = trimmed,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (string.IsNullOrEmpty(trimmed))
            {
                // Boş değer → soft delete
                prop.IsDeleted = true;
                prop.DeletedAt = DateTime.UtcNow;
            }
            else
            {
                // Güncelle
                prop.Value = trimmed;
                prop.UpdatedAt = DateTime.UtcNow;
                prop.IsDeleted = false;
                prop.DeletedAt = null;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
