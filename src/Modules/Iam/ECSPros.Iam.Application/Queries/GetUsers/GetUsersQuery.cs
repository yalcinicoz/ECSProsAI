using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Queries.GetUsers;

public record GetUsersQuery(
    string? Search = null,
    bool ActiveOnly = false,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedUserResult>>;

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
    bool IsActive,
    DateTime? LastLoginAt,
    List<string> Roles);
