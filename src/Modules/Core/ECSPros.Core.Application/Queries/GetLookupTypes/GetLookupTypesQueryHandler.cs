using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetLookupTypes;

public class GetLookupTypesQueryHandler : IRequestHandler<GetLookupTypesQuery, Result<List<LookupTypeDto>>>
{
    private readonly ICoreDbContext _context;

    public GetLookupTypesQueryHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<LookupTypeDto>>> Handle(GetLookupTypesQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.LookupTypes
            .OrderBy(x => x.Code)
            .Select(x => new LookupTypeDto(x.Id, x.Code, x.NameI18n, x.Description, x.IsSystem))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
