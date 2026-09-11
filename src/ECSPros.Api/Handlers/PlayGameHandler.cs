using System.Text.Json;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Promotion.Application.Games;
using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Handlers;

/// <summary>
/// POST /api/store/games/{code}/play — docs/BACKEND_OYUNLAR.md §3/§3a. Sonucu SUNUCU belirler:
/// ödül ağırlıklı seçilir, kupon/puan YANIT DÖNMEDEN ÖNCE üyenin hesabına işlenir, hak düşer.
/// İdempotency (§3a-5): dönemde hak bitmişse yeni ödül ÜRETİLMEZ — o dönemin son oynanışı aynı playId ve sonuçla döner.
/// Api katmanında: puan ödülü CRM (LoyaltyAccount) ister, Promotion modülü CRM'i bilmez.
/// </summary>
public record PlayGameCommand(Guid FirmPlatformId, string Code, Guid MemberId) : IRequest<Result<StorePlayResultDto>>;

public class PlayGameHandler(IPromotionDbContext promo, ICrmDbContext crm) : IRequestHandler<PlayGameCommand, Result<StorePlayResultDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<StorePlayResultDto>> Handle(PlayGameCommand r, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var code = r.Code.Trim().ToLowerInvariant();
        var g = await promo.Games.Include(x => x.Prizes).FirstOrDefaultAsync(x => x.FirmPlatformId == r.FirmPlatformId && x.Code == code, ct);
        if (g is null) return Result.Failure<StorePlayResultDto>("Oyun bulunamadı.");

        var key = SansOyunuKurali.DonemAnahtari(now, g.LimitPeriod);
        var donemdekiler = await promo.GamePlays.Where(p => p.GameId == g.Id && p.MemberId == r.MemberId && p.PeriodKey == key)
            .OrderByDescending(p => p.PlayedAt).ToListAsync(ct);
        var durum = SansOyunuKurali.DurumHesapla(g, now, true, donemdekiler.Count);
        if (durum.Status == SansOyunuKurali.DurumEnded) return Result.Failure<StorePlayResultDto>(g.LabelEnded);
        if (durum.Status != SansOyunuKurali.DurumAvailable)
        {
            // Hak bitti: son sonucu aynen döndür (ağ kopması / çift dokunuş) — ikinci ödül asla üretilmez
            var son = donemdekiler.FirstOrDefault();
            if (son is null) return Result.Failure<StorePlayResultDto>(durum.Label);
            return Result.Success(await SonucAsync(g, son, durum, ct));
        }

        var oduller = g.Prizes.Where(p => !p.IsDeleted).OrderBy(p => p.SortOrder).ToList();
        var rng = Random.Shared;
        var secilen = SansOyunuKurali.OdulSec(oduller, g.AlwaysWin, rng);
        if (secilen is null) return Result.Failure<StorePlayResultDto>("Oyun ödülleri tanımlı değil.");
        var kazandi = secilen.Kind != GamePrizeKinds.None;

        var play = new GamePlay
        {
            GameId = g.Id, FirmPlatformId = r.FirmPlatformId, MemberId = r.MemberId, PeriodKey = key,
            PrizeId = secilen.Id, Won = kazandi, PlayedAt = now, CreatedBy = r.MemberId,
        };
        if (g.Type == GameTypes.Scratch)
        {
            var hucreler = SansOyunuKurali.KaziKazanHucreleri(oduller, kazandi ? secilen : null, rng);
            play.CellsJson = JsonSerializer.Serialize(hucreler.Select(h => new StorePlayCellDto(h.PrizeId.ToString(), h.Label, h.Color)), Json);
        }

        // Ödül YANITTAN ÖNCE hesaba işlenir (§3a-1)
        if (kazandi && secilen.Kind == GamePrizeKinds.Coupon)
        {
            var kupon = await KuponUretAsync(g, secilen, r.MemberId, now, ct);
            play.CouponId = kupon.Id; play.CouponCode = kupon.Code;
        }
        else if (kazandi && secilen.Kind == GamePrizeKinds.Points && secilen.Points is { } puan && puan > 0)
        {
            await PuanEkleAsync(r.MemberId, puan, play.Id, g, ct);
            play.PointsGiven = puan;
        }
        promo.GamePlays.Add(play);
        await promo.SaveChangesAsync(ct);
        if (play.PointsGiven is not null) await crm.SaveChangesAsync(ct);

        var yeniDurum = SansOyunuKurali.DurumHesapla(g, now, true, donemdekiler.Count + 1);
        return Result.Success(await SonucAsync(g, play, yeniDurum, ct));
    }

    private async Task<StorePlayResultDto> SonucAsync(Game g, GamePlay play, SansOyunuKurali.Durum durum, CancellationToken ct)
    {
        var prize = g.Prizes.FirstOrDefault(p => p.Id == play.PrizeId) ?? new GamePrize { Id = Guid.Empty, Label = "Pas", Kind = GamePrizeKinds.None };
        DateTime? validUntil = null;
        if (play.CouponId is { } cid)
            validUntil = await promo.Coupons.Where(c => c.Id == cid).Select(c => c.EndsAt).FirstOrDefaultAsync(ct);
        var amountText = prize.Kind == GamePrizeKinds.Coupon
            ? (prize.CouponType == "percentage" ? $"%{prize.CouponValue:0.##}" : $"{prize.CouponValue:0.##} TL")
            : prize.Kind == GamePrizeKinds.Points ? $"{prize.Points} puan" : null;
        var aciklama = prize.Description ?? (prize.MinimumCartTotal is { } m && m > 0 ? $"{m:0.##} TL ve üzeri alışverişlerde geçerli" : null);
        var cells = play.CellsJson is null ? null : JsonSerializer.Deserialize<List<StorePlayCellDto>>(play.CellsJson, Json);
        return new StorePlayResultDto(
            play.Id.ToString(), prize.Id.ToString(), play.Won,
            new StorePlayPrizeDto(prize.Id.ToString(), prize.Label, prize.Kind, aciklama, play.CouponCode, validUntil, amountText, play.PointsGiven),
            play.Won ? g.WinMessage : g.LoseMessage,
            play.Won ? SansOyunuKurali.Sablon(g.WinSubMessage, durum.RemainingPlays, durum.NextPlayAt, prize.Label)
                     : SansOyunuKurali.Sablon(g.LoseSubMessage, durum.RemainingPlays, durum.NextPlayAt, null),
            durum.RemainingPlays, durum.NextPlayAt, durum.Status, durum.Label, cells);
    }

    /// <summary>Kişiye özel kupon (MemberId bağlı → GET /account/coupons'ta görünür; KuponHedefKurali başkasına kapatır). Tek kullanımlık.</summary>
    private async Task<Coupon> KuponUretAsync(Game g, GamePrize prize, Guid memberId, DateTime now, CancellationToken ct)
    {
        const string alfabe = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var onEk = new string(g.Code.ToUpperInvariant().Where(char.IsLetterOrDigit).Take(6).ToArray());
        var deger = prize.CouponType == "percentage" ? $"{prize.CouponValue:0}" : $"{prize.CouponValue:0}";
        string kod;
        do
        {
            var ek = new string(Enumerable.Range(0, 4).Select(_ => alfabe[Random.Shared.Next(alfabe.Length)]).ToArray());
            kod = $"{onEk}{deger}-{ek}";
        } while (await promo.Coupons.IgnoreQueryFilters().AnyAsync(c => c.Code == kod, ct));
        var kupon = new Coupon
        {
            Code = kod, NameI18n = new() { ["tr"] = prize.Label }, CouponType = prize.CouponType!, DiscountValue = prize.CouponValue ?? 0,
            UsageLimitTotal = 1, UsageLimitPerMember = 1, MinimumCartTotal = prize.MinimumCartTotal, ValidForFirstOrderOnly = false,
            StartsAt = now, EndsAt = now.AddDays(g.CouponValidDays), MemberId = memberId, IsActive = true, CreatedBy = memberId,
        };
        promo.Coupons.Add(kupon);
        return kupon;
    }

    private async Task PuanEkleAsync(Guid memberId, int puan, Guid playId, Game g, CancellationToken ct)
    {
        var hesap = await crm.LoyaltyAccounts.FirstOrDefaultAsync(a => a.MemberId == memberId, ct);
        if (hesap is null) { hesap = new LoyaltyAccount { MemberId = memberId }; crm.LoyaltyAccounts.Add(hesap); }
        hesap.TotalPoints += puan; hesap.AvailablePoints += puan; hesap.UpdatedAt = DateTime.UtcNow;
        crm.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = hesap.Id, TransactionType = "earn", Points = puan, BalanceAfter = hesap.AvailablePoints,
            ReferenceType = "game_play", ReferenceId = playId, Notes = $"Şans oyunu: {g.Code}",
        });
    }
}
