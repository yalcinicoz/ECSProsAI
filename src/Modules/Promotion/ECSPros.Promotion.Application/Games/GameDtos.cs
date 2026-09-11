namespace ECSPros.Promotion.Application.Games;

// ── Panel DTO'ları ──
public record GamePrizeDto(
    Guid? Id, string Label, string? ShortLabel, string Kind, string? Color, string? IconUrl, string? Description,
    int Weight, int SortOrder, bool IsActive, string? CouponType, decimal? CouponValue, decimal? MinimumCartTotal, int? Points);

public record GameDetailDto(
    Guid Id, Guid FirmPlatformId, string Code, string Type,
    Dictionary<string, string> TitleI18n, Dictionary<string, string>? SubtitleI18n, Dictionary<string, string>? DescriptionI18n,
    Dictionary<string, string>? RulesTextI18n, string? CtaLabel, string? ImageUrl, string? ThemeColor, string? AccentColor,
    bool RequiresLogin, bool AlwaysWin, DateTime StartsAt, DateTime? EndsAt, bool IsActive,
    string LimitPeriod, int LimitCount, int CouponValidDays, int SortOrder,
    string LabelAvailable, string LabelCooldown, string LabelExhausted, string LabelLoginRequired, string LabelEnded,
    string WinMessage, string WinSubMessage, string LoseMessage, string LoseSubMessage,
    List<GamePrizeDto> Prizes, int PlayCount, int WinCount);

public record GameListDto(
    Guid Id, Guid FirmPlatformId, string Code, string Type, Dictionary<string, string> TitleI18n,
    DateTime StartsAt, DateTime? EndsAt, bool IsActive, bool AlwaysWin, string LimitPeriod, int LimitCount,
    int PrizeCount, int PlayCount, int WinCount, DateTime CreatedAt);

public record GamePlayListDto(
    Guid Id, Guid MemberId, DateTime PlayedAt, string PeriodKey, bool Won, string? PrizeLabel, string? CouponCode, int? PointsGiven);

// ── Mağaza (mobil) DTO'ları — docs/BACKEND_OYUNLAR.md §2-3 ile birebir alan adları ──
public record StoreGamePrizeDto(string Id, string Label, string? ShortLabel, string Kind, string? Color, string? IconUrl, string? Description);

public record StoreGameDto(
    string Id, string Code, string Type, string Title, string? Subtitle, string? Description, string? ImageUrl,
    string? ThemeColor, string? AccentColor, bool RequiresLogin, bool AlwaysWin,
    string Status, string StatusLabel, int RemainingPlays, DateTime? NextPlayAt, DateTime StartsAt, DateTime? EndsAt,
    string? CtaLabel, string? RulesText, List<StoreGamePrizeDto> Prizes);

public record StorePlayPrizeDto(string Id, string Label, string Kind, string? Description, string? CouponCode, DateTime? ValidUntil, string? AmountText, int? Points);
public record StorePlayCellDto(string PrizeId, string Label, string? Color);

public record StorePlayResultDto(
    string PlayId, string PrizeId, bool Won, StorePlayPrizeDto Prize, string Message, string? SubMessage,
    int RemainingPlays, DateTime? NextPlayAt, string Status, string StatusLabel, List<StorePlayCellDto>? Cells);

public record StoreGameHistoryDto(string PlayId, string GameCode, string GameTitle, DateTime PlayedAt, bool Won, string? PrizeLabel, string? CouponCode, int? Points);
