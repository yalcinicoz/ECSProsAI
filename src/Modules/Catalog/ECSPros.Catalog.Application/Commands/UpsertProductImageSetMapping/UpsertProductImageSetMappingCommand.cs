using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpsertProductImageSetMapping;

public record UpsertProductImageSetMappingCommand(
    Guid ProductId,
    Guid ForSetId,
    Guid UseSetId) : IRequest<Result<Guid>>;

public class UpsertProductImageSetMappingCommandHandler
    : IRequestHandler<UpsertProductImageSetMappingCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public UpsertProductImageSetMappingCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(UpsertProductImageSetMappingCommand request, CancellationToken ct)
    {
        var mapping = await _db.ProductImageSetMappings
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.ForSetId == request.ForSetId, ct);

        if (mapping is null)
        {
            mapping = new ProductImageSetMapping
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                ForSetId = request.ForSetId,
                UseSetId = request.UseSetId
            };
            _db.ProductImageSetMappings.Add(mapping);
        }
        else
        {
            mapping.UseSetId = request.UseSetId;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(mapping.Id);
    }
}
