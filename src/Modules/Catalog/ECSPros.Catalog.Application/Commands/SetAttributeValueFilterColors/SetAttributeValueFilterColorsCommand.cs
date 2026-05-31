using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetAttributeValueFilterColors;

public record SetAttributeValueFilterColorsCommand(
    Guid AttributeValueId,
    List<Guid> FilterColorIds
) : IRequest<Result<bool>>;

public class SetAttributeValueFilterColorsCommandHandler : IRequestHandler<SetAttributeValueFilterColorsCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public SetAttributeValueFilterColorsCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetAttributeValueFilterColorsCommand request, CancellationToken cancellationToken)
    {
        var attrValue = await _db.AttributeValues
            .Include(v => v.AttributeType)
            .FirstOrDefaultAsync(v => v.Id == request.AttributeValueId, cancellationToken);
        if (attrValue is null)
            return Result.Failure<bool>("Attribute value not found.");

        if (attrValue.AttributeType.RequiresFilterColor && request.FilterColorIds.Count == 0)
            return Result.Failure<bool>("Bu özellik tipi en az bir filtre rengi eşleştirmesi gerektiriyor.");

        var existing = await _db.AttributeValueFilterColors
            .Where(m => m.AttributeValueId == request.AttributeValueId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(m => m.FilterColorId).ToHashSet();
        var requestedIds = request.FilterColorIds.ToHashSet();

        // Remove mappings not in new list
        foreach (var mapping in existing.Where(m => !requestedIds.Contains(m.FilterColorId)))
        {
            mapping.IsDeleted = true;
            mapping.DeletedAt = DateTime.UtcNow;
        }

        // Add new mappings
        foreach (var colorId in requestedIds.Where(id => !existingIds.Contains(id)))
        {
            _db.AttributeValueFilterColors.Add(new AttributeValueFilterColor
            {
                Id = Guid.NewGuid(),
                AttributeValueId = request.AttributeValueId,
                FilterColorId = colorId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
