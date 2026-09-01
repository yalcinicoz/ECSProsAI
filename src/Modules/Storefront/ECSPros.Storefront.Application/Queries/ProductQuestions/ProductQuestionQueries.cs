using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.ProductQuestions;

public record ProductQuestionDto(
    Guid Id, string ProductCode, string Question, string? Answer,
    string Status, string MemberName, DateTime CreatedAt, DateTime? AnsweredAt);

/// <summary>Ürün detayı: yalnız CEVAPLANMIŞ (yayındaki) sorular — herkese açık.</summary>
public record GetProductQuestionsQuery(Guid FirmPlatformId, string ProductCode, int Limit = 20)
    : IRequest<Result<List<ProductQuestionDto>>>;

public class GetProductQuestionsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetProductQuestionsQuery, Result<List<ProductQuestionDto>>>
{
    public async Task<Result<List<ProductQuestionDto>>> Handle(GetProductQuestionsQuery request, CancellationToken ct)
        => Result.Success(await db.ProductQuestions.AsNoTracking()
            .Where(q => q.FirmPlatformId == request.FirmPlatformId
                     && q.ProductCode == request.ProductCode && q.Status == "answered")
            .OrderByDescending(q => q.AnsweredAt)
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(q => new ProductQuestionDto(q.Id, q.ProductCode, q.Question, q.Answer,
                q.Status, q.MemberName, q.CreatedAt, q.AnsweredAt))
            .ToListAsync(ct));
}

/// <summary>Hesabım → Sorularım: üyenin tüm soruları (gizlenenler dahil — kendi cevabını görür).</summary>
public record GetMemberQuestionsQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<ProductQuestionDto>>>;

public class GetMemberQuestionsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberQuestionsQuery, Result<List<ProductQuestionDto>>>
{
    public async Task<Result<List<ProductQuestionDto>>> Handle(GetMemberQuestionsQuery request, CancellationToken ct)
        => Result.Success(await db.ProductQuestions.AsNoTracking()
            .Where(q => q.FirmPlatformId == request.FirmPlatformId && q.MemberId == request.MemberId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(200)
            .Select(q => new ProductQuestionDto(q.Id, q.ProductCode, q.Question, q.Answer,
                q.Status, q.MemberName, q.CreatedAt, q.AnsweredAt))
            .ToListAsync(ct));
}

/// <summary>Panel moderasyonu: durum filtresi + sayfalı; bekleyenler en eski önce (SLA).</summary>
public record GetQuestionsForModerationQuery(
    Guid? FirmPlatformId = null, string? Status = null, int Page = 1, int PageSize = 30)
    : IRequest<Result<PagedResult<ProductQuestionDto>>>;

public class GetQuestionsForModerationQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetQuestionsForModerationQuery, Result<PagedResult<ProductQuestionDto>>>
{
    public async Task<Result<PagedResult<ProductQuestionDto>>> Handle(GetQuestionsForModerationQuery request, CancellationToken ct)
    {
        var q = db.ProductQuestions.AsNoTracking().AsQueryable();
        if (request.FirmPlatformId is { } fp) q = q.Where(x => x.FirmPlatformId == fp);
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(x => x.Status == request.Status);

        var toplam = await q.CountAsync(ct);
        var sayfa = Math.Max(1, request.Page);
        var boy = Math.Clamp(request.PageSize, 1, 100);
        var items = await q
            .OrderBy(x => x.Status == "pending" ? 0 : 1)
            .ThenBy(x => x.Status == "pending" ? x.CreatedAt : DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((sayfa - 1) * boy).Take(boy)
            .Select(x => new ProductQuestionDto(x.Id, x.ProductCode, x.Question, x.Answer,
                x.Status, x.MemberName, x.CreatedAt, x.AnsweredAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ProductQuestionDto>(items, toplam, sayfa, boy));
    }
}
