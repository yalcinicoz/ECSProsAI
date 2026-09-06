using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProductGroupAttribute;

public record UpdateProductGroupAttributeCommand(
    Guid Id,
    bool IsVariant,
    bool IsRequired,
    int SortOrder,
    Guid? DefaultAttributeValueId = null
) : IRequest<Result<bool>>;

public class UpdateProductGroupAttributeCommandHandler : IRequestHandler<UpdateProductGroupAttributeCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateProductGroupAttributeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateProductGroupAttributeCommand request, CancellationToken ct)
    {
        var attr = await _db.ProductGroupAttributes
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (attr is null)
            return Result.Failure<bool>("Özellik bulunamadı.");

        if (request.DefaultAttributeValueId is { } dv
            && !await _db.AttributeValues.AnyAsync(v => v.Id == dv && v.AttributeTypeId == attr.AttributeTypeId, ct))
            return Result.Failure<bool>("Varsayılan değer bu özellik tipine ait değil.");

        attr.IsVariant = request.IsVariant;
        attr.IsRequired = request.IsRequired;
        attr.SortOrder = request.SortOrder;
        attr.DefaultAttributeValueId = request.DefaultAttributeValueId;
        attr.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
