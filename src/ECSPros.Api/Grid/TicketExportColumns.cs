using ECSPros.Crm.Application.Tickets.Queries;

namespace ECSPros.Api.Grid;

/// <summary>Müşteri İlişkileri (talep/şikayet) Excel kolonları (anahtarlar panel DataGrid kolon anahtarlarıyla aynı; takip no + durum kilitli).</summary>
public static class TicketExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<TicketExportRow>> All = new GridExportColumn<TicketExportRow>[]
    {
        new("trackingNo", "Takip No", r => r.TrackingNo, Locked: true),
        new("type", "Tür", r => TicketGrid.TypeLabel(r.Type)),
        new("subject", "Konu", r => r.SubjectName),
        new("status", "Durum", r => r.StatusName, Locked: true),
        new("customer", "Müşteri", r => r.CustomerName),
        new("customerPhone", "Müşteri Telefon", r => r.CustomerPhone),
        new("caller", "Arayan", r => r.CallerName),
        new("callerPhone", "Arayan Telefon", r => r.CallerPhone),
        new("orderNumber", "Sipariş No", r => r.OrderNumber),
        new("createdAt", "Kayıt Tarihi", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("createdByName", "Kayıt Açan", r => r.CreatedByName),
        new("lastActivityAt", "Son İşlem", r => GridExportWriter.ToIstanbul(r.LastActivityAt)),
        new("updatedByName", "Son İşlem Yapan", r => r.UpdatedByName),
        new("activityCount", "İşlem Sayısı", r => r.ActivityCount),
        new("hidden", "Gizli", r => r.IsHidden ? "Evet" : "Hayır"),
        new("body", "İçerik", r => r.BodyText),
    };
}
