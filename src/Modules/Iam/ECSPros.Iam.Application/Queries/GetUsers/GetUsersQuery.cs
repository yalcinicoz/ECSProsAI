using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Iam.Application.Queries.GetUsers;

public record GetUsersQuery(
    string? Search = null,
    bool ActiveOnly = false,
    int Page = 1,
    int PageSize = 20,
    GridRequest? Grid = null) : IRequest<Result<PagedUserResult>>;
    // Grid (2026-09-08, DataGrid): beyaz listeli f.* filtreleri + sort/dir + `role` (UserGrid); null → eski davranış (username artan)

public record PagedUserResult(List<UserListDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record UserListDto(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Department,
    string? JobTitle,
    string? Phone,
    bool IsActive,
    DateTime? LastLoginAt,
    List<string> Roles);
