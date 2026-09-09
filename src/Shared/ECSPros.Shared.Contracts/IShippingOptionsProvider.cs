namespace ECSPros.Shared.Contracts;

/// <summary>Kanalın kargo ayarları — panel Kanallar ekranı yazar (FirmPlatform.Settings jsonb).</summary>
/// <param name="Fee">Sabit kargo bedeli (0 = kargo ücretsiz).</param>
/// <param name="FreeThreshold">Ücretsiz kargo sepet eşiği (0 = eşik yok).</param>
public record ShippingOptions(decimal Fee, decimal FreeThreshold)
{
    public static readonly ShippingOptions Varsayilan = new(0m, 0m);
}

/// <summary>
/// 2026-09-09: kanal bazlı kargo ücreti/eşiği. Kural <see cref="KargoUcretiKurali"/>'nda;
/// bu port yalnız kanalın ayarını verir (IPaymentOptionsProvider kalıbı).
/// </summary>
public interface IShippingOptionsProvider
{
    Task<ShippingOptions> GetAsync(Guid firmPlatformId, CancellationToken ct = default);
}
