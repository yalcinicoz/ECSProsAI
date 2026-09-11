using ECSPros.Promotion.Domain.Entities;

namespace ECSPros.Promotion.Application.Games;

/// <summary>
/// Şans oyunları TEK KURAL (docs/BACKEND_OYUNLAR.md §3a, 2026-09-11): hak dönemi, durum + etiket, ağırlıklı ödül
/// seçimi, kazı kazan hücre kurgusu ve tanım doğrulaması. Saf fonksiyonlar — test edilir; mobil hiçbir kuralı bilmez.
/// </summary>
public static class SansOyunuKurali
{
    public static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public const string DurumAvailable = "available";
    public const string DurumCooldown = "cooldown";
    public const string DurumExhausted = "exhausted";
    public const string DurumLoginRequired = "login_required";
    public const string DurumEnded = "ended";

    /// <summary>Hak dönemi anahtarı — gün: "2026-09-11" (İstanbul günü), hafta: "2026-W37" (ISO), toplam: "total".</summary>
    public static string DonemAnahtari(DateTime utcNow, string period)
    {
        var yerel = TimeZoneInfo.ConvertTimeFromUtc(utcNow, Istanbul);
        return period switch
        {
            GameLimitPeriods.Week => $"{System.Globalization.ISOWeek.GetYear(yerel)}-W{System.Globalization.ISOWeek.GetWeekOfYear(yerel):00}",
            GameLimitPeriods.Total => "total",
            _ => yerel.ToString("yyyy-MM-dd"),
        };
    }

