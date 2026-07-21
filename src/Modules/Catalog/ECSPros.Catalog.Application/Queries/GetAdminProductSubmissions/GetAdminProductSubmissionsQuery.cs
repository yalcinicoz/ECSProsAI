using System.Text.Json;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetAdminProductSubmissions;

/// <summary>Admin (panel): tüm tedarikçi gönderimleri, durum/tedarikçi filtreli. Owner-scoped DEĞİL.</summary>
public record GetAdminProductSubmissionsQuery(string? Status, Guid? SupplierId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AdminSubmissionListDto>>>;

public record AdminSubmissionListDto(
    Guid Id, Guid SupplierId, string SupplierProductCode, string GroupCode,
    Dictionary<string, string> Name, int VariantCount, string Status,
    string? ProductCode, string? ReviewNote, DateTime SubmittedAt, DateTime? ReviewedAt);

public class GetAdminProductSubmissionsQueryHandler
    : IRequestHandler<GetAdminProductSubmissionsQuery, Result<PagedResult<AdminSubmissionListDto>>>
{
    private readonly ICatalogDbContext _db;
    public GetAdminProductSubmissionsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminSubmissionListDto>>> Handle(GetAdminProductSubmissionsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var q = _db.ProductSubmissions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(s => s.Status == request.Status);
        if (request.SupplierId.HasValue) q = q.Where(s => s.SupplierId == request.SupplierId);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new AdminSubmissionListDto(
                s.Id, s.SupplierId, s.SupplierProductCode, s.GroupCode, s.Name, s.VariantCount,
                s.Status, s.ProductCode, s.ReviewNote, s.CreatedAt, s.ReviewedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AdminSubmissionListDto>(items, total, page, pageSize));
    }
}
