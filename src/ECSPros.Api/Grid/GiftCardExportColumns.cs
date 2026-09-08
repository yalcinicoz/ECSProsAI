using ECSPros.Order.Application.Queries.GetGiftCards;

namespace ECSPros.Api.Grid;

/// <summary>Hediye kartları Excel kolonları (kod kilitli).</summary>
public static class GiftCardExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<GiftCardExportRow>> All = new GridExportColumn<GiftCardExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("status", "Durum", r => GiftCardGrid.StatusLabel(r.Status)),
        new("originalAmount", "Tutar", r => r.OriginalAmount),
        new("remainingAmount", "Kalan", r => r.RemainingAmount),
        new("currencyCode", "Para Birimi", r => r.CurrencyCode),
        new("validFrom", "Geçerlilik Başlangıcı", r => r.ValidFrom.ToDateTime(TimeOnly.MinValue)),
        new("validUntil", "Geçerlilik Bitişi", r => r.ValidUntil.HasValue ? (object)r.ValidUntil.Value.ToDateTime(TimeOnly.MinValue) : null),
        new("isSingleUse", "Tek Kullanım", r => r.IsSingleUse ? "Evet" : "Hayır"),
        new("memberId", "Üye Id", r => r.CreatedForMemberId),
        new("orderId", "Sipariş Id", r => r.CreatedFromOrderId),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
