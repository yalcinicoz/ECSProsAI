namespace ECSPros.Order.Application.Services;

/// <summary>Core <c>core_payment_methods</c> kaydının Id'sini koda göre çözer (salt-okunur; FirmResolver kalıbı).
/// İade planı §2.6: teslimde kapıda ödeme tahsilat satırı ve PayTR/mock tahsilat satırı bu Id ile yazılır;
/// bulunamazsa <see cref="Guid.Empty"/> (satır yine yazılır — kural tahsilat toplamına bakar, yönteme değil).</summary>
public interface IPaymentMethodResolver
{
    Task<Guid?> GetIdByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Siparişin <c>PaymentMethod</c> değerini (kart | kapida-nakit | kapida-kart) Core koduna çevirir.</summary>
    static string CoreCodeFor(string? orderPaymentMethod) => orderPaymentMethod switch
    {
        "kapida-nakit" or "kapida-kart" => "cash_on_delivery",
        "havale" => "bank_transfer",
        "cuzdan" => "wallet",
        _ => "credit_card",
    };
}
