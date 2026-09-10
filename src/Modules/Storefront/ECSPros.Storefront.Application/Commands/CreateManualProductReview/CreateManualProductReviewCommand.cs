using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;

namespace ECSPros.Storefront.Application.Commands.CreateManualProductReview;

/// <summary>FAZ 15.4k (2026-09-10, eski "Ürün Yorum Ekle"): personelin panelden yorum girmesi — mağazadan/telefondan gelen
/// müşteri geri bildirimi, ithal yorumlar. Üye seçilirse ona bağlanır; seçilmezse MemberId=Guid.Empty ve yalnız görünen ad.
/// Üye-başına-tek-yorum kuralı uygulanmaz (personel girişi). <paramref name="Approve"/> → doğrudan yayında.</summary>
public record CreateManualProductReviewCommand(
    Guid FirmPlatformId, string ProductCode, Guid? MemberId, string MemberName, int Rating, string? Text, string? Topic,
    bool Approve, Guid CreatedBy) : IRequest<Result<Guid>>;

public class CreateManualProductReviewCommandHandler(IStorefrontDbContext db) : IRequestHandler<CreateManualProductReviewCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateManualProductReviewCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductCode)) return Result.Failure<Guid>("Ürün kodu gereklidir.");
        if (request.Rating is < 1 or > 5) return Result.Failure<Guid>("Puan 1 ile 5 arasında olmalıdır.");
        if (string.IsNullOrWhiteSpace(request.MemberName)) return Result.Failure<Guid>("Görünen ad gereklidir.");
        if (string.IsNullOrWhiteSpace(request.Text) && request.Rating == 0) return Result.Failure<Guid>("Yorum metni ya da puan gerekli.");

        var yorum = new ProductReview
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId ?? Guid.Empty,
            ProductCode = request.ProductCode.Trim(),
            Rating = request.Rating,
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
            Topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim(),
            MemberName = request.MemberName.Trim(),
            Status = request.Approve ? "approved" : "pending",
            ModeratedAt = request.Approve ? DateTime.UtcNow : null,
            CreatedBy = request.CreatedBy,
        };
        db.ProductReviews.Add(yorum);
        await db.SaveChangesAsync(ct);
        return Result.Success(yorum.Id);
    }
}
