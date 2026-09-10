using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Return : BaseEntity
{
    /// <summary>Geçici legacy MySQL importunda opiadesiparisler.Id.</summary>
    public int? LegacyReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid MemberId { get; set; }
    /// <summary>İade planı R1 (2026-09-10): <see cref="ReturnConstants.TypeUndelivered"/> (Teslimatsız İade) |
    /// <see cref="ReturnConstants.TypeCustomer"/> (Müşteri İadesi). Eski kayıtlarda legacy_type_N kalmış olabilir
    /// (aktarım düzeltmesi bu planın dışında).</summary>
    public string ReturnType { get; set; } = string.Empty;
    public string? CustomerNotes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ReturnCargoFirmId { get; set; }
    public string? ReturnTrackingNumber { get; set; }
    public DateTime? ReturnCargoSentAt { get; set; }
    public DateTime? ReturnCargoReceivedAt { get; set; }
    public string? InspectionNotes { get; set; }
    public DateTime? InspectionCompletedAt { get; set; }
    public Guid? InspectionCompletedBy { get; set; }
    public string RefundMethod { get; set; } = string.Empty;
    /// <summary>pending | completed | not_applicable (İade planı R7-R10: para iadesi hesaplanmaz; nedeni
    /// <see cref="RefundNotApplicableReason"/>).</summary>
    public string RefundStatus { get; set; } = string.Empty;
    /// <summary>cod_not_collected | marketplace | unpaid | already_refunded — <c>IadeOdemeKurali</c> neden kodu.</summary>
    public string? RefundNotApplicableReason { get; set; }
    public decimal RefundAmount { get; set; }
    /// <summary>E8: paketi anlaşmalı kargoya kodla bırakma için üretilen iade kodu
    /// (IAD-XXXXXX). Gerçek kargo entegrasyonuna (H2) dek takip numarasından ayrı tutulur.</summary>
    public string? CargoReturnCode { get; set; }
    /// <summary>E8: üyenin talep sırasında yüklediği görsellerin URL'leri (/media/returns/...).</summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>Y3 (2026-09-09): iadenin KANALI siparişten gelir (iade kaydında kanal kolonu yok).
    /// Navigasyon yalnız kapsam filtresi ve raporlama için; FK (OrderId) zaten vardı, migration gerekmez.</summary>
    public Order Order { get; set; } = null!;

    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    public ICollection<ReturnRefund> Refunds { get; set; } = new List<ReturnRefund>();
}
