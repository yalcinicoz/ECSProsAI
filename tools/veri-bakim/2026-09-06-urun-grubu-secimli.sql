-- "Ürün Grubu" üst ad özelliği (2026-09-06, kullanıcı kararı: gruplama katmanı YOK): urun_alt_grubu/text → urun_grubu/select,
-- değer havuzu = grup adları, grup varsayılanı = kendi adı, ürünlere geri dolum. İdempotent; seed aynı işi açılışta yapar.
-- ÖN KOŞUL: Catalog migration AddProductGroupAttributeDefaultValue uygulanmış olmalı.
DO $$
DECLARE tid uuid; n int;
BEGIN
  SELECT "Id" INTO tid FROM definition.attribute_types WHERE "Code" IN ('urun_grubu','urun_alt_grubu') ORDER BY ("Code"='urun_grubu') DESC LIMIT 1;
  IF tid IS NULL THEN
    tid := gen_random_uuid();
    INSERT INTO definition.attribute_types ("Id","Code","NameI18n","DataType","IsActive","SortOrder","UseInFilter","CreatedAt","IsDeleted")
    VALUES (tid,'urun_grubu','{"tr":"Ürün Grubu"}'::jsonb,'select',true,1200,false,now(),false);
  ELSE
    UPDATE definition.attribute_types SET "Code"='urun_grubu', "DataType"='select', "NameI18n"='{"tr":"Ürün Grubu"}'::jsonb,
           "IsActive"=true, "IsDeleted"=false, "DeletedAt"=NULL, "UpdatedAt"=now() WHERE "Id"=tid;
  END IF;

  UPDATE definition.product_group_attributes SET "IsDeleted"=false, "DeletedAt"=NULL, "UpdatedAt"=now() WHERE "AttributeTypeId"=tid AND "IsDeleted";
  INSERT INTO definition.product_group_attributes ("Id","ProductGroupId","AttributeTypeId","IsVariant","IsRequired","IsPrimaryAxis","SortOrder","CreatedAt","IsDeleted")
  SELECT gen_random_uuid(), g."Id", tid, false, false, false, 200, now(), false
    FROM definition.product_groups g WHERE NOT g."IsDeleted"
     AND NOT EXISTS (SELECT 1 FROM definition.product_group_attributes x WHERE x."ProductGroupId"=g."Id" AND x."AttributeTypeId"=tid);

  -- değer havuzu = grup adları (tr)
  INSERT INTO definition.attribute_values ("Id","AttributeTypeId","NameI18n","ExtraData","IsActive","SortOrder","CreatedAt","IsDeleted")
  SELECT gen_random_uuid(), tid, g."NameI18n", jsonb_build_object('productGroupCode', g."Code"), true,
         10 * row_number() OVER (ORDER BY g."NameI18n"->>'tr'), now(), false
    FROM definition.product_groups g
   WHERE NOT g."IsDeleted" AND coalesce(g."NameI18n"->>'tr','') <> ''
     AND NOT EXISTS (SELECT 1 FROM definition.attribute_values v WHERE v."AttributeTypeId"=tid AND v."NameI18n"->>'tr' = g."NameI18n"->>'tr');

  -- grup varsayılanı = kendi adı
  UPDATE definition.product_group_attributes pga
     SET "DefaultAttributeValueId" = v."Id", "UpdatedAt" = now()
    FROM definition.product_groups g, definition.attribute_values v
   WHERE pga."ProductGroupId" = g."Id" AND pga."AttributeTypeId" = tid AND pga."DefaultAttributeValueId" IS NULL
     AND v."AttributeTypeId" = tid AND NOT v."IsDeleted" AND v."NameI18n"->>'tr' = g."NameI18n"->>'tr';

  -- geri dolum
  INSERT INTO catalog.product_attributes ("Id","ProductId","AttributeTypeId","AttributeValueId","CreatedAt","IsDeleted")
  SELECT gen_random_uuid(), p."Id", tid, pga."DefaultAttributeValueId", now(), false
    FROM catalog.products p
    JOIN definition.product_group_attributes pga ON pga."ProductGroupId"=p."ProductGroupId" AND pga."AttributeTypeId"=tid AND NOT pga."IsDeleted"
   WHERE NOT p."IsDeleted" AND pga."DefaultAttributeValueId" IS NOT NULL
     AND NOT EXISTS (SELECT 1 FROM catalog.product_attributes pa WHERE pa."ProductId"=p."Id" AND pa."AttributeTypeId"=tid AND NOT pa."IsDeleted");
  GET DIAGNOSTICS n = ROW_COUNT;
  RAISE NOTICE 'geri dolum: % ürün', n;
END $$;
ANALYZE catalog.product_attributes;
SELECT (SELECT count(*) FROM definition.attribute_values v JOIN definition.attribute_types t ON t."Id"=v."AttributeTypeId" WHERE t."Code"='urun_grubu' AND NOT v."IsDeleted") AS deger,
       (SELECT count(*) FROM definition.product_group_attributes pga JOIN definition.attribute_types t ON t."Id"=pga."AttributeTypeId" WHERE t."Code"='urun_grubu' AND NOT pga."IsDeleted" AND pga."DefaultAttributeValueId" IS NOT NULL) AS varsayilanli_grup,
       (SELECT count(*) FROM catalog.product_attributes pa JOIN definition.attribute_types t ON t."Id"=pa."AttributeTypeId" WHERE t."Code"='urun_grubu' AND NOT pa."IsDeleted") AS urun;
