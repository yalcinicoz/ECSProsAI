using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateAttributeType;

public record UpdateAttributeTypeCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    int SortOrder,
    bool IsActive,
    bool RequiresFilterColor = false
) : IRequest<Result<bool>>;

public class UpdateAttributeTypeCommandHandler : IRequestHandler<UpdateAttributeTypeCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateAttributeTypeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateAttributeTypeCommand request, CancellationToken ct)
    {
        var attrType = await _db.AttributeTypes.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (attrType is null)
            return Result.Failure<bool>("Özellik tipi bulunamadı.");

        attrType.NameI18n = request.NameI18n;
        attrType.SortOrder = request.SortOrder;
        attrType.IsActive = request.IsActive;
        attrType.RequiresFilterColor = request.RequiresFilterColor;
        attrType.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
