using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.CreateAttributeType;

public record CreateAttributeTypeCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string DataType,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateAttributeTypeCommandHandler : IRequestHandler<CreateAttributeTypeCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateAttributeTypeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateAttributeTypeCommand request, CancellationToken ct)
    {
        var attr = new AttributeType
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToLowerInvariant(),
            NameI18n = request.NameI18n,
            DataType = request.DataType,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AttributeTypes.Add(attr);
        await _db.SaveChangesAsync(ct);

        return Result.Success(attr.Id);
    }
}
