using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateAttributeValue;

public record UpdateAttributeValueCommand(
    Guid ValueId,
    Dictionary<string, string> NameI18n,
    int SortOrder,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateAttributeValueCommandHandler : IRequestHandler<UpdateAttributeValueCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateAttributeValueCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateAttributeValueCommand request, CancellationToken ct)
    {
        var value = await _db.AttributeValues.FirstOrDefaultAsync(v => v.Id == request.ValueId, ct);
        if (value is null)
            return Result.Failure<bool>("Özellik değeri bulunamadı.");

        // Aynı özellik tipindeki diğer değerlerle isim çakışması (kendi ID'si hariç)
        var existingNames = await _db.AttributeValues
            .Where(v => v.AttributeTypeId == value.AttributeTypeId && v.Id != request.ValueId)
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
                return Result.Failure<bool>($"Bu değer zaten mevcut: \"{incoming.Value.Trim()}\"");
        }

        value.NameI18n = request.NameI18n;
        value.SortOrder = request.SortOrder;
        value.IsActive = request.IsActive;
        value.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
