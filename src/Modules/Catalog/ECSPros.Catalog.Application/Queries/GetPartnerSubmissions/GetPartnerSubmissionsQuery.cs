using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetPartnerSubmissions;

/// <summary>Partner owner-scoped: çağıran tedarikçinin (SupplierId) ürün gönderimleri + durumları.
/// Başka tedarikçinin kayıtları asla dönmez.</summary>
public record GetPartnerSubmissionsQuery(Guid SupplierId, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PartnerSubmissionDto>>>;

public record PartnerSubmissionDto(
    string SupplierProductCode,
    string GroupCode,
    Dictionary<string, string> Name,
    int VariantCount,
    string Status,
    string? ProductCode,
    string? ReviewNote,
    DateTime SubmittedAt,
    DateTime? ReviewedAt);

public class GetPartnerSubmissionsQueryHandler
    : IRequestHandler<GetPartnerSubmissionsQuery, Result<PagedResult<PartnerSubmissionDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetPartnerSubmissionsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<PagedResult<PartnerSubmissionDto>>> Handle(GetPartnerSubmissionsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _db.ProductSubmissions.Where(s => s.SupplierId == request.SupplierId);
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(s => s.Status == request.Status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new PartnerSubmissionDto(
                s.SupplierProductCode, s.GroupCode, s.Name, s.VariantCount,
                s.Status, s.ProductCode, s.ReviewNote, s.CreatedAt, s.ReviewedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<PartnerSubmissionDto>(items, total, page, pageSize));
    }
}
