using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.purchase_orders — Satın Alma (T1, docs/urun-tedarik-is-akisi.md §2.1).
/// HAFİF kayıt katmanı (İ2): hiçbir akışı kilitlemez; mal kabul ve ayrıştırma bu kayıt olmadan da yürür.
/// Kapanış ELLE verilir (İ3/İ4 — kesin eşleşme yoktur); "receiving" bilgi amaçlıdır.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    public string Code { get; set; } = string.Empty;              // SA-YYYYAAGG-0001
    public Guid SupplierId { get; set; }                          // accounts.current_accounts.Id (cari)
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    /// <summary>draft | ordered | receiving | closed | cancelled</summary>
    public string Status { get; set; } = "draft";
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();

    public static readonly string[] Statuses = ["draft", "ordered", "receiving", "closed", "cancelled"];
}
