using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberSavedSearches;

/// <summary>E11: üyenin kayıtlı aramaları (yeni → eski) — Favori Aramalarım sayfası.</summary>
public record GetMemberSavedSearchesQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<SavedSearchDto>>>;

public record SavedSearchDto(
    Guid Id,
    string? Name,
    string Query,
    Dictionary<string, string>? Filters,
    bool NotifyEnabled,
    DateTime CreatedAt);

public class GetMemberSavedSearchesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberSavedSearchesQuery, Result<List<SavedSearchDto>>>
{
    public async Task<Result<List<SavedSearchDto>>> Handle(GetMemberSavedSearchesQuery request, CancellationToken ct)
    {
        var liste = await db.SavedSearches
            .Where(s => s.FirmPlatformId == request.FirmPlatformId && s.MemberId == request.MemberId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SavedSearchDto(s.Id, s.Name, s.Query, s.Filters, s.NotifyEnabled, s.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(liste);
    }
}
