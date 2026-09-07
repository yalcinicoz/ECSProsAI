-- Salt-okunur envanter: eski CreateStockCard.UrunSinifBulAsync ilişkileri.
-- Mevcut MySQL istemcisinde juludedb seçilerek, tercihen SELECT-only hesapla çalıştırın.
-- Şema veya veri değişikliği yapmaz. Sonuç ızgarasını CSV olarak dışa aktarın.
-- Eski metodun 1/1/493 fallback'i ve Rows[0] seçimi burada YOKTUR.
-- MySQL grup ID/kodu, V3 AttributeCode değildir.

START TRANSACTION READ ONLY;

SELECT
    us.Id AS sinifId,
    us.cinsiyetId,
    c.cinsiyet,
    ug.Id AS mysqlGrupId,
    ug.kod AS mysqlGrupKod,
    ug.aciklama AS mysqlGrupAdi,
    ua.Id AS altGrupId,
    ua.aciklama AS v3GrupAdi
FROM juludedb.dfurunsiniflari us
JOIN juludedb.dfurungruplari ug ON ug.urunSinifId = us.Id
JOIN juludedb.dfurunaltgruplari ua ON ua.urunGrupId = ug.Id
LEFT JOIN juludedb.dfcinsiyetler c ON c.Id = us.cinsiyetId
ORDER BY ua.aciklama, us.cinsiyetId, ug.Id, ua.Id;

ROLLBACK;
