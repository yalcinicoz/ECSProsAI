using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateProductGroup;

public record CreateProductGroupCommand(
    Dictionary<string, string> NameI18n,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateProductGroupCommandHandler : IRequestHandler<CreateProductGroupCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateProductGroupCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateProductGroupCommand request, CancellationToken ct)
    {
        var code = SlugHelper.FromNameI18n(request.NameI18n);
        if (string.IsNullOrEmpty(code))
            return Result.Failure<Guid>("Ad alanından geçerli bir kod üretilemedi.");

        // Benzersizlik kontrolü: varsa sonuna sayı ekle
        var baseCode = code;
        var suffix = 2;
        while (await _db.ProductGroups.AnyAsync(pg => pg.Code == code, ct))
            code = $"{baseCode}_{suffix++}";

        var group = new ProductGroup
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameI18n = request.NameI18n,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }
}
