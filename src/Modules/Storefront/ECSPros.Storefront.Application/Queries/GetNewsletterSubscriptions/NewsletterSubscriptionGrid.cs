using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetNewsletterSubscriptions;

/// <summary>
/// Bülten aboneleri DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (isActive/firmPlatformId) + global arama (e-posta).
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — abone bir kanala aittir.</para>
/// <para>E-posta kişisel veridir: Excel kolonunda alan yetkisine bağlıdır.</para>
/// </summary>
public static class NewsletterSubscriptionGrid
{
    public static readonly GridSchema<NewsletterSubscription> Schema = new GridSchema<NewsletterSubscription>()
        .Kanal(n => n.FirmPlatformId)   // Y3 (K2)
        .Text("email", n => n.Email)
        .Bool("isActive", n => n.IsActive)
        .Bool("isMember", n => n.MemberId != null)
        .Date("createdAt", n => n.CreatedAt)
        .Guid("firmPlatformId", n => n.FirmPlatformId)
        .Guid("memberId", n => n.MemberId)
        .Sort("email", n => n.Email)
        .Sort("isActive", n => n.IsActive)
        .Sort("isMember", n => n.MemberId != null)
        .Sort("createdAt", n => n.CreatedAt)
        .DefaultSort(n => n.CreatedAt, desc: true)
        .TieBreaker(n => n.Id);

    public static IQueryable<NewsletterSubscription> ApplyNamed(
        IQueryable<NewsletterSubscription> query, NewsletterFilters f)
    {
        if (f.IsActive.HasValue) query = query.Where(n => n.IsActive == f.IsActive.Value);
        if (f.FirmPlatformId.HasValue) query = query.Where(n => n.FirmPlatformId == f.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var aranan = f.Search.Trim().ToLower();
            query = query.Where(n => n.Email.ToLower().Contains(aranan));
        }
        return query;
    }

    public static IQueryable<NewsletterSubscription> ApplyAll(
        IQueryable<NewsletterSubscription> query, NewsletterFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);
}

public record NewsletterFilters(bool? IsActive = null, Guid? FirmPlatformId = null, string? Search = null);

public record NewsletterExportRow(string Email, bool IsActive, bool IsMember, DateTime CreatedAt);

public record ExportNewsletterSubscriptionsQuery(NewsletterFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<NewsletterExportRow>>>;

public class ExportNewsletterSubscriptionsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportNewsletterSubscriptionsQuery, Result<GridExportSource<NewsletterExportRow>>>
{
    public async Task<Result<GridExportSource<NewsletterExportRow>>> Handle(
        ExportNewsletterSubscriptionsQuery r, CancellationToken ct)
    {
        var q = NewsletterSubscriptionGrid.ApplyAll(db.NewsletterSubscriptions.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<NewsletterExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = NewsletterSubscriptionGrid.Schema.ApplySort(q, r.Grid).Select(n => new NewsletterExportRow(
            n.Email, n.IsActive, n.MemberId != null, n.CreatedAt));
        return Result.Success(new GridExportSource<NewsletterExportRow>(count, rows));
    }
}
