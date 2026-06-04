using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteChannelCategory;

public record DeleteChannelCategoryCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteChannelCategoryCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteChannelCategoryCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteChannelCategoryCommand request, CancellationToken ct)
    {
        var cat = await db.ChannelCategories
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cat is null) return Result.Failure<bool>("Kanal kategorisi bulunamadı.");

        cat.IsDeleted = true;
        cat.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
