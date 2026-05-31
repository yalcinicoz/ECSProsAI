using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.AddAxisSubAttribute;

public record AddAxisSubAttributeCommand(
    Guid ProductGroupId,
    Guid AxisAttributeTypeId,
    Guid SubAttributeTypeId,
    bool IsRequired,
    int SortOrder
) : IRequest<Result<Guid>>;

public class AddAxisSubAttributeCommandHandler : IRequestHandler<AddAxisSubAttributeCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public AddAxisSubAttributeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(AddAxisSubAttributeCommand request, CancellationToken ct)
    {
        // Eksenin bu grupta gerçekten varyant ekseni olarak tanımlı olduğunu doğrula
        var axisIsVariant = await _db.ProductGroupAttributes.AnyAsync(
            a => a.ProductGroupId == request.ProductGroupId
              && a.AttributeTypeId == request.AxisAttributeTypeId
              && a.IsVariant, ct);

        if (!axisIsVariant)
            return Result.Failure<Guid>("Bu özellik bu grupta varyant ekseni olarak tanımlı değil.");

        // Aynı alt özellik daha önce eklenmiş mi?
        var alreadyExists = await _db.ProductGroupAxisSubAttributes.AnyAsync(
            x => x.ProductGroupId == request.ProductGroupId
              && x.AxisAttributeTypeId == request.AxisAttributeTypeId
              && x.SubAttributeTypeId == request.SubAttributeTypeId, ct);

        if (alreadyExists)
            return Result.Failure<Guid>("Bu alt özellik zaten eklenmiş.");

        // Bir özellik hem eksen hem alt özellik olamaz (döngü engeli)
        if (request.AxisAttributeTypeId == request.SubAttributeTypeId)
            return Result.Failure<Guid>("Bir özellik kendi ekseninin alt özelliği olamaz.");

        var entry = new ProductGroupAxisSubAttribute
        {
            Id = Guid.NewGuid(),
            ProductGroupId = request.ProductGroupId,
            AxisAttributeTypeId = request.AxisAttributeTypeId,
            SubAttributeTypeId = request.SubAttributeTypeId,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductGroupAxisSubAttributes.Add(entry);
        await _db.SaveChangesAsync(ct);

        return Result.Success(entry.Id);
    }
}
