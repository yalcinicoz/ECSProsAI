using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProductGroup;

public record UpdateProductGroupCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    int SortOrder,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateProductGroupCommandHandler : IRequestHandler<UpdateProductGroupCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateProductGroupCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateProductGroupCommand request, CancellationToken ct)
    {
        var group = await _db.ProductGroups.FirstOrDefaultAsync(pg => pg.Id == request.Id, ct);
        if (group is null)
            return Result.Failure<bool>("Ürün grubu bulunamadı.");

        group.NameI18n = request.NameI18n;
        group.ParentId = request.ParentId;
        group.SortOrder = request.SortOrder;
        group.IsActive = request.IsActive;
        group.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
