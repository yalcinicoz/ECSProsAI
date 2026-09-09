using ECSPros.Storefront.Application.Queries.GetReviewsForModeration;

namespace ECSPros.Api.Grid;

/// <summary>Yorum moderasyonu Excel kolonları (ürün kodu kilitli).</summary>
public static class ReviewModerationExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ReviewModerationExportRow>> All = new GridExportColumn<ReviewModerationExportRow>[]
    {
        new("productCode", "Ürün Kodu", r => r.ProductCode, Locked: true),
        new("memberName", "Üye", r => r.MemberName),
        new("rating", "Puan", r => r.Rating),
        new("topic", "Konu", r => r.Topic),
        new("text", "Yorum", r => r.Text),
        new("status", "Durum", r => ReviewModerationGrid.StatusLabel(r.Status)),
        new("rejectReason", "Ret Sebebi", r => r.RejectReason),
        new("createdAt", "Yazılma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("moderatedAt", "Moderasyon", r => r.ModeratedAt.HasValue ? GridExportWriter.ToIstanbul(r.ModeratedAt.Value) : null),
    };
}
