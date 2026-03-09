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
        var query = _context.Members.AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(m =>
                (m.Email != null && m.Email.ToLower().Contains(s)) ||
                (m.Phone != null && m.Phone.Contains(s)) ||
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new MemberListDto(m.Id, m.FirstName, m.LastName, m.Email, m.Phone, m.IsRegistered, m.IsActive, m.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedMemberResult(items, totalCount, request.Page, request.PageSize));
    }
}
