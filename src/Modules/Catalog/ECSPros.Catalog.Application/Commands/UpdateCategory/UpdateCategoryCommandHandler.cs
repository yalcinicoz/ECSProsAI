using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, Result<bool>>
{
    private readonly ICatalogDbContext _context;

    public UpdateCategoryCommandHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category is null)
            return Result.Failure<bool>("Kategori bulunamadı.");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _context.Categories
                .AnyAsync(c => c.Id == request.ParentId, cancellationToken);
            if (!parentExists)
                return Result.Failure<bool>("Üst kategori bulunamadı.");
        }

        category.NameI18n = request.NameI18n;
        category.ParentId = request.ParentId;
        category.FillType = request.FillType;
        category.IsActive = request.IsActive;
        category.SortOrder = request.SortOrder;
        category.UpdatedBy = request.UpdatedBy;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
