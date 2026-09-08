using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMembers;

public class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, Result<PagedMemberResult>>
{
    private readonly ICrmDbContext _context;

    public GetMembersQueryHandler(ICrmDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedMemberResult>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        // DataGrid F4 (2026-09-08): adlandırılmış filtre + global arama + beyaz listeli grid filtreleri TEK yerden (MemberGrid).
        var filters = new MemberListFilters(request.Search, request.ActiveOnly);
        var query = MemberGrid.ApplyAll(_context.Members.AsQueryable(), filters, request.Grid);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await MemberGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new MemberListDto(m.Id, m.FirstName, m.LastName, m.Email, m.Phone, m.IsRegistered, m.IsActive, m.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedMemberResult(items, totalCount, request.Page, request.PageSize));
    }
}
