namespace ECSPros.Order.Domain.Entities;

/// <summary>İade sözlüğü (İade akışı planı, 2026-09-10). Etiketler <c>DurumEtiketleri</c>'nde (TEK kural).</summary>
public static class ReturnConstants
{
    // İade tipi (R1)
    public const string TypeUndelivered = "undelivered";   // Teslimatsız İade — şirket başlatır, sipariş `returned`
    public const string TypeCustomer = "customer";         // Müşteri İadesi — sipariş durumu değişmez

    // İade durumu
    public const string StatusRequested = "requested";
    public const string StatusApproved = "approved";
    public const string StatusReceived = "received";
    public const string StatusRefunded = "refunded";
    public const string StatusClosed = "closed";           // geri ödeme uygun değil → teslim alma sonrası kapanır
    public const string StatusRejected = "rejected";

    // Geri ödeme durumu
    public const string RefundPending = "pending";
    public const string RefundCompleted = "completed";
    public const string RefundNotApplicable = "not_applicable";

    // Geri ödeme yöntemi
    public const string RefundMethodCardRefund = "card_refund";
    public const string RefundMethodBankTransfer = "bank_transfer";
    public const string RefundMethodWallet = "wallet";
    public const string RefundMethodOriginalPayment = "original_payment";
    public const string RefundMethodNone = "none";         // tahsilat yok — para iadesi hesaplanmaz

    // Gönderi durumu
    public const string ShipmentReturnedToSender = "returned_to_sender";

    /// <summary>Sistem iade nedeni "Teslim Edilemedi" (core lookup <c>return_reason</c> değeri; sabit Id —
    /// Order modülü Core'u sorgulamadan kullanır; seed + migration aynı Id'yi yazar).</summary>
    public static readonly Guid UndeliveredReasonId = new("a1d3c0de-7e51-4d1e-9a9e-0000f1ade001");

    /// <summary>Siparişin ödeme yöntemine göre varsayılan geri ödeme yöntemi (İade planı §2.3/4).</summary>
    public static string RefundMethodFor(string? paymentMethod) => paymentMethod switch
    {
        "kart" => RefundMethodCardRefund,
        "kapida-nakit" or "kapida-kart" => RefundMethodBankTransfer,   // teslim sonrası tahsilat → havale ile iade
        "havale" => RefundMethodBankTransfer,
        "cuzdan" => RefundMethodWallet,
        _ => RefundMethodOriginalPayment,
    };
}
