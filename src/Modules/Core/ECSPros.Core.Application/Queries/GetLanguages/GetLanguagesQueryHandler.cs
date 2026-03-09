using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetLanguages;

public class GetLanguagesQueryHandler : IRequestHandler<GetLanguagesQuery, Result<List<LanguageDto>>>
{
    private readonly ICoreDbContext _context;

    public GetLanguagesQueryHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<LanguageDto>>> Handle(GetLanguagesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Languages.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new LanguageDto(x.Id, x.Code, x.NativeName, x.Direction, x.IsDefault, x.IsActive, x.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
