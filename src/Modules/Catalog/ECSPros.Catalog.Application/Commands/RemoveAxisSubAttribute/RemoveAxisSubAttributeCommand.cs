using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.RemoveAxisSubAttribute;

public record RemoveAxisSubAttributeCommand(Guid Id) : IRequest<Result<bool>>;

public class RemoveAxisSubAttributeCommandHandler : IRequestHandler<RemoveAxisSubAttributeCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public RemoveAxisSubAttributeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RemoveAxisSubAttributeCommand request, CancellationToken ct)
    {
        var entry = await _db.ProductGroupAxisSubAttributes
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (entry is null)
            return Result.Failure<bool>("Eksen alt özelliği bulunamadı.");

        entry.IsDeleted = true;
        entry.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}
