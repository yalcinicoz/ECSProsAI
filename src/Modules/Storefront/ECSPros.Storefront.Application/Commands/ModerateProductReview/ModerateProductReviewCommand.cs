using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.ModerateProductReview;

/// <summary>E7: yorum moderasyonu (admin) — reddedilirken neden yazılır
/// (üye Yorumlarım "Reddedilenler" sekmesinde görür).</summary>
public record ModerateProductReviewCommand(
    Guid ReviewId, bool Approve, string? RejectReason = null) : IRequest<Result<bool>>;

public class ModerateProductReviewCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<ModerateProductReviewCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ModerateProductReviewCommand request, CancellationToken ct)
    {
        var yorum = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct);
        if (yorum is null) return Result.Failure<bool>("Yorum bulunamadı.");

        yorum.Status = request.Approve ? "approved" : "rejected";
        yorum.RejectReason = request.Approve ? null
            : (string.IsNullOrWhiteSpace(request.RejectReason) ? "Yayın kriterlerine uygun değil." : request.RejectReason.Trim());
        yorum.ModeratedAt = DateTime.UtcNow;
        yorum.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
