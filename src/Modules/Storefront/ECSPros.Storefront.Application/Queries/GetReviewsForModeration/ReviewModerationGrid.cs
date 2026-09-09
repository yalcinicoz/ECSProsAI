using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetReviewsForModeration;

/// <summary>
/// Yorum moderasyonu DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + durum sekmesi +
/// global arama (yorum metni, ürün kodu, üye adı).
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — kapsam dışı kanalın yorumu listede, sayımda ve
/// Excel'de GÖRÜNMEZ. Kapsam liste ucunda <c>GridRequestParser.Parse(..., kanalKisiti)</c> ile,
/// export'ta <c>body.ToGridRequest(kanalKisiti)</c> ile taşınır.</para>
/// </summary>
public static class ReviewModerationGrid
{
    public static readonly string[] Statuses = { "pending", "approved", "rejected" };

    public static readonly GridSchema<ProductReview> Schema = new GridSchema<ProductReview>()
        .Kanal(r => r.FirmPlatformId)   // Y3 (K2)
        .Text("productCode", r => r.ProductCode)
        .Text("memberName", r => r.MemberName)
        .Text("text", r => r.Text)
        .Text("rejectReason", r => r.RejectReason)
        .Text("topic", r => r.Topic)
        .Enum("status", r => r.Status, Statuses)
        .Number("rating", r => r.Rating)
        .Bool("hasText", r => r.Text != null && r.Text != "")
        .Bool("moderated", r => r.ModeratedAt != null)
        .Date("createdAt", r => r.CreatedAt)
        .Date("moderatedAt", r => r.ModeratedAt)
        .Guid("firmPlatformId", r => r.FirmPlatformId)
        .Guid("memberId", r => r.MemberId)
        .Sort("text", r => r.Text)
        .Sort("productCode", r => r.ProductCode)
        .Sort("memberName", r => r.MemberName)
        .Sort("rating", r => r.Rating)
        .Sort("status", r => r.Status)
        .Sort("topic", r => r.Topic)
        .Sort("createdAt", r => r.CreatedAt)
        .Sort("moderatedAt", r => r.ModeratedAt)
        .DefaultSort(r => r.CreatedAt, desc: true)
        .TieBreaker(r => r.Id);

    public static IQueryable<ProductReview> ApplyNamed(IQueryable<ProductReview> query, ReviewModerationFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(r => r.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(r => r.ProductCode.ToLower().Contains(term)
                || r.MemberName.ToLower().Contains(term)
                || (r.Text != null && r.Text.ToLower().Contains(term)));
        }
        return query;
    }

    /// <param name="kanalKisiti">Y3 kapsam; verilmezse <c>grid.KanalKisiti</c> kullanılır
    /// (eski çağrılar kapsamı ayrı parametreyle taşıdığı için açık geçilebilir).</param>
    public static IQueryable<ProductReview> ApplyAll(
        IQueryable<ProductReview> query, ReviewModerationFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);

    public static string StatusLabel(string s) => s switch
    {
        "pending" => "Onay Bekliyor", "approved" => "Onaylı", "rejected" => "Reddedildi", _ => s,
    };
}

public record ReviewModerationFilters(string? Status = null, string? Search = null);

public record ReviewModerationExportRow(
    string ProductCode, string MemberName, int Rating, string? Topic, string? Text,
    string Status, string? RejectReason, DateTime CreatedAt, DateTime? ModeratedAt);

public record ExportReviewsForModerationQuery(ReviewModerationFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ReviewModerationExportRow>>>;

public class ExportReviewsForModerationQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportReviewsForModerationQuery, Result<GridExportSource<ReviewModerationExportRow>>>
{
    public async Task<Result<GridExportSource<ReviewModerationExportRow>>> Handle(
        ExportReviewsForModerationQuery r, CancellationToken ct)
    {
        var q = ReviewModerationGrid.ApplyAll(db.ProductReviews.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ReviewModerationExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = ReviewModerationGrid.Schema.ApplySort(q, r.Grid).Select(x => new ReviewModerationExportRow(
            x.ProductCode, x.MemberName, x.Rating, x.Topic, x.Text, x.Status, x.RejectReason, x.CreatedAt, x.ModeratedAt));
        return Result.Success(new GridExportSource<ReviewModerationExportRow>(count, rows));
    }
}
