using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Games;

/// <summary>GET /api/store/games — kanalda bugün aktif oyunlar + bu üyenin (ya da misafirin) durumu.</summary>
public record GetStoreGamesQuery(Guid FirmPlatformId, Guid? MemberId) : IRequest<Result<List<StoreGameDto>>>;

public class GetStoreGamesQueryHandler(IPromotionDbContext db) : IRequestHandler<GetStoreGamesQuery, Result<List<StoreGameDto>>>
{
    public async Task<Result<List<StoreGameDto>>> Handle(GetStoreGamesQuery r, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var games = await db.Games.AsNoTracking().Include(g => g.Prizes)
            .Where(g => g.FirmPlatformId == r.FirmPlatformId && g.IsActive && g.StartsAt <= now && (g.EndsAt == null || g.EndsAt >= now))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.StartsAt)
            .ToListAsync(ct);
        var list = new List<StoreGameDto>();
        foreach (var g in games)
        {
            var oynanan = 0;
            if (r.MemberId is { } mid)
            {
                var key = SansOyunuKurali.DonemAnahtari(now, g.LimitPeriod);
                oynanan = await db.GamePlays.CountAsync(p => p.GameId == g.Id && p.MemberId == mid && p.PeriodKey == key, ct);
            }
            var d = SansOyunuKurali.DurumHesapla(g, now, r.MemberId.HasValue, oynanan);
            list.Add(Map(g, d));
        }
        return Result.Success(list);
    }

    public static StoreGameDto Map(Game g, SansOyunuKurali.Durum d) => new(
        g.Id.ToString(), g.Code, g.Type, Tr(g.TitleI18n) ?? g.Code, Tr(g.SubtitleI18n), Tr(g.DescriptionI18n), g.ImageUrl,
        g.ThemeColor, g.AccentColor, g.RequiresLogin, g.AlwaysWin,
        d.Status, d.Label, d.RemainingPlays, d.NextPlayAt, g.StartsAt, g.EndsAt, g.CtaLabel, Tr(g.RulesTextI18n),
        g.Prizes.Where(p => p.IsActive && !p.IsDeleted).OrderBy(p => p.SortOrder)
            .Select(p => new StoreGamePrizeDto(p.Id.ToString(), p.Label, p.ShortLabel, p.Kind, p.Color, p.IconUrl, p.Description)).ToList());

    public static string? Tr(Dictionary<string, string>? m) => m is null ? null : (m.TryGetValue("tr", out var v) && !string.IsNullOrWhiteSpace(v) ? v : m.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
}

/// <summary>GET /api/store/account/games/history — üyenin oynanışları.</summary>
public record GetMemberGameHistoryQuery(Guid FirmPlatformId, Guid MemberId, int Limit = 50) : IRequest<Result<List<StoreGameHistoryDto>>>;

public class GetMemberGameHistoryQueryHandler(IPromotionDbContext db) : IRequestHandler<GetMemberGameHistoryQuery, Result<List<StoreGameHistoryDto>>>
{
    public async Task<Result<List<StoreGameHistoryDto>>> Handle(GetMemberGameHistoryQuery r, CancellationToken ct)
    {
        var rows = await db.GamePlays.AsNoTracking()
            .Where(p => p.MemberId == r.MemberId && p.FirmPlatformId == r.FirmPlatformId)
            .OrderByDescending(p => p.PlayedAt).Take(Math.Clamp(r.Limit, 1, 200))
            .Select(p => new { p.Id, p.Game.Code, p.Game.TitleI18n, p.PlayedAt, p.Won, PrizeLabel = p.Prize != null ? p.Prize.Label : null, p.CouponCode, p.PointsGiven })
            .ToListAsync(ct);
        return Result.Success(rows.Select(x => new StoreGameHistoryDto(x.Id.ToString(), x.Code, GetStoreGamesQueryHandler.Tr(x.TitleI18n) ?? x.Code,
            x.PlayedAt, x.Won, x.PrizeLabel, x.CouponCode, x.PointsGiven)).ToList());
    }
}
