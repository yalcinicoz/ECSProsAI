using ECSPros.Order.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Y3 3. tur (2026-09-09, tasarım §O.8): TOPLU İŞLEMLERDE kayıt-bazlı kapsam doğrulaması.
///
/// Liste filtrelenmiş olsa bile istemciden gelen kimlik listesine güvenilmez: kullanıcı
/// isteği elle üretip kapsam dışı bir siparişi işleme sokabilir. Bu yüzden toplu uçlar,
/// gelen HER kaydın kanalının kullanıcının kapsamında olduğunu yeniden doğrular.
/// </summary>
public static class KapsamDogrulama
{
    /// <summary>
    /// Verilen siparişlerin tamamı kullanıcının kapsamında mı? Değilse kapsam dışı olanların
    /// sayısı döner (çağıran 404/400 üretir — hangi siparişin var olduğunu sızdırmadan).
    /// </summary>
    public static async Task<int> KapsamDisiSiparisSayisiAsync(
        IKanalKapsami kapsam, IOrderDbContext db, string permissionKey,
        IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return 0;

        var izinli = await kapsam.KanallarAsync(permissionKey, ct);
        if (izinli is null) return 0;                       // kısıtsız (süper admin / kapsamsız yetki)
        if (izinli.Count == 0) return orderIds.Count;       // hiç kanal yok → hepsi kapsam dışı

        var idListesi = orderIds as IList<Guid> ?? orderIds.ToList();
        var izinliListe = izinli as IList<Guid> ?? izinli.ToList();

        // Kapsam İÇİNDEKİLERİ sayar; fark kapsam dışıdır (var olmayan kimlik de kapsam dışı sayılır).
        var kapsamIci = await db.Orders.AsNoTracking()
            .Where(o => idListesi.Contains(o.Id) && izinliListe.Contains(o.FirmPlatformId))
            .CountAsync(ct);

        return idListesi.Count - kapsamIci;
    }
}
