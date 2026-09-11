using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetUsers;

/// <summary>
/// Kullanıcılar (personel) DataGrid şeması: beyaz listeli sıralama/filtre + mevcut adlandırılmış filtreler (search, activeOnly).
/// `role` filtresi şemada değil handler tarafında (UserRoles.Any) — grid alanı "role" özel işlenir (ApplyAll skipFields).
/// </summary>
public static class UserGrid
{
    public static readonly GridSchema<User> Schema = new GridSchema<User>()
        .Text("username", u => u.Username)
        .Text("firstName", u => u.FirstName)
        .Text("lastName", u => u.LastName)
        .Text("email", u => u.Email)
        .Text("phone", u => u.Phone)
        .Text("department", u => u.Department)
        .Text("jobTitle", u => u.JobTitle)
        .Bool("isActive", u => u.IsActive)
        .Bool("isSuperAdmin", u => u.IsSuperAdmin)   // ROL kolonu: Süper Admin / Çalışan
        .Date("lastLoginAt", u => u.LastLoginAt)
        .Date("createdAt", u => u.CreatedAt)
        .Sort("username", u => u.Username)
        .Sort("name", u => u.FirstName)
        .Sort("firstName", u => u.FirstName)
        .Sort("lastName", u => u.LastName)
        .Sort("email", u => u.Email)
        .Sort("phone", u => u.Phone)
        .Sort("department", u => u.Department)
        .Sort("jobTitle", u => u.JobTitle)
        .Sort("lastLoginAt", u => u.LastLoginAt)
        .Sort("createdAt", u => u.CreatedAt)
        .Sort("isActive", u => u.IsActive)
        .Sort("isSuperAdmin", u => u.IsSuperAdmin)
        .DefaultSort(u => u.Username, desc: false)
        .TieBreaker(u => u.Id);

    public static IQueryable<User> ApplyNamed(IQueryable<User> query, UserListFilters f)
    {
        query = query.Where(u => !u.IsDeleted);
        if (f.ActiveOnly) query = query.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                (u.Phone != null && u.Phone.Contains(s)));
        }
        return query;
    }

    /// <summary>Adlandırılmış + grid filtreleri; `role` (rol kodu, virgüllü) handler tarafında UserRoles üzerinden uygulanır.</summary>
    public static IQueryable<User> ApplyAll(IQueryable<User> query, UserListFilters f, GridRequest? grid)
    {
        query = ApplyNamed(query, f);
        var role = grid?.Filters.FirstOrDefault(x => string.Equals(x.Field, "role", StringComparison.OrdinalIgnoreCase));
        if (role is not null && !string.IsNullOrWhiteSpace(role.Value))
        {
            var codes = role.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            query = query.Where(u => u.UserRoles.Any(r => !r.IsDeleted && codes.Contains(r.Role.Code)));
        }
        return Schema.ApplyFilters(query, grid, "role");
    }
}

public record UserListFilters(string? Search = null, bool ActiveOnly = false);

public record UserExportRow(
    string Username, string FirstName, string LastName, string Email, string? Phone, string Department, string? JobTitle,
    bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt, string Roles, bool IsSuperAdmin);

public record ExportUsersQuery(UserListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<UserExportRow>>>;

public class ExportUsersQueryHandler(IIamDbContext db) : IRequestHandler<ExportUsersQuery, Result<GridExportSource<UserExportRow>>>
{
    public async Task<Result<GridExportSource<UserExportRow>>> Handle(ExportUsersQuery r, CancellationToken ct)
    {
        var q = UserGrid.ApplyAll(db.Users.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<UserExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = UserGrid.Schema.ApplySort(q, r.Grid).Select(u => new UserExportRow(
            u.Username, u.FirstName, u.LastName, u.Email, u.Phone, u.Department, u.JobTitle, u.IsActive, u.LastLoginAt, u.CreatedAt,
            string.Join(", ", u.UserRoles.Where(x => !x.IsDeleted).Select(x => x.Role.Code)), u.IsSuperAdmin));
        return Result.Success(new GridExportSource<UserExportRow>(count, rows));
    }
}
