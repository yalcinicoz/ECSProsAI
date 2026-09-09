using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Iam.Application.Yetkilendirme;

/// <summary>
/// Y5 (2026-09-09, tasarım §J): yetki olaylarının DENETİM KAYDI.
///
/// Kurallar:
///  • Kayıt YALNIZ EKLENİR. Uygulamada bu satırları güncelleyen/silen hiçbir yol yoktur.
///  • Panelde ham JSON değil, İNSAN DİLİYLE cümle gösterilir → <see cref="Ozet"/> alanı zorunludur.
///  • Süper adminin işlemleri de yazılır (bypass audit'i bypass etmez, K5).
///  • Kanal kapsamı değişiklikleri "önce → sonra" olarak saklanır.
///
/// Kayıtlar mevcut <c>iam.iam_audit_logs</c> tablosunda, <c>EntityType</c> ailesi
/// <c>yetki.*</c> ile tutulur; genel uygulama loglarından bu önekle ayrılır.
/// </summary>
public interface IYetkiDenetimi
{
    Task YazAsync(YetkiDenetimKaydi kayit, CancellationToken ct = default);
}

/// <param name="Olay">yetki.grup.olustur | yetki.grup.guncelle | yetki.grup.yetkiler |
/// yetki.grup.uye.ekle | yetki.grup.uye.cikar | yetki.kullanici.istisna |
/// yetki.katalog.guncelle | yetki.superadmin.ver | yetki.superadmin.kaldir | yetki.simulasyon</param>
/// <param name="Ozet">Panelde gösterilecek insan-okur cümle.</param>
public record YetkiDenetimKaydi(
    string Olay,
    string Ozet,
    Guid? HedefKullaniciId = null,
    Guid? HedefGrupId = null,
    Guid? YetkiId = null,
    string? Oncesi = null,
    string? Sonrasi = null);

public class YetkiDenetimi(IIamDbContext db, IYetkiDenetimBaglami baglam) : IYetkiDenetimi
{
    public async Task YazAsync(YetkiDenetimKaydi kayit, CancellationToken ct = default)
    {
        var context = new Dictionary<string, object>
        {
            ["ozet"] = kayit.Ozet,
        };
        if (kayit.HedefKullaniciId is { } hk) context["hedefKullaniciId"] = hk;
        if (kayit.HedefGrupId is { } hg) context["hedefGrupId"] = hg;
        if (kayit.YetkiId is { } yid) context["yetkiId"] = yid;

        db.AuditLogs.Add(new AuditLog
        {
            UserId = baglam.AktorId,
            EntityType = kayit.Olay,                                   // "yetki.*" ailesi
            EntityId = kayit.HedefKullaniciId ?? kayit.HedefGrupId ?? kayit.YetkiId ?? Guid.Empty,
            Action = kayit.Olay.Split('.').Last(),
            OldValues = kayit.Oncesi is null ? null : new Dictionary<string, object> { ["deger"] = kayit.Oncesi },
            NewValues = kayit.Sonrasi is null ? null : new Dictionary<string, object> { ["deger"] = kayit.Sonrasi },
            IpAddress = baglam.Ip,
            UserAgent = baglam.UserAgent,
            Context = context,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>İsteği yapan kullanıcı ve ağ bilgisi — API katmanı doldurur.</summary>
public interface IYetkiDenetimBaglami
{
    Guid? AktorId { get; }
    string? Ip { get; }
    string? UserAgent { get; }
}
