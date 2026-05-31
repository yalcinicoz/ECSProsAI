using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateAttributeValue;

public record CreateAttributeValueCommand(
    Guid AttributeTypeId,
    Dictionary<string, string> NameI18n,
    int SortOrder,
    List<Guid>? FilterColorIds = null
) : IRequest<Result<Guid>>;

public class CreateAttributeValueCommandHandler : IRequestHandler<CreateAttributeValueCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateAttributeValueCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateAttributeValueCommand request, CancellationToken ct)
    {
        var attrType = await _db.AttributeTypes.FirstOrDefaultAsync(a => a.Id == request.AttributeTypeId, ct);
        if (attrType is null)
            return Result.Failure<Guid>("Özellik tipi bulunamadı.");

        if (attrType.RequiresFilterColor && (request.FilterColorIds is null || request.FilterColorIds.Count == 0))
            return Result.Failure<Guid>("Bu özellik tipi en az bir filtre rengi eşleştirmesi gerektiriyor.");

        var existingNames = await _db.AttributeValues
            .Where(v => v.AttributeTypeId == request.AttributeTypeId)
            .Select(v => v.NameI18n)
            .ToListAsync(ct);

        foreach (var incoming in request.NameI18n)
        {
            var incomingVal = incoming.Value.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(incomingVal)) continue;

            var duplicate = existingNames.Any(existing =>
                existing.TryGetValue(incoming.Key, out var existingVal) &&
                existingVal.Trim().ToUpperInvariant() == incomingVal);

            if (duplicate)
                return Result.Failure<Guid>($"Bu değer zaten mevcut: \"{incoming.Value.Trim()}\"");
        }

        var value = new AttributeValue
        {
            Id = Guid.NewGuid(),
            AttributeTypeId = request.AttributeTypeId,
            NameI18n = request.NameI18n,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AttributeValues.Add(value);

        if (request.FilterColorIds is { Count: > 0 })
        {
            foreach (var colorId in request.FilterColorIds)
            {
                _db.AttributeValueFilterColors.Add(new AttributeValueFilterColor
                {
                    Id = Guid.NewGuid(),
                    AttributeValueId = value.Id,
                    FilterColorId = colorId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(value.Id);
    }
}
