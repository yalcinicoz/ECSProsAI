using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetLookupValues;

public class GetLookupValuesQueryHandler : IRequestHandler<GetLookupValuesQuery, Result<List<LookupValueDto>>>
{
    private readonly ICoreDbContext _context;

    public GetLookupValuesQueryHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<LookupValueDto>>> Handle(GetLookupValuesQuery request, CancellationToken cancellationToken)
    {
        var typeExists = await _context.LookupTypes
            .AnyAsync(t => t.Code == request.TypeCode, cancellationToken);

        if (!typeExists)
            return Result.Failure<List<LookupValueDto>>($"Lookup type '{request.TypeCode}' bulunamadı.");

        var query = _context.LookupValues
            .Where(v => v.LookupType.Code == request.TypeCode);

        if (request.ActiveOnly)
            query = query.Where(v => v.IsActive);

        var items = await query
            .OrderBy(v => v.SortOrder)
            .Select(v => new LookupValueDto(v.Id, v.Code, v.NameI18n, v.Color, v.Icon, v.IsDefault, v.IsActive, v.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
