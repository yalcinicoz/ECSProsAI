using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetAdminMenus;

public record GetAdminMenusQuery(bool ActiveOnly = false) : IRequest<Result<List<AdminMenuDto>>>;

public record AdminMenuDto(
    Guid Id,
    Guid? ParentId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Icon,
    string? Route,
    string? PermissionCode,
    bool IsActive,
    int SortOrder,
    List<AdminMenuDto> Children
);

public class GetAdminMenusQueryHandler : IRequestHandler<GetAdminMenusQuery, Result<List<AdminMenuDto>>>
{
    private readonly IIamDbContext _db;

    public GetAdminMenusQueryHandler(IIamDbContext db) => _db = db;

    public async Task<Result<List<AdminMenuDto>>> Handle(GetAdminMenusQuery request, CancellationToken ct)
    {
        var query = _db.AdminMenus.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(m => m.IsActive);

        var all = await query.OrderBy(m => m.SortOrder).ThenBy(m => m.Code).ToListAsync(ct);

        // Ağaç yapısına dönüştür
        var tree = BuildTree(all, null);
        return Result.Success(tree);
    }

    private static List<AdminMenuDto> BuildTree(
        List<ECSPros.Iam.Domain.Entities.AdminMenu> all, Guid? parentId)
    {
        return all
            .Where(m => m.ParentId == parentId)
            .Select(m => new AdminMenuDto(
                m.Id, m.ParentId, m.Code, m.NameI18n, m.Icon, m.Route,
                m.PermissionCode, m.IsActive, m.SortOrder,
                BuildTree(all, m.Id)))
            .ToList();
    }
}
