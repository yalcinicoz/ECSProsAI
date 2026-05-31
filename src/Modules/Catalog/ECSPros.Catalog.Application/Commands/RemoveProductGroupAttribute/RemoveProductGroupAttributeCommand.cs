using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.RemoveProductGroupAttribute;

public record RemoveProductGroupAttributeCommand(Guid Id) : IRequest<Result<bool>>;

public class RemoveProductGroupAttributeCommandHandler : IRequestHandler<RemoveProductGroupAttributeCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public RemoveProductGroupAttributeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RemoveProductGroupAttributeCommand request, CancellationToken ct)
    {
        var attr = await _db.ProductGroupAttributes
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (attr is null)
            return Result.Failure<bool>("Özellik bulunamadı.");

        attr.IsDeleted = true;
        attr.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}
