using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateProductGroup;

/// <param name="CopyAttributesFromGroupId">Dolu ise kaynak grubun özellik şablonu (grup özellikleri: varyant ekseni /
/// ana eksen / zorunlu / sıra / varsayılan değer + eksen alt özellikleri) yeni gruba kopyalanır (2026-09-07).</param>
public record CreateProductGroupCommand(
    Dictionary<string, string> NameI18n,
    int SortOrder,
    Guid? CopyAttributesFromGroupId = null
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

        if (request.CopyAttributesFromGroupId is { } sourceId)
        {
            var sourceExists = await _db.ProductGroups.AnyAsync(pg => pg.Id == sourceId, ct);
            if (!sourceExists)
                return Result.Failure<Guid>("Özellikleri kopyalanacak kaynak ürün grubu bulunamadı.");

            var now = DateTime.UtcNow;
            var sourceAttrs = await _db.ProductGroupAttributes.AsNoTracking()
                .Where(a => a.ProductGroupId == sourceId)
                .ToListAsync(ct);
            foreach (var a in sourceAttrs)
            {
                _db.ProductGroupAttributes.Add(new ProductGroupAttribute
                {
                    Id = Guid.NewGuid(),
                    ProductGroupId = group.Id,
                    AttributeTypeId = a.AttributeTypeId,
                    IsVariant = a.IsVariant,
                    IsRequired = a.IsRequired,
                    IsPrimaryAxis = a.IsPrimaryAxis,
                    SortOrder = a.SortOrder,
                    DefaultAttributeValueId = a.DefaultAttributeValueId,
                    CreatedAt = now
                });
            }

            var sourceSubs = await _db.ProductGroupAxisSubAttributes.AsNoTracking()
                .Where(s => s.ProductGroupId == sourceId)
                .ToListAsync(ct);
            foreach (var s in sourceSubs)
            {
                _db.ProductGroupAxisSubAttributes.Add(new ProductGroupAxisSubAttribute
                {
                    Id = Guid.NewGuid(),
                    ProductGroupId = group.Id,
                    AxisAttributeTypeId = s.AxisAttributeTypeId,
                    SubAttributeTypeId = s.SubAttributeTypeId,
                    IsRequired = s.IsRequired,
                    SortOrder = s.SortOrder,
                    CreatedAt = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }
}
