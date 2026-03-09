using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _context;

    public CreateCategoryCommandHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Categories.AnyAsync(c => c.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' kodu zaten mevcut.");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _context.Categories.AnyAsync(c => c.Id == request.ParentId, cancellationToken);
            if (!parentExists)
                return Result.Failure<Guid>("Üst kategori bulunamadı.");
        }

        var category = new Category
        {
            Code = request.Code,
            NameI18n = request.NameI18n,
            ParentId = request.ParentId,
            FillType = request.FillType,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(category.Id);
    }
}
