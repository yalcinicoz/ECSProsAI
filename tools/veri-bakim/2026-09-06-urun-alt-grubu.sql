-- Ürün Alt Grubu özelliği (2026-09-06): text, zorunlu değil, filtreye girmez; TÜM ürün gruplarına atanır.
-- İdempotent — tekrar çalıştırılabilir. Uygulama açılışında DatabaseSeeder.SeedUrunAltGrubuGroupAttributesAsync
-- aynı işi yapar; bu betik seed'i beklemeden veritabanına uygulamak içindir.
DO $$
DECLARE tid uuid;
BEGIN
  SELECT "Id" INTO tid FROM definition.attribute_types WHERE "Code" = 'urun_alt_grubu';
  IF tid IS NULL THEN
    tid := gen_random_uuid();
    INSERT INTO definition.attribute_types
      ("Id","Code","NameI18n","DataType","IsActive","SortOrder","UseInFilter","CreatedAt","IsDeleted")
    VALUES (tid, 'urun_alt_grubu', '{"tr":"Ürün Alt Grubu"}'::jsonb, 'text', true, 1200, false, now(), false);
  ELSE
    UPDATE definition.attribute_types
       SET "IsDeleted" = false, "DeletedAt" = NULL, "DeletedBy" = NULL, "IsActive" = true, "UpdatedAt" = now()
     WHERE "Id" = tid AND ("IsDeleted" OR NOT "IsActive");
  END IF;

  UPDATE definition.product_group_attributes
     SET "IsDeleted" = false, "DeletedAt" = NULL, "DeletedBy" = NULL, "UpdatedAt" = now()
   WHERE "AttributeTypeId" = tid AND "IsDeleted";

  INSERT INTO definition.product_group_attributes
    ("Id","ProductGroupId","AttributeTypeId","IsVariant","IsRequired","IsPrimaryAxis","SortOrder","CreatedAt","IsDeleted")
  SELECT gen_random_uuid(), g."Id", tid, false, false, false, 200, now(), false
    FROM definition.product_groups g
   WHERE NOT g."IsDeleted"
     AND NOT EXISTS (SELECT 1 FROM definition.product_group_attributes pga
                      WHERE pga."ProductGroupId" = g."Id" AND pga."AttributeTypeId" = tid);
END $$;

-- Doğrulama
SELECT (SELECT count(*) FROM definition.product_groups WHERE NOT "IsDeleted") AS grup,
       (SELECT count(*) FROM definition.product_group_attributes pga
          JOIN definition.attribute_types t ON t."Id" = pga."AttributeTypeId"
         WHERE t."Code" = 'urun_alt_grubu' AND NOT pga."IsDeleted") AS atanan;
