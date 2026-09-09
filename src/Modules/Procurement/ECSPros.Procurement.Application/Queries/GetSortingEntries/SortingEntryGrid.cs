using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Procurement.Application.Queries.GetSortingEntries;

/// <summary>
/// Sayım kayıtları DataGrid şeması (2026-09-09, tur 12).
///
/// <para>Sütun sınırları (kasıtlı): ürün adı / SKU / barkod CATALOG modülünde çözülür (varyant
/// gevşek referanstır) → sıralanamaz ve şemada yok; ekranın arama kutusu bu alanlarda ürün
/// çözümlemesiyle çalışır (handler'da özel işlem, "ürün adı" kalıbı — Stoklar/Mal Kabul ile aynı).
/// Süzgeçler kayıt alanları üzerinden: miktar, maliyet, etiket, yerleştirme durumu, parti.</para>
///
/// <para><c>etiketEksik</c>: etiketi basılmamış kayıt — depo "hangi desteler basılmadı" diye
/// sorduğunda tek süzgeçle yanıt verir. <c>partisiz</c> (İ2): partiye bağlanmamış sayım.</para>
/// </summary>
public static class SortingEntryGrid
{
    public static readonly string[] PutawayStatuses = { "pending", "placed" };

    public static readonly GridSchema<SortingEntry> Schema = new GridSchema<SortingEntry>()
        .Enum("putawayStatus", e => e.PutawayStatus, PutawayStatuses)
        .Bool("labelPrinted", e => e.LabelPrinted)
        .Bool("etiketEksik", e => !e.LabelPrinted)
        .Bool("partisiz", e => e.ReceiptBatchId == null)
        .Bool("maliyetli", e => e.UnitCost != null)
        .Bool("satista", e => e.OnSaleAt != null)
        .Number("quantity", e => e.Quantity)
        .Number("unitCost", e => e.UnitCost)
        .Number("labelCount", e => e.LabelCount)
        .Guid("batchId", e => e.ReceiptBatchId)
        .Guid("variantId", e => e.VariantId)
        .Guid("placedBinId", e => e.PlacedBinId)
        .Date("createdAt", e => e.CreatedAt)
        .Date("placedAt", e => e.PlacedAt)
        .Date("onSaleAt", e => e.OnSaleAt)
        .Sort("quantity", e => e.Quantity)
        .Sort("unitCost", e => e.UnitCost)
        .Sort("labelCount", e => e.LabelCount)
        .Sort("labelPrinted", e => e.LabelPrinted)
        .Sort("putawayStatus", e => e.PutawayStatus)
        .Sort("createdAt", e => e.CreatedAt)
        .Sort("placedAt", e => e.PlacedAt)
        .DefaultSort(e => e.CreatedAt)        // en yeni sayım üstte (ekranın eski sırası)
        .TieBreaker(e => e.Id);
}
