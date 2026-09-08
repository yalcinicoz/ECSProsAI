using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Crm.Application.Queries.GetMembers;

public record GetMembersQuery(
    string? Search = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 20,
    GridRequest? Grid = null) : IRequest<Result<PagedMemberResult>>;
    // Grid (2026-09-08, DataGrid F4): beyaz listeli f.* filtreleri + sort/dir (MemberGrid.Schema); null → eski davranış

public record PagedMemberResult(List<MemberListDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record MemberListDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    bool IsRegistered,
    bool IsActive,
    DateTime CreatedAt);
