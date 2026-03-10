using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateAttributeValue;

public record CreateAttributeValueCommand(
    Guid AttributeTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateAttributeValueCommandHandler : IRequestHandler<CreateAttributeValueCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateAttributeValueCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateAttributeValueCommand request, CancellationToken ct)
    {
        var typeExists = await _db.AttributeTypes.AnyAsync(a => a.Id == request.AttributeTypeId, ct);
        if (!typeExists)
            return Result.Failure<Guid>("Özellik tipi bulunamadı.");

        var value = new AttributeValue
        {
            Id = Guid.NewGuid(),
            AttributeTypeId = request.AttributeTypeId,
            Code = request.Code.Trim().ToLowerInvariant(),
            NameI18n = request.NameI18n,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AttributeValues.Add(value);
        await _db.SaveChangesAsync(ct);

        return Result.Success(value.Id);
    }
}
