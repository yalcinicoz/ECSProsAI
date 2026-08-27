namespace ECSPros.Shared.Contracts;

/// <summary>Checkout'un sunucu-taraflı kupon doğrulama portu (2026-08-27, FAZ 9/9.4 güvenlik
/// kapanışı): sipariş anında kupon KODU yeniden doğrulanır ve indirim tutarı SUNUCUDA
/// hesaplanır — istemciden gelen tutar asla esas alınmaz. Promotion modülü implemente eder.</summary>
public record CouponCheckResult(Guid CouponId, decimal DiscountAmount, string? Error)
{
    public bool Gecerli => Error is null && DiscountAmount >= 0 && CouponId != Guid.Empty;
}

public interface ICouponValidator
{
    Task<CouponCheckResult> ValidateAsync(
        string code, decimal cartTotal, Guid? memberId, CancellationToken ct = default);
}
