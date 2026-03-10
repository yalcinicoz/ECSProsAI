using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateProductGroup;

public record CreateProductGroupCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateProductGroupCommandHandler : IRequestHandler<CreateProductGroupCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateProductGroupCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateProductGroupCommand request, CancellationToken ct)
    {
        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.ProductGroups.AnyAsync(pg => pg.Id == request.ParentId, ct);
            if (!parentExists)
                return Result.Failure<Guid>("Üst ürün grubu bulunamadı.");
        }

        var group = new ProductGroup
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToLowerInvariant(),
            NameI18n = request.NameI18n,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }
}
