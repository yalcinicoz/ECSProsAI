using ECSPros.Order.Application.Queries.GetReturns;

namespace ECSPros.Api.Grid;

/// <summary>İadeler Excel kolonları (iade no + durum kilitli).</summary>
public static class ReturnExportColumns
{
    private static object? Tarih(DateTime? d) => d.HasValue ? GridExportWriter.ToIstanbul(d.Value) : null;

    public static readonly IReadOnlyList<GridExportColumn<ReturnExportRow>> All = new GridExportColumn<ReturnExportRow>[]
    {
        new("returnNumber", "İade No", r => r.ReturnNumber, Locked: true),
        new("orderNumber", "Sipariş No", r => r.OrderNumber),
        new("createdAt", "Tarih", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("returnType", "Tip", r => ReturnGrid.TypeLabel(r.ReturnType)),
        new("status", "Durum", r => ReturnGrid.StatusLabel(r.Status), Locked: true),
        new("refundMethod", "Geri Ödeme Yöntemi", r => r.RefundMethod),
        new("refundStatus", "Geri Ödeme Durumu", r => r.RefundStatus),
        new("refundAmount", "Tutar", r => r.RefundAmount),
        new("trackingNumber", "Kargo Takip No", r => r.ReturnTrackingNumber),
        new("cargoReturnCode", "Kargo İade Kodu", r => r.CargoReturnCode),
        new("cargoSentAt", "Kargoya Verildi", r => Tarih(r.ReturnCargoSentAt)),
        new("cargoReceivedAt", "Teslim Alındı", r => Tarih(r.ReturnCargoReceivedAt)),
        new("inspectionCompletedAt", "Kontrol Tamamlandı", r => Tarih(r.InspectionCompletedAt)),
        new("customerNotes", "Müşteri Notu", r => r.CustomerNotes),
        new("inspectionNotes", "Kontrol Notu", r => r.InspectionNotes),
    };
}
