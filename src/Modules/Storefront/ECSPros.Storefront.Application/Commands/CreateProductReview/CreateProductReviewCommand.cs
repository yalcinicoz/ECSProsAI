using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CreateProductReview;

/// <summary>
/// E7: yorum yaz — pending doğar. Satın alma şartı ÇAĞIRANDA doğrulanır (API katmanı
/// üyenin teslim edilmiş sipariş kalemlerini Order+Catalog ile çözer; modüller arası
/// bağımlılık Application'a sokulmaz) — OrderItemId doğrulanan kalemdir.
/// Üye aynı ürüne ikinci aktif yorum yazamaz (silineni yeniden yazabilir).
/// </summary>
public record CreateProductReviewCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string ProductCode,
    Guid? OrderItemId,
    int Rating,
    string? Text,
    string MemberName) : IRequest<Result<Guid>>;

public class CreateProductReviewCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateProductReviewCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductReviewCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return Result.Failure<Guid>("Ürün kodu gereklidir.");
        if (request.Rating is < 1 or > 5)
            return Result.Failure<Guid>("Puan 1 ile 5 arasında olmalıdır.");

        var kod = request.ProductCode.Trim();
        var mevcut = await db.ProductReviews.AnyAsync(
            r => r.FirmPlatformId == request.FirmPlatformId
                 && r.MemberId == request.MemberId && r.ProductCode == kod, ct);
        if (mevcut)
            return Result.Failure<Guid>("Bu ürün için zaten bir değerlendirmeniz var.");

        var yorum = new ProductReview
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            ProductCode = kod,
            OrderItemId = request.OrderItemId,
            Rating = request.Rating,
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
            Status = "pending",
            MemberName = request.MemberName
        };
        db.ProductReviews.Add(yorum);
        await db.SaveChangesAsync(ct);
        return Result.Success(yorum.Id);
    }
}
