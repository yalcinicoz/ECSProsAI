using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Accounts.Application.Queries.GetSupplierSettlements;

/// <summary>
/// Hakediş satırları DataGrid şeması (2026-09-09, tur 12).
///
/// <para>Liste her zaman TEK SATICIYA kilitlidir (ekranın satıcı seçicisi) — hakediş satırı
/// satıcının parasıdır, kimse başkasının satırını görmemeli. Kilit named filtrede
/// (<c>supplierAccountId</c>) tutulur; şema süzgeçleri onun ÜZERİNE uygulanır.</para>
///
/// <para><c>katman</c> serbest metindir ("contract_group+turnover" gibi ekli olabilir) → Enum değil
/// Text; ekran "hangi katmandan geldi" sorusunu içerir-süzgeciyle yanıtlar. <c>iade</c>: ters satır
/// (ReversalOfId dolu) — iade edilen satışın hakedişini geri alan kayıt.</para>
/// </summary>
public static class SettlementLineGrid
{
    public static readonly string[] Statuses = { "pending", "available", "paid", "reversed" };

    public static readonly GridSchema<SettlementLine> Schema = new GridSchema<SettlementLine>()
        .Text("orderNumber", l => l.OrderNumber)
        .Text("sku", l => l.Sku)
        .Text("productName", l => l.ProductName)
        .Text("katman", l => l.CommissionLayer)
        .Enum("status", l => l.Status, Statuses)
        .Bool("iade", l => l.ReversalOfId != null)
        .Bool("kampanyali", l => l.CampaignId != null)
        .Bool("indirimPayiVar", l => l.CampaignDiscountShareAmount > 0)
        .Bool("defterdeVar", l => l.LedgerTransactionId != null)
        .Number("quantity", l => l.Quantity)
        .Number("grossAmount", l => l.GrossAmount)
        .Number("commissionRate", l => l.CommissionRate)
        .Number("commissionAmount", l => l.CommissionAmount)
        .Number("discountShare", l => l.CampaignDiscountShareAmount)
        .Number("netAmount", l => l.NetAmount)
        .Date("deliveredAt", l => l.DeliveredAt)
        .Date("eligibleAt", l => l.EligibleAt)
        .Date("availableAt", l => l.AvailableAt)
        .Date("paidAt", l => l.PaidAt)
        .Date("createdAt", l => l.CreatedAt)
        .Sort("orderNumber", l => l.OrderNumber)
        .Sort("sku", l => l.Sku)
        .Sort("katman", l => l.CommissionLayer)
        .Sort("status", l => l.Status)
        .Sort("quantity", l => l.Quantity)
        .Sort("grossAmount", l => l.GrossAmount)
        .Sort("commissionRate", l => l.CommissionRate)
        .Sort("commissionAmount", l => l.CommissionAmount)
        .Sort("discountShare", l => l.CampaignDiscountShareAmount)
        .Sort("netAmount", l => l.NetAmount)
        .Sort("deliveredAt", l => l.DeliveredAt)
        .Sort("eligibleAt", l => l.EligibleAt)
        .Sort("paidAt", l => l.PaidAt)
        .Sort("createdAt", l => l.CreatedAt)
        .DefaultSort(l => l.CreatedAt)   // en yeni hakediş üstte (ekranın eski sırası)
        .TieBreaker(l => l.Id);
}
