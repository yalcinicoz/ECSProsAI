using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

/// <summary>
/// Şans oyunu (docs/BACKEND_OYUNLAR.md, 2026-09-11): Çarkıfelek (wheel) · Salla Kazan (shake) · Kazı Kazan (scratch).
/// Günlük kampanya kurgusu: panelde tanımlanır, tarih aralığında + aktifken mobil ana sayfada ikon çıkar.
/// Sonucu HER ZAMAN sunucu belirler (ödül seçimi, kupon üretimi, hak düşümü) — mobil yalnız animasyon oynatır.
/// </summary>
public class Game : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    /// <summary>URL/push anahtarı (`/oyunlar/{code}`); kanal içinde benzersiz, küçük harf.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>wheel | shake | scratch</summary>
    public string Type { get; set; } = GameTypes.Wheel;
    public Dictionary<string, string> TitleI18n { get; set; } = new();
    public Dictionary<string, string>? SubtitleI18n { get; set; }
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public Dictionary<string, string>? RulesTextI18n { get; set; }
    public string? CtaLabel { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThemeColor { get; set; }
    public string? AccentColor { get; set; }
    /// <summary>v1: hep true — misafir oynayamaz (kupon üyeye bağlanır; cihaz token'ı kısa ömürlü/anonim, hak takibi yapılamaz).</summary>
    public bool RequiresLogin { get; set; } = true;
    /// <summary>Herkes kazanır modu: `none` ödül tanımlanamaz, play asla won:false dönmez.</summary>
    public bool AlwaysWin { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Hak dönemi: day | week | total (kampanya boyunca toplam).</summary>
    public string LimitPeriod { get; set; } = GameLimitPeriods.Day;
    /// <summary>Dönem başına hak sayısı.</summary>
    public int LimitCount { get; set; } = 1;
    /// <summary>Kazanılan kuponun geçerlilik süresi (gün).</summary>
    public int CouponValidDays { get; set; } = 7;
    public int SortOrder { get; set; }

    // Kullanıcıya gösterilen metinler — mobil metin üretmez, hepsi panelden ({n} = kalan hak, {next} = sonraki hak tarihi, {prize} = ödül adı)
    public string LabelAvailable { get; set; } = "Bugün {n} hakkın var";
    public string LabelCooldown { get; set; } = "Yarın tekrar gel";
    public string LabelExhausted { get; set; } = "Hakkın bitti";
    public string LabelLoginRequired { get; set; } = "Oynamak için giriş yap";
    public string LabelEnded { get; set; } = "Kampanya bitti";
    public string WinMessage { get; set; } = "Tebrikler!";
    public string WinSubMessage { get; set; } = "{prize} kazandın. Kuponlarım'a eklendi.";
    public string LoseMessage { get; set; } = "Bu sefer olmadı";
    public string LoseSubMessage { get; set; } = "Bir dahaki sefere bol şans!";

    public ICollection<GamePrize> Prizes { get; set; } = new List<GamePrize>();
    public ICollection<GamePlay> Plays { get; set; } = new List<GamePlay>();
}

public static class GameTypes
{
    public const string Wheel = "wheel";
    public const string Shake = "shake";
    public const string Scratch = "scratch";
    public static readonly string[] All = { Wheel, Shake, Scratch };
}

public static class GameLimitPeriods
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Total = "total";
    public static readonly string[] All = { Day, Week, Total };
}

public static class GamePrizeKinds
{
    public const string Coupon = "coupon";
    public const string Points = "points";
    public const string None = "none";
    /// <summary>v1'de desteklenen türler (kupon motoru yalnız percentage/fixed bilir; free_shipping/product sonraki tur).</summary>
    public static readonly string[] Supported = { Coupon, Points, None };
}

/// <summary>Oyun ödülü — çarkta dilim (SortOrder = dilim sırası), kazı kazanda kutucuk değeri.</summary>
public class GamePrize : BaseEntity
{
    public Guid GameId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? ShortLabel { get; set; }
    /// <summary>coupon | points | none (bkz. GamePrizeKinds)</summary>
    public string Kind { get; set; } = GamePrizeKinds.Coupon;
    public string? Color { get; set; }
    public string? IconUrl { get; set; }
    public string? Description { get; set; }
    /// <summary>Olasılık ağırlığı (0 = hiç çıkmaz; kazı kazanda yalnız dolgu olarak kullanılır).</summary>
    public int Weight { get; set; } = 1;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    // coupon: percentage | fixed + değer + (opsiyonel) asgari sepet
    public string? CouponType { get; set; }
    public decimal? CouponValue { get; set; }
    public decimal? MinimumCartTotal { get; set; }
    // points
    public int? Points { get; set; }

    public Game Game { get; set; } = null!;
}

/// <summary>Oynanış kaydı — hak düşümü + idempotent sonuç (aynı dönemde hak bitince son sonuç aynen döner).</summary>
public class GamePlay : BaseEntity
{
    public Guid GameId { get; set; }
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    /// <summary>Hak dönemi anahtarı: "2026-09-11" (gün, İstanbul) · "2026-W37" (hafta) · "total".</summary>
    public string PeriodKey { get; set; } = string.Empty;
    public Guid? PrizeId { get; set; }
    public bool Won { get; set; }
    public Guid? CouponId { get; set; }
    public string? CouponCode { get; set; }
    public int? PointsGiven { get; set; }
    /// <summary>Kazı kazan 6 hücre (JSON dizi: prizeId, label, color) — aynı sonuç tekrar dönsün diye saklanır.</summary>
    public string? CellsJson { get; set; }
    public DateTime PlayedAt { get; set; }

    public Game Game { get; set; } = null!;
    public GamePrize? Prize { get; set; }
}
