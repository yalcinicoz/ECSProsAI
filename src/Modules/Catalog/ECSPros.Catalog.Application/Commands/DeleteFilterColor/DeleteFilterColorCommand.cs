using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteFilterColor;

public record DeleteFilterColorCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteFilterColorCommandHandler : IRequestHandler<DeleteFilterColorCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteFilterColorCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteFilterColorCommand request, CancellationToken cancellationToken)
    {
        var color = await _db.FilterColors.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (color is null)
            return Result.Failure<bool>("Filter color not found.");

        var usedCount = await _db.AttributeValueFilterColors
            .CountAsync(m => m.FilterColorId == request.Id, cancellationToken);
        if (usedCount > 0)
            return Result.Failure<bool>($"Bu filtre rengi {usedCount} özellik değerinde kullanılıyor, silinemez.");

        color.IsDeleted = true;
        color.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
