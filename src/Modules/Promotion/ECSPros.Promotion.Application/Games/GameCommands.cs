using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Games;

/// <summary>Oyun oluştur/güncelle — ödüller listesiyle birlikte (listede olmayan ödül soft-delete; oynanmış ödülün kaydı korunur).</summary>
public record SaveGameCommand(
    Guid? Id, Guid FirmPlatformId, string Code, string Type,
    Dictionary<string, string> TitleI18n, Dictionary<string, string>? SubtitleI18n, Dictionary<string, string>? DescriptionI18n,
    Dictionary<string, string>? RulesTextI18n, string? CtaLabel, string? ImageUrl, string? ThemeColor, string? AccentColor,
    bool AlwaysWin, DateTime StartsAt, DateTime? EndsAt, bool IsActive, string LimitPeriod, int LimitCount, int CouponValidDays, int SortOrder,
    string? LabelAvailable, string? LabelCooldown, string? LabelExhausted, string? LabelLoginRequired, string? LabelEnded,
    string? WinMessage, string? WinSubMessage, string? LoseMessage, string? LoseSubMessage,
    List<GamePrizeDto> Prizes, Guid UserId) : IRequest<Result<Guid>>;

public class SaveGameCommandHandler(IPromotionDbContext db) : IRequestHandler<SaveGameCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveGameCommand r, CancellationToken ct)
    {
        if (r.FirmPlatformId == Guid.Empty) return Result.Failure<Guid>("Platform seçilmedi.");
        var code = r.Code.Trim().ToLowerInvariant();
        Game g;
        if (r.Id is { } id)
        {
            g = await db.Games.Include(x => x.Prizes).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Oyun bulunamadı.");
            g.UpdatedAt = DateTime.UtcNow; g.UpdatedBy = r.UserId;
        }
        else
        {
            g = new Game { FirmPlatformId = r.FirmPlatformId, CreatedBy = r.UserId };
            db.Games.Add(g);
        }
        if (await db.Games.AnyAsync(x => x.FirmPlatformId == r.FirmPlatformId && x.Code == code && x.Id != g.Id, ct))
            return Result.Failure<Guid>($"'{code}' kodu bu kanalda zaten kullanılıyor.");

        g.FirmPlatformId = r.FirmPlatformId; g.Code = code; g.Type = r.Type;
        g.TitleI18n = r.TitleI18n; g.SubtitleI18n = r.SubtitleI18n; g.DescriptionI18n = r.DescriptionI18n; g.RulesTextI18n = r.RulesTextI18n;
        g.CtaLabel = Bos(r.CtaLabel); g.ImageUrl = Bos(r.ImageUrl); g.ThemeColor = Bos(r.ThemeColor); g.AccentColor = Bos(r.AccentColor);
        g.RequiresLogin = true;   // v1 kararı: misafir oynayamaz (kupon üyeye bağlanır, cihaz token'ı anonim/kısa ömürlü)
        g.AlwaysWin = r.AlwaysWin; g.StartsAt = Utc(r.StartsAt); g.EndsAt = r.EndsAt is { } e ? Utc(e) : null; g.IsActive = r.IsActive;
        g.LimitPeriod = r.LimitPeriod; g.LimitCount = r.LimitCount; g.CouponValidDays = r.CouponValidDays; g.SortOrder = r.SortOrder;
        g.LabelAvailable = Bos(r.LabelAvailable) ?? "Bugün {n} hakkın var";
        g.LabelCooldown = Bos(r.LabelCooldown) ?? "Yarın tekrar gel";
        g.LabelExhausted = Bos(r.LabelExhausted) ?? "Hakkın bitti";
        g.LabelLoginRequired = Bos(r.LabelLoginRequired) ?? "Oynamak için giriş yap";
        g.LabelEnded = Bos(r.LabelEnded) ?? "Kampanya bitti";
        g.WinMessage = Bos(r.WinMessage) ?? "Tebrikler!";
        g.WinSubMessage = Bos(r.WinSubMessage) ?? "{prize} kazandın. Kuponlarım'a eklendi.";
        g.LoseMessage = Bos(r.LoseMessage) ?? "Bu sefer olmadı";
        g.LoseSubMessage = Bos(r.LoseSubMessage) ?? "Bir dahaki sefere bol şans!";

        // Ödüller: id eşleşen güncellenir, yeni eklenir, listede olmayan soft-delete
        var gelenIdler = r.Prizes.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet();
        foreach (var eski in g.Prizes.Where(p => !gelenIdler.Contains(p.Id)).ToList())
        { eski.IsDeleted = true; eski.DeletedAt = DateTime.UtcNow; eski.DeletedBy = r.UserId; }
        var sira = 0;
        var tumOduller = new List<GamePrize>();
        foreach (var dto in r.Prizes)
        {
            var p = dto.Id is { } pid ? g.Prizes.FirstOrDefault(x => x.Id == pid) : null;
            if (p is null) { p = new GamePrize { GameId = g.Id, CreatedBy = r.UserId }; db.GamePrizes.Add(p); }
            tumOduller.Add(p);
            p.Label = dto.Label.Trim(); p.ShortLabel = Bos(dto.ShortLabel); p.Kind = dto.Kind; p.Color = Bos(dto.Color); p.IconUrl = Bos(dto.IconUrl);
            p.Description = Bos(dto.Description); p.Weight = dto.Weight; p.SortOrder = sira++; p.IsActive = dto.IsActive;
            p.CouponType = dto.Kind == GamePrizeKinds.Coupon ? dto.CouponType : null;
            p.CouponValue = dto.Kind == GamePrizeKinds.Coupon ? dto.CouponValue : null;
            p.MinimumCartTotal = dto.Kind == GamePrizeKinds.Coupon ? dto.MinimumCartTotal : null;
            p.Points = dto.Kind == GamePrizeKinds.Points ? dto.Points : null;
            p.UpdatedAt = DateTime.UtcNow;
        }
        var dogrulama = SansOyunuKurali.Dogrula(g, tumOduller);
        if (dogrulama is not null) return Result.Failure<Guid>(dogrulama);

        await db.SaveChangesAsync(ct);
        return Result.Success(g.Id);
    }

    private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static DateTime Utc(DateTime d) => d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();
}

/// <summary>Oyunu siler (soft). Oynanmış oyun silinmez — pasife alınır (kupon/puan geçmişi korunur).</summary>
public record DeleteGameCommand(Guid Id, Guid UserId) : IRequest<Result<bool>>;

public class DeleteGameCommandHandler(IPromotionDbContext db) : IRequestHandler<DeleteGameCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteGameCommand r, CancellationToken ct)
    {
        var g = await db.Games.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (g is null) return Result.Failure<bool>("Oyun bulunamadı.");
        if (await db.GamePlays.AnyAsync(p => p.GameId == g.Id, ct))
            return Result.Failure<bool>("Bu oyun oynanmış; silinemez. Oyunu pasife alabilirsiniz.");
        g.IsDeleted = true; g.DeletedAt = DateTime.UtcNow; g.DeletedBy = r.UserId; g.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
