using ECSPros.Procurement.Application.Queries.GetReceiptBatches;

namespace ECSPros.Api.Grid;

/// <summary>Mal kabul partileri Excel kolonları (kod kilitli).</summary>
public static class ReceiptBatchExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ReceiptBatchExportRow>> All = new GridExportColumn<ReceiptBatchExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("receivedAt", "Teslim", r => GridExportWriter.ToIstanbul(r.ReceivedAt)),
        new("deliveryNoteNumber", "İrsaliye No", r => r.DeliveryNoteNumber),
        new("packageCount", "Koli", r => r.PackageCount),
        new("itemCount", "Kalem", r => r.ItemCount),
        new("linkedPoCount", "Bağlı Satın Alma", r => r.LinkedPoCount),
        new("hasInvoice", "Faturalı", r => r.HasInvoice ? "Evet" : "Hayır"),
        new("status", "Durum", r => ReceiptBatchGrid.StatusLabel(r.Status)),
        new("notes", "Not", r => r.Notes),
    };
}
