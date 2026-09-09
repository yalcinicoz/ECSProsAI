using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Iam.Application.Yetkilendirme;

/// <summary>
/// Yetki logları DataGrid şeması (2026-09-09).
///
/// ★ Kaynak <c>AuditLogs</c>'un <c>EntityType</c>'ı "yetki." ile başlayan satırlarıdır; ekranda
/// gösterilen <c>Aktor</c>/<c>HedefKullanici</c>/<c>HedefGrup</c>/<c>Yetki</c> ve <c>Ozet</c> alanları
/// handler'da AYRI sorgularla / <c>Context</c> jsonb'sinden çözülür — bu yüzden onlar SIRALANAMAZ.
/// Sıralanabilirler: tarih, olay kodu, IP.
///
/// ★ <c>Context</c> alanı <c>Dictionary&lt;string, object&gt;</c> olduğu için özet metni SQL'e
/// çevrilemez: ne DbFunction parametresi olabilir (EF model doğrulaması reddediyor) ne de indeksleyicisi
/// çevrilir. Bu yüzden özet üzerinden sıralama/filtre YOK — denendi, <c>GridSchemasDbTests</c> yakaladı.
///
/// ★ DÜZELTME (arama): eskiden özet aramasi BELLEKTE, sayfalamadan SONRA yapılıyordu; <c>toplam</c>
/// filtresiz sayıldığı için arama sayfalamayı bozuyordu (yalnız o sayfadaki eşleşmeler görünür, sayı
/// yanlış). Artık arama terimi ÖNCE kullanıcı/grup/yetki kayıtlarına çözülüyor, sonra DB'de
/// <c>UserId</c>/<c>EntityId</c>/<c>EntityType</c> üzerinden süzülüyor — sayım da aynı küme üzerinde.
/// </summary>
public static class YetkiLogGrid
{
    public static readonly GridSchema<AuditLog> Schema = new GridSchema<AuditLog>()
        .Text("olay", a => a.EntityType)
        .Text("ip", a => a.IpAddress)
        .Date("tarih", a => a.CreatedAt)
        .Guid("aktorId", a => a.UserId)
        .Guid("hedefId", a => a.EntityId)
        .Sort("tarih", a => a.CreatedAt)
        .Sort("olay", a => a.EntityType)
        .Sort("ip", a => a.IpAddress)
        .DefaultSort(a => a.CreatedAt, desc: true)
        .TieBreaker(a => a.Id);

    /// <summary>Yalnız yetki olayları — ekranın kapsamı budur, her zaman uygulanır.</summary>
    public static IQueryable<AuditLog> YalnizYetkiOlaylari(IQueryable<AuditLog> query)
        => query.Where(a => a.EntityType.StartsWith("yetki."));
}
