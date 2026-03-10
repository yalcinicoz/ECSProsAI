using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetExpenseTypes;

public record GetExpenseTypesQuery(bool ActiveOnly = true) : IRequest<Result<List<ExpenseTypeDto>>>;

public record ExpenseTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsItemLevel,
    decimal DefaultTaxRate,
    bool IsActive,
    int SortOrder
);

public class GetExpenseTypesQueryHandler : IRequestHandler<GetExpenseTypesQuery, Result<List<ExpenseTypeDto>>>
{
    private readonly ICoreDbContext _db;

    public GetExpenseTypesQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<ExpenseTypeDto>>> Handle(GetExpenseTypesQuery request, CancellationToken ct)
    {
        var query = _db.ExpenseTypes.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(e => e.IsActive);

        var list = await query
            .OrderBy(e => e.SortOrder).ThenBy(e => e.Code)
            .Select(e => new ExpenseTypeDto(e.Id, e.Code, e.NameI18n, e.IsItemLevel, e.DefaultTaxRate, e.IsActive, e.SortOrder))
            .ToListAsync(ct);

        return Result.Success<List<ExpenseTypeDto>>(list);
    }
}
