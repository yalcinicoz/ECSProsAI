using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateAttributeType;

public record CreateAttributeTypeCommand(
    Dictionary<string, string> NameI18n,
    string DataType,
    int SortOrder,
    bool RequiresFilterColor = false
) : IRequest<Result<Guid>>;

public class CreateAttributeTypeCommandHandler : IRequestHandler<CreateAttributeTypeCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateAttributeTypeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateAttributeTypeCommand request, CancellationToken ct)
    {
        var code = SlugHelper.FromNameI18n(request.NameI18n);
        if (string.IsNullOrEmpty(code))
            return Result.Failure<Guid>("Ad alanından geçerli bir kod üretilemedi.");

        // Benzersizlik kontrolü: varsa sonuna sayı ekle
        var baseCode = code;
        var suffix = 2;
        while (await _db.AttributeTypes.AnyAsync(a => a.Code == code, ct))
            code = $"{baseCode}_{suffix++}";

        var attr = new AttributeType
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameI18n = request.NameI18n,
            DataType = request.DataType,
            SortOrder = request.SortOrder,
            IsActive = true,
            RequiresFilterColor = request.RequiresFilterColor,
            CreatedAt = DateTime.UtcNow
        };

        _db.AttributeTypes.Add(attr);
        await _db.SaveChangesAsync(ct);

        return Result.Success(attr.Id);
    }
}
