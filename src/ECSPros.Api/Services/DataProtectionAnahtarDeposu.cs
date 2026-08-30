using System.Xml.Linq;
using ECSPros.Iam.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services;

/// <summary>
/// FAZ 10 / A1 — Data Protection key ring deposu: birincil depo DB
/// (iam.data_protection_keys, EF repository); eski dosya deposu (~/.ecspros/dp-keys)
/// bir sürüm boyunca SALT-OKUNUR geri dönüş yolu olarak okunmaya devam eder.
/// Yazma her zaman yalnız DB'ye gider. DB okunamazsa (tablo henüz migrate edilmemiş,
/// bağlantı yok) dosya anahtarlarıyla devam edilir — mevcut şifreli kimlik bilgileri
/// çözülebilir kalır; durum hata olarak loglanır.
/// </summary>
public sealed class DbOncelikliDosyaYedekliXmlRepository : IXmlRepository
{
    private readonly IXmlRepository _birincil;   // DB (EF)
    private readonly IXmlRepository _dosya;      // eski dosya deposu (salt-okunur)
    private readonly ILogger _logger;

    public DbOncelikliDosyaYedekliXmlRepository(
        IXmlRepository birincil, IXmlRepository dosya, ILogger logger)
    {
        _birincil = birincil;
        _dosya = dosya;
        _logger = logger;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        List<XElement> sonuc;
        try
        {
            sonuc = _birincil.GetAllElements().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Data Protection anahtarları DB'den okunamadı — dosya deposuyla devam ediliyor " +
                "(iam.data_protection_keys migrate edildi mi?)");
            sonuc = new List<XElement>();
        }

        var idler = new HashSet<string>(
            sonuc.Select(e => (string?)e.Attribute("id"))
                 .Where(id => id is not null)!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var eski in _dosya.GetAllElements())
        {
            var id = (string?)eski.Attribute("id");
            if (id is null || idler.Add(id))
                sonuc.Add(eski);
        }
        return sonuc;
    }

    public void StoreElement(XElement element, string friendlyName)
        => _birincil.StoreElement(element, friendlyName);
}

/// <summary>
/// FAZ 10 / A1 — açılışta dosya deposundaki (~/.ecspros/dp-keys/key-*.xml) anahtarları
/// iam.data_protection_keys tablosuna bir kez kopyalar (idempotent — aynı key id ikinci
/// kez eklenmez). Dosyalar SİLİNMEZ; tablo yoksa/DB kapalıysa uyarı loglar, açılışı bozmaz
/// (dosya geri dönüş yolu devrededir).
/// </summary>
public static class DataProtectionDosyaAnahtarAktarici
{
    public static async Task AktarAsync(IServiceProvider services, string dosyaYolu)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DataProtectionDosyaAnahtarAktarici");
        try
        {
            if (!Directory.Exists(dosyaYolu)) return;
            var dosyalar = Directory.EnumerateFiles(dosyaYolu, "key-*.xml").ToList();
            if (dosyalar.Count == 0) return;

            var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            var dbdekiler = await db.DataProtectionKeys.AsNoTracking()
                .Select(k => k.Xml).ToListAsync();
            var dbIdler = new HashSet<string>(
                dbdekiler.Where(x => x is not null)
                         .Select(x => (string?)XElement.Parse(x!).Attribute("id"))
                         .Where(id => id is not null)!,
                StringComparer.OrdinalIgnoreCase);

            var eklenen = 0;
            foreach (var dosya in dosyalar)
            {
                var xml = await File.ReadAllTextAsync(dosya);
                var id = (string?)XElement.Parse(xml).Attribute("id");
                if (id is null || dbIdler.Contains(id)) continue;

                db.DataProtectionKeys.Add(new DataProtectionKey
                {
                    FriendlyName = Path.GetFileNameWithoutExtension(dosya),
                    Xml = xml,
                });
                eklenen++;
            }

            if (eklenen > 0)
            {
                await db.SaveChangesAsync();
                logger.LogInformation(
                    "Data Protection: {Adet} dosya anahtarı DB'ye aktarıldı (iam.data_protection_keys).",
                    eklenen);
            }
        }
        catch (Exception ex)
        {
            // Açılışı bozma — dosya deposu geri dönüş yolu olarak okunmaya devam ediyor.
            logger.LogWarning(ex,
                "Data Protection dosya anahtarları DB'ye aktarılamadı; dosya deposu kullanılmaya devam edecek.");
        }
    }
}
