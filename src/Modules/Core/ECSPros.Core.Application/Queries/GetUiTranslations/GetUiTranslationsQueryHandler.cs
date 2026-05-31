using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetUiTranslations;

public class GetUiTranslationsQueryHandler
    : IRequestHandler<GetUiTranslationsQuery, Result<List<UiTranslationDto>>>
{
    private readonly ICoreDbContext _context;

    public GetUiTranslationsQueryHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<UiTranslationDto>>> Handle(
        GetUiTranslationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.UiTranslations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Namespace))
            query = query.Where(x => x.Namespace == request.Namespace);

        if (!string.IsNullOrWhiteSpace(request.Lang))
            query = query.Where(x => x.Lang == request.Lang);

        var items = await query
            .OrderBy(x => x.Namespace)
            .ThenBy(x => x.Key)
            .ThenBy(x => x.Lang)
            .Select(x => new UiTranslationDto(x.Id, x.Namespace, x.Key, x.Lang, x.Value))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
