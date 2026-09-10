-- 2026-09-10: Eski sistem (juludedb.cctaksitler, bankaId=26 PayTR) kanal bazlı müşteri taksit tablolarının
-- yeni kanal ayarına (core_firm_platforms.Settings.installmentTable) aktarımı. İDEMPOTENT: tabloyu yazar,
-- installmentSource'a DOKUNMAZ (varsayılan "provider" kalır — kaynağı yetkili personel panelden seçer).
-- Eşleme legacyPlatformId ile: tozlu=1, julude=2, olutbutik=12, mishar=41. Eski platform 14'ün yeni kanalı yok (atlandı).
-- Çalıştırma: psql -h localhost -U ecommerce -d ecommerce_db -f tools/veri-bakim/2026-09-10-taksit-tablosu-aktarimi.sql
BEGIN;
WITH tablo(legacy_id, tbl) AS (VALUES
  (1,  '[{"count":2,"rate":0},{"count":3,"rate":0},{"count":4,"rate":12.11},{"count":5,"rate":14.49},{"count":6,"rate":17.00},{"count":7,"rate":19.69},{"count":8,"rate":22.43},{"count":9,"rate":25.38},{"count":10,"rate":28.39},{"count":11,"rate":31.58},{"count":12,"rate":34.95}]'::jsonb),
  (2,  '[{"count":2,"rate":7.99},{"count":3,"rate":10.42},{"count":4,"rate":12.96},{"count":5,"rate":15.63},{"count":6,"rate":18.41},{"count":7,"rate":21.33},{"count":8,"rate":24.41},{"count":9,"rate":27.67},{"count":10,"rate":31.04},{"count":11,"rate":34.64},{"count":12,"rate":38.45}]'::jsonb),
  (12, '[{"count":2,"rate":7.99},{"count":3,"rate":10.42},{"count":4,"rate":12.96},{"count":5,"rate":15.63},{"count":6,"rate":18.41},{"count":7,"rate":21.33},{"count":8,"rate":24.41},{"count":9,"rate":27.67},{"count":10,"rate":31.04},{"count":11,"rate":34.64},{"count":12,"rate":38.45}]'::jsonb),
  (41, '[{"count":2,"rate":8.40},{"count":3,"rate":10.44},{"count":4,"rate":12.47},{"count":5,"rate":14.52},{"count":6,"rate":16.55}]'::jsonb)
)
UPDATE core.core_firm_platforms fp
SET "Settings" = COALESCE(fp."Settings", '{}'::jsonb) || jsonb_build_object('installmentTable', t.tbl),
    "UpdatedAt" = now()
FROM tablo t
WHERE fp."IsDeleted" = false
  AND (fp."Settings"->>'legacyPlatformId')::int = t.legacy_id;
SELECT "Code", "Settings"->>'installmentSource' AS kaynak, jsonb_array_length("Settings"->'installmentTable') AS satir
FROM core.core_firm_platforms WHERE "Settings" ? 'installmentTable' ORDER BY 1;
COMMIT;
