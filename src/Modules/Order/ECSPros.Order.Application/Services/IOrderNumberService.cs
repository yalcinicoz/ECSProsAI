namespace ECSPros.Order.Application.Services;

/// <summary>Kanala özel seriden sipariş numarası üretir. Seri yoksa kanal koduyla
/// güvenli varsayılan seri otomatik açılır. Üretim atomiktir; aynı numara iki
/// siparişe verilemez, iptal edilen numara geri kullanılmaz.</summary>
public interface IOrderNumberService
{
    Task<string> GenerateAsync(Guid firmPlatformId, CancellationToken ct = default);
}
