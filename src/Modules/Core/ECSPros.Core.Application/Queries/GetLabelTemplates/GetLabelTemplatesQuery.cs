using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetLabelTemplates;

public record GetLabelTemplatesQuery(string? TargetType = null, bool ActiveOnly = false)
    : IRequest<Result<List<LabelTemplateDto>>>;

public record LabelTemplateDto(
    Guid Id, string Code, string Name, string TargetType,
    decimal WidthMm, decimal HeightMm, string ElementsJson, bool IsDefault, bool IsActive);

public class GetLabelTemplatesQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetLabelTemplatesQuery, Result<List<LabelTemplateDto>>>
{
    public async Task<Result<List<LabelTemplateDto>>> Handle(GetLabelTemplatesQuery request, CancellationToken ct)
    {
        var q = db.LabelTemplates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.TargetType)) q = q.Where(t => t.TargetType == request.TargetType);
        if (request.ActiveOnly) q = q.Where(t => t.IsActive);
        var list = await q.OrderBy(t => t.TargetType).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Name).ToListAsync(ct);
        return Result.Success(list.Select(t => new LabelTemplateDto(
            t.Id, t.Code, t.Name, t.TargetType, t.WidthMm, t.HeightMm, t.ElementsJson, t.IsDefault, t.IsActive)).ToList());
    }
}
