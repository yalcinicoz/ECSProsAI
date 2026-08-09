using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCardMessages;

/// <summary>Ürün Kartı F2: kanalın kart mesajları — panel CRUD listesi (pasifler dahil).</summary>
public record GetCardMessagesQuery(Guid FirmPlatformId) : IRequest<Result<List<CardMessageDto>>>;

public record CardMessageDto(
    Guid Id,
    int Slot,
    Dictionary<string, string> MessageI18n,
    string? Icon,
    string? Color,
    string ScopeType,
    List<Guid>? ScopeCategoryIds,
    List<string>? ScopeProductCodes,
    DateTime? StartDate,
    DateTime? EndDate,
    int SortOrder,
    bool IsActive);

public class GetCardMessagesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetCardMessagesQuery, Result<List<CardMessageDto>>>
{
    public async Task<Result<List<CardMessageDto>>> Handle(GetCardMessagesQuery request, CancellationToken ct)
    {
        var liste = await db.CardMessages.AsNoTracking()
            .Where(m => m.FirmPlatformId == request.FirmPlatformId)
            .OrderBy(m => m.Slot).ThenBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .Select(m => new CardMessageDto(m.Id, m.Slot, m.MessageI18n, m.Icon, m.Color,
                m.ScopeType, m.ScopeCategoryIds, m.ScopeProductCodes,
                m.StartDate, m.EndDate, m.SortOrder, m.IsActive))
            .ToListAsync(ct);
        return Result.Success(liste);
    }
}
