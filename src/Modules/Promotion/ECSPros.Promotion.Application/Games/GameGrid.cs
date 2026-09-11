using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Games;

/// <summary>Şans oyunları DataGrid şeması (admin liste): kod/ad/tip/dönem/aktif/yayında + tarih; kanal kapsamı FirmPlatformId.</summary>
public static class GameGrid
{
    public static readonly GridSchema<Game> Schema = new GridSchema<Game>()
        .Kanal(g => g.FirmPlatformId)
        .Text("code", g => g.Code)
        .Text("title", g => GridJson.Text(g.TitleI18n, "tr"))
        .Enum("type", g => g.Type, GameTypes.All)
        .Enum("limitPeriod", g => g.LimitPeriod, GameLimitPeriods.All)
        .Number("limitCount", g => g.LimitCount)
        .Number("playCount", g => g.Plays.Count)
        .Bool("isActive", g => g.IsActive)
        .Bool("alwaysWin", g => g.AlwaysWin)
        .Bool("live", g => g.IsActive && g.StartsAt <= DateTime.UtcNow && (g.EndsAt == null || g.EndsAt >= DateTime.UtcNow))
        .Date("startsAt", g => g.StartsAt)
        .Date("endsAt", g => g.EndsAt)
        .Date("createdAt", g => g.CreatedAt)
        .Guid("firmPlatformId", g => g.FirmPlatformId)
        .Sort("code", g => g.Code)
        .Sort("title", g => GridJson.Text(g.TitleI18n, "tr"))
        .Sort("type", g => g.Type)
        .Sort("limitPeriod", g => g.LimitPeriod)
        .Sort("limitCount", g => g.LimitCount)
        .Sort("playCount", g => g.Plays.Count)
        .Sort("isActive", g => g.IsActive)
        .Sort("alwaysWin", g => g.AlwaysWin)
        .Sort("startsAt", g => g.StartsAt)
        .Sort("endsAt", g => g.EndsAt)
        .Sort("createdAt", g => g.CreatedAt)
        .DefaultSort(g => g.CreatedAt, desc: true)
        .TieBreaker(g => g.Id);

    public static IQueryable<Game> ApplyAll(IQueryable<Game> query, string? search, GridRequest? grid)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(g => g.Code.ToLower().Contains(s) || GridJson.Text(g.TitleI18n, "tr")!.ToLower().Contains(s));
        }
        return Schema.ApplyKanalKapsami(Schema.ApplyFilters(query, grid), grid?.KanalKisiti);
    }
}

public record GetGamesQuery(string? Search, int Page, int PageSize, GridRequest? Grid) : IRequest<Result<PagedResult<GameListDto>>>;

public class GetGamesQueryHandler(IPromotionDbContext db) : IRequestHandler<GetGamesQuery, Result<PagedResult<GameListDto>>>
{
    public async Task<Result<PagedResult<GameListDto>>> Handle(GetGamesQuery r, CancellationToken ct)
    {
        var q = GameGrid.ApplyAll(db.Games.AsNoTracking(), r.Search, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await GameGrid.Schema.ApplySort(q, r.Grid)
            .Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(g => new GameListDto(g.Id, g.FirmPlatformId, g.Code, g.Type, g.TitleI18n, g.StartsAt, g.EndsAt, g.IsActive, g.AlwaysWin,
                g.LimitPeriod, g.LimitCount, g.Prizes.Count(p => p.IsActive), g.Plays.Count, g.Plays.Count(p => p.Won), g.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<GameListDto>(items, total, r.Page, r.PageSize));
    }
}

public record GetGameDetailQuery(Guid Id) : IRequest<Result<GameDetailDto>>;

public class GetGameDetailQueryHandler(IPromotionDbContext db) : IRequestHandler<GetGameDetailQuery, Result<GameDetailDto>>
{
    public async Task<Result<GameDetailDto>> Handle(GetGameDetailQuery r, CancellationToken ct)
    {
        var g = await db.Games.AsNoTracking().Include(x => x.Prizes).FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (g is null) return Result.Failure<GameDetailDto>("Oyun bulunamadı.");
        var playCount = await db.GamePlays.CountAsync(p => p.GameId == g.Id, ct);
        var winCount = await db.GamePlays.CountAsync(p => p.GameId == g.Id && p.Won, ct);
        return Result.Success(Map(g, playCount, winCount));
    }

    public static GameDetailDto Map(Game g, int playCount, int winCount) => new(
        g.Id, g.FirmPlatformId, g.Code, g.Type, g.TitleI18n, g.SubtitleI18n, g.DescriptionI18n, g.RulesTextI18n, g.CtaLabel,
        g.ImageUrl, g.ThemeColor, g.AccentColor, g.RequiresLogin, g.AlwaysWin, g.StartsAt, g.EndsAt, g.IsActive,
        g.LimitPeriod, g.LimitCount, g.CouponValidDays, g.SortOrder,
        g.LabelAvailable, g.LabelCooldown, g.LabelExhausted, g.LabelLoginRequired, g.LabelEnded,
        g.WinMessage, g.WinSubMessage, g.LoseMessage, g.LoseSubMessage,
        g.Prizes.OrderBy(p => p.SortOrder).Select(p => new GamePrizeDto(p.Id, p.Label, p.ShortLabel, p.Kind, p.Color, p.IconUrl, p.Description,
            p.Weight, p.SortOrder, p.IsActive, p.CouponType, p.CouponValue, p.MinimumCartTotal, p.Points)).ToList(),
        playCount, winCount);
}

public record GetGamePlaysQuery(Guid GameId, int Page, int PageSize) : IRequest<Result<PagedResult<GamePlayListDto>>>;

public class GetGamePlaysQueryHandler(IPromotionDbContext db) : IRequestHandler<GetGamePlaysQuery, Result<PagedResult<GamePlayListDto>>>
{
    public async Task<Result<PagedResult<GamePlayListDto>>> Handle(GetGamePlaysQuery r, CancellationToken ct)
    {
        var q = db.GamePlays.AsNoTracking().Where(p => p.GameId == r.GameId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.PlayedAt).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(p => new GamePlayListDto(p.Id, p.MemberId, p.PlayedAt, p.PeriodKey, p.Won, p.Prize != null ? p.Prize.Label : null, p.CouponCode, p.PointsGiven))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<GamePlayListDto>(items, total, r.Page, r.PageSize));
    }
}
