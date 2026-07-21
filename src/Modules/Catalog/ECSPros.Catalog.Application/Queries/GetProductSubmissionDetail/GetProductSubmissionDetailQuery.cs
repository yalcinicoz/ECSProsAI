using System.Text.Json;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductSubmissionDetail;

/// <summary>Admin (panel) inceleme: gönderimin tam ham gövdesi + metadata. Onay/red kararı için.</summary>
public record GetProductSubmissionDetailQuery(Guid Id) : IRequest<Result<ProductSubmissionDetailDto>>;

public record ProductSubmissionDetailDto(
    Guid Id, Guid SupplierId, Guid? ApiClientId, string SupplierProductCode, string GroupCode,
    Dictionary<string, string> Name, int VariantCount, string Status,
    string? ProductCode, string? ReviewNote, DateTime SubmittedAt, DateTime? ReviewedAt,
    JsonElement Payload);

public class GetProductSubmissionDetailQueryHandler
    : IRequestHandler<GetProductSubmissionDetailQuery, Result<ProductSubmissionDetailDto>>
{
    private readonly ICatalogDbContext _db;
    public GetProductSubmissionDetailQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<ProductSubmissionDetailDto>> Handle(GetProductSubmissionDetailQuery request, CancellationToken ct)
    {
        var s = await _db.ProductSubmissions.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (s is null) return Result.Failure<ProductSubmissionDetailDto>("Gönderim bulunamadı.");

        JsonElement payload;
        try { payload = JsonDocument.Parse(s.PayloadJson).RootElement.Clone(); }
        catch { payload = JsonDocument.Parse("{}").RootElement.Clone(); }

        return Result.Success(new ProductSubmissionDetailDto(
            s.Id, s.SupplierId, s.ApiClientId, s.SupplierProductCode, s.GroupCode, s.Name, s.VariantCount,
            s.Status, s.ProductCode, s.ReviewNote, s.CreatedAt, s.ReviewedAt, payload));
    }
}
