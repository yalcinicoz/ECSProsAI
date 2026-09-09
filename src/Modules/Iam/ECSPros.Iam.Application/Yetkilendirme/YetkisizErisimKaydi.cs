using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Iam.Application.Yetkilendirme;

/// <summary>
/// Y8 son adım (2026-09-09, tasarım §J.1): <b>yetkisiz erişim denemelerinin</b> kaydı.
///
/// Neden gerekli: default-deny sessizdir. Bir kullanıcı yetkisi olmayan ekranı ısrarla
/// deniyorsa bu ya yanlış yetkilendirmedir (kişi işini yapamıyor) ya da kötüye kullanımdır;
/// ikisi de görülmeden yönetilemez.
///
/// Neden ÖRNEKLENEREK: bu olay istemci tarafından tetiklenir — tek bir döngü dakikada binlerce
/// satır yazabilirdi. <see cref="YetkisizErisimOrnekleyici"/> aynı (kullanıcı + yetki + uç)
/// üçlüsünü pencere başına BİR kez yazar, penceredeki diğer denemeleri sayar ve bir sonraki
/// kayda "atlanan" olarak taşır. Böylece hem sinyal kaybolmaz hem tablo şişmez.
/// </summary>
/// <param name="Tur">yetki (403) | kanal (404) | kayit (404, toplu işlemde kapsam dışı kayıt) | bildirim (SignalR)</param>
/// <param name="YetkiKey">Reddin dayandığı yetki key'i (ör. "orders.manage").</param>
/// <param name="Yol">HTTP yolu ya da hub konusu.</param>
/// <param name="Durum">Üretilen HTTP durumu (403/404; SignalR'da 0).</param>
/// <param name="AtlananDeneme">Bu kayıttan önce aynı anahtarla yapılıp yazılmayan deneme sayısı.</param>
public record YetkisizErisimKaydi(
    string Tur, string YetkiKey, string Yol, string Yontem, int Durum, int AtlananDeneme);

public interface IYetkisizErisimKaydedici
{
    /// <summary>Örnekleme kararını da içerir: pencere doluysa yalnız sayaç artar, DB'ye yazılmaz.</summary>
    Task DeneAsync(YetkisizErisimKaydi kayit, Guid kullaniciId, CancellationToken ct = default);
}

/// <summary>
/// Pencere bazlı örnekleyici (singleton). Aynı anahtar için pencere içinde tek kayıt yazılır;
/// arada gelen denemeler sayılır ve bir sonraki yazımda rapor edilir.
///
/// Bellek sınırı: saldırgan yolu/yetkiyi değiştirerek anahtar üretebilir; bu yüzden sözlük
/// <see cref="MaksAnahtar"/> ile sınırlıdır ve dolduğunda süresi geçmiş anahtarlar temizlenir,
/// yine dolu kalırsa yeni anahtarlar YAZILIR ama izlenmez (kayıt kaybı yerine bellek güvenliği).
/// </summary>
public sealed class YetkisizErisimOrnekleyici
{
    public static readonly TimeSpan Pencere = TimeSpan.FromMinutes(10);
    private const int MaksAnahtar = 5000;

    private sealed class Sayac
    {
        public DateTime SonYazim;
        public int Atlanan;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Sayac> _sayaclar = new();

    /// <summary>Yazılmalı mı? Yazılacaksa penceredeki atlanan deneme sayısı döner.</summary>
    public bool YazilsinMi(string anahtar, out int atlanan)
    {
        var simdi = DateTime.UtcNow;
        atlanan = 0;

        if (_sayaclar.TryGetValue(anahtar, out var sayac))
        {
            lock (sayac)
            {
                if (simdi - sayac.SonYazim < Pencere)
                {
                    sayac.Atlanan++;
                    return false;
                }
                atlanan = sayac.Atlanan;
                sayac.Atlanan = 0;
                sayac.SonYazim = simdi;
                return true;
            }
        }

        if (_sayaclar.Count >= MaksAnahtar) Temizle(simdi);
        if (_sayaclar.Count < MaksAnahtar)
            _sayaclar.TryAdd(anahtar, new Sayac { SonYazim = simdi, Atlanan = 0 });

        return true;   // ilk deneme her zaman yazılır
    }

    private void Temizle(DateTime simdi)
    {
        foreach (var (k, v) in _sayaclar)
            if (simdi - v.SonYazim > Pencere)
                _sayaclar.TryRemove(k, out _);
    }
}

public class YetkisizErisimKaydedici(
    IIamDbContext db, IYetkiDenetimBaglami baglam, YetkisizErisimOrnekleyici ornekleyici)
    : IYetkisizErisimKaydedici
{
    public async Task DeneAsync(YetkisizErisimKaydi kayit, Guid kullaniciId, CancellationToken ct = default)
    {
        var anahtar = $"{kullaniciId}|{kayit.Tur}|{kayit.YetkiKey}|{kayit.Yontem} {kayit.Yol}";
        if (!ornekleyici.YazilsinMi(anahtar, out var atlanan)) return;

        var kullaniciAdi = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.Users.Where(u => u.Id == kullaniciId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim()), ct) ?? "Kullanıcı";

        var sebep = kayit.Tur switch
        {
            "kanal" => "kapsamı dışındaki bir satış kanalına",
            "kayit" => "kapsamı dışındaki kayıtlara",
            "bildirim" => "yetkisi olmayan bir bildirim konusuna",
            _ => "yetkisi olmayan bir ekrana/işleme",
        };
        var ozet = $"{kullaniciAdi} {sebep} erişmeye çalıştı: {kayit.Yontem} {kayit.Yol} " +
                   $"(gereken yetki: {kayit.YetkiKey}" +
                   (kayit.Durum > 0 ? $", yanıt: {kayit.Durum}" : "") + ")." +
                   (atlanan > 0
                       ? $" Son {(int)YetkisizErisimOrnekleyici.Pencere.TotalMinutes} dakikada aynı denemeden {atlanan} tane daha oldu."
                       : "");

        db.AuditLogs.Add(new AuditLog
        {
            UserId = kullaniciId,
            EntityType = "yetki.reddedildi",
            EntityId = Guid.Empty,
            Action = "reddedildi",
            NewValues = new Dictionary<string, object>
            {
                ["tur"] = kayit.Tur,
                ["yetki"] = kayit.YetkiKey,
                ["yol"] = kayit.Yol,
                ["yontem"] = kayit.Yontem,
                ["durum"] = kayit.Durum,
                ["atlananDeneme"] = atlanan,
            },
            IpAddress = baglam.Ip,
            UserAgent = baglam.UserAgent,
            Context = new Dictionary<string, object> { ["ozet"] = ozet },
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