    /// <summary>Dönemin bittiği an (UTC) — cooldown'da `nextPlayAt`; toplam dönemde null.</summary>
    public static DateTime? DonemSonu(DateTime utcNow, string period)
    {
        var yerel = TimeZoneInfo.ConvertTimeFromUtc(utcNow, Istanbul).Date;
        if (period == GameLimitPeriods.Total) return null;
        DateTime yerelSonraki;
        if (period == GameLimitPeriods.Week)
        {
            // ISO hafta: bir sonraki Pazartesi 00:00
            var pazartesiyeKalan = ((int)DayOfWeek.Monday - (int)yerel.DayOfWeek + 7) % 7;
            yerelSonraki = yerel.AddDays(pazartesiyeKalan == 0 ? 7 : pazartesiyeKalan);
        }
        else yerelSonraki = yerel.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(yerelSonraki, DateTimeKind.Unspecified), Istanbul);
    }

    public sealed record Durum(string Status, string Label, int RemainingPlays, DateTime? NextPlayAt);

    /// <summary>Oyunun bu üye için durumu (oynanmadan önce ya da oynandıktan sonra aynı fonksiyon).</summary>
    public static Durum DurumHesapla(Game g, DateTime utcNow, bool uyeMi, int donemdeOynanan)
    {
        if (!g.IsActive || utcNow < g.StartsAt || (g.EndsAt is { } e && utcNow > e))
            return new Durum(DurumEnded, g.LabelEnded, 0, null);
        if (!uyeMi && g.RequiresLogin)
            return new Durum(DurumLoginRequired, g.LabelLoginRequired, 0, null);
        var kalan = Math.Max(0, g.LimitCount - donemdeOynanan);
        if (kalan > 0)
            return new Durum(DurumAvailable, Sablon(g.LabelAvailable, kalan, null, null), kalan, null);
        if (g.LimitPeriod == GameLimitPeriods.Total)
            return new Durum(DurumExhausted, g.LabelExhausted, 0, null);
        var sonraki = DonemSonu(utcNow, g.LimitPeriod);
        return new Durum(DurumCooldown, Sablon(g.LabelCooldown, 0, sonraki, null), 0, sonraki);
    }

    /// <summary>{n} kalan hak, {next} sonraki hak (İstanbul gün.ay), {prize} ödül adı.</summary>
    public static string Sablon(string metin, int n, DateTime? next, string? prize)
    {
        var s = metin.Replace("{n}", n.ToString());
        if (next is { } d) s = s.Replace("{next}", TimeZoneInfo.ConvertTimeFromUtc(d, Istanbul).ToString("dd.MM"));
        if (prize is not null) s = s.Replace("{prize}", prize);
        return s;
    }

    /// <summary>Ağırlıklı seçim; alwaysWin'de `none` adaylar elenir. Ağırlığı 0 olan hiç çıkmaz. Aday yoksa null.</summary>
    public static GamePrize? OdulSec(IReadOnlyList<GamePrize> prizes, bool alwaysWin, Random rng)
    {
        var adaylar = prizes.Where(p => p.IsActive && p.Weight > 0 && (!alwaysWin || p.Kind != GamePrizeKinds.None)).ToList();
        if (adaylar.Count == 0) return null;
        var toplam = adaylar.Sum(p => p.Weight);
        var r = rng.Next(toplam);
        foreach (var p in adaylar) { r -= p.Weight; if (r < 0) return p; }
        return adaylar[^1];
    }

    public sealed record Hucre(Guid PrizeId, string Label, string? Color);

    /// <summary>
    /// Kazı kazan 3×2 = 6 hücre. Kazanan kurgu: kazanan ödül tam 3 hücre, kalan 3 hücre başka ödüllerden ve hiçbiri 3 kez geçmez.
    /// Kaybeden kurgu: her değer en fazla 2 kez. En az 3 farklı (none dışı) ödül gerekir (Dogrula bunu garanti eder).
    /// </summary>
    public static List<Hucre> KaziKazanHucreleri(IReadOnlyList<GamePrize> prizes, GamePrize? kazanan, Random rng)
    {
        var havuz = prizes.Where(p => p.IsActive && p.Kind != GamePrizeKinds.None).ToList();
        var hucreler = new List<GamePrize>();
        if (kazanan is not null)
        {
            hucreler.AddRange(Enumerable.Repeat(kazanan, 3));
            var digerleri = havuz.Where(p => p.Id != kazanan.Id).ToList();
            // 3 dolgu: her değer ≤2 → en az 2 farklı dolgu ödülü gerekir
            var dolgu = new List<GamePrize>();
            for (var i = 0; i < 3 && digerleri.Count > 0; i++)
            {
                var uygun = digerleri.Where(p => dolgu.Count(x => x.Id == p.Id) < 2).ToList();
                if (uygun.Count == 0) break;
                dolgu.Add(uygun[rng.Next(uygun.Count)]);
            }
            hucreler.AddRange(dolgu);
        }
        else
        {
            // kaybeden: 6 hücre, her ödül ≤2 kez (havuz ≥3 ödül)
            for (var i = 0; i < 6; i++)
            {
                var uygun = havuz.Where(p => hucreler.Count(x => x.Id == p.Id) < 2).ToList();
                if (uygun.Count == 0) break;
                hucreler.Add(uygun[rng.Next(uygun.Count)]);
            }
        }
        // 6'ya tamamla (havuz küçükse — Dogrula normalde engeller)
        while (hucreler.Count < 6 && havuz.Count > 0) hucreler.Add(havuz[rng.Next(havuz.Count)]);
        // Karıştır
        for (var i = hucreler.Count - 1; i > 0; i--) { var j = rng.Next(i + 1); (hucreler[i], hucreler[j]) = (hucreler[j], hucreler[i]); }
        return hucreler.Select(p => new Hucre(p.Id, p.ShortLabel ?? p.Label, p.Color)).ToList();
    }

    /// <summary>Tanım doğrulaması (panel kaydı): null = geçerli, aksi hâlde hata metni.</summary>
    public static string? Dogrula(Game g, IReadOnlyList<GamePrize> prizes)
    {
        if (!GameTypes.All.Contains(g.Type)) return "Oyun tipi wheel, shake ya da scratch olmalıdır.";
        if (string.IsNullOrWhiteSpace(g.Code)) return "Oyun kodu zorunludur.";
        if (!System.Text.RegularExpressions.Regex.IsMatch(g.Code, "^[a-z0-9-]{2,50}$")) return "Oyun kodu yalnız küçük harf, rakam ve tire içerebilir.";
        if (!g.TitleI18n.Values.Any(v => !string.IsNullOrWhiteSpace(v))) return "Oyun adı zorunludur.";
        if (!GameLimitPeriods.All.Contains(g.LimitPeriod)) return "Hak dönemi day, week ya da total olmalıdır.";
        if (g.LimitCount < 1) return "Dönem başına hak en az 1 olmalıdır.";
        if (g.CouponValidDays < 1) return "Kupon geçerlilik süresi en az 1 gün olmalıdır.";
        if (g.EndsAt is { } e && e < g.StartsAt) return "Bitiş tarihi başlangıçtan önce olamaz.";
        var aktif = prizes.Where(p => p.IsActive).ToList();
        if (aktif.Count == 0) return "En az bir aktif ödül tanımlanmalıdır.";
        foreach (var p in aktif)
        {
            if (!GamePrizeKinds.Supported.Contains(p.Kind)) return $"'{p.Label}' ödül türü desteklenmiyor (coupon, points, none).";
            if (string.IsNullOrWhiteSpace(p.Label)) return "Her ödülün adı olmalıdır.";
            if (p.Weight < 0) return $"'{p.Label}' ağırlığı negatif olamaz.";
            if (p.Kind == GamePrizeKinds.Coupon)
            {
                if (p.CouponType is not ("percentage" or "fixed")) return $"'{p.Label}' kupon tipi percentage ya da fixed olmalıdır.";
                if (p.CouponValue is not > 0) return $"'{p.Label}' kupon değeri sıfırdan büyük olmalıdır.";
                if (p.CouponType == "percentage" && p.CouponValue > 100) return $"'{p.Label}' yüzde indirim 100'ü aşamaz.";
            }
            if (p.Kind == GamePrizeKinds.Points && p.Points is not > 0) return $"'{p.Label}' puan sıfırdan büyük olmalıdır.";
        }
        var kazandiran = aktif.Where(p => p.Kind != GamePrizeKinds.None).ToList();
        if (g.AlwaysWin && aktif.Any(p => p.Kind == GamePrizeKinds.None)) return "Herkes kazanır modunda 'Pas' (none) ödül tanımlanamaz.";
        if (kazandiran.Count == 0) return "En az bir kazandıran ödül olmalıdır.";
        if (!kazandiran.Any(p => p.Weight > 0)) return "Kazandıran ödüllerden en az birinin ağırlığı sıfırdan büyük olmalıdır.";
        if (g.Type == GameTypes.Scratch && kazandiran.Count < 3) return "Kazı kazanda en az 3 farklı kazandıran ödül gerekir (3×2 kurgu: kazanan 3 hücre + hiçbir değer 3 kez geçmeyen dolgu).";
        if (g.Type == GameTypes.Wheel && aktif.Count < 2) return "Çarkta en az 2 dilim olmalıdır.";
        return null;
    }
}
