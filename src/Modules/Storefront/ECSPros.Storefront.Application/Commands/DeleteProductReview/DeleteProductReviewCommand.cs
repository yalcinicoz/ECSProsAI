using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteProductReview;

/// <summary>E7: üyenin kendi yorumunu silmesi — soft delete (Yorumlarım "Silinenler"
/// sekmesinde arşivlenir; aynı ürüne yeniden yorum yazılabilir).</summary>
public record DeleteProductReviewCommand(Guid MemberId, Guid ReviewId) : IRequest<Result<bool>>;

public class DeleteProductReviewCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteProductReviewCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteProductReviewCommand request, CancellationToken ct)
    {
        var yorum = await db.ProductReviews.FirstOrDefaultAsync(
            r => r.Id == request.ReviewId && r.MemberId == request.MemberId, ct);
        if (yorum is null) return Result.Failure<bool>("Yorum bulunamadı.");

        yorum.IsDeleted = true;
        yorum.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
