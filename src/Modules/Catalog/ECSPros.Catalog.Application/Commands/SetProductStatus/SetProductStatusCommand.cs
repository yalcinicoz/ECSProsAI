using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetProductStatus;

public record SetProductStatusCommand(Guid Id, bool IsActive) : IRequest<Result<bool>>;

public class SetProductStatusCommandHandler : IRequestHandler<SetProductStatusCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public SetProductStatusCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetProductStatusCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (product is null)
            return Result.Failure<bool>("Ürün bulunamadı.");

        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
