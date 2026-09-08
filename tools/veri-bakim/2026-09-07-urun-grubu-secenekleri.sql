-- Only attribute type + options, based on the upstream SeedUrunGrubuAsync convention.
-- No group creation, product backfill, defaults, ERP mappings or service changes.
BEGIN;
SET LOCAL lock_timeout='3s';
SET LOCAL statement_timeout='30s';
LOCK TABLE definition.attribute_types,definition.attribute_values IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE definition.product_groups IN SHARE MODE;
CREATE TEMP TABLE old_types ON COMMIT DROP AS SELECT * FROM definition.attribute_types;
CREATE TEMP TABLE old_values ON COMMIT DROP AS SELECT * FROM definition.attribute_values;
DO $$
DECLARE tid uuid; n int;
BEGIN
 IF current_database()<>'ecommerce_db' OR inet_server_addr()<>'192.168.0.241'::inet OR pg_is_in_recovery()
 THEN RAISE EXCEPTION 'Unexpected target'; END IF;
 IF EXISTS(SELECT 1 FROM definition.attribute_types WHERE "Code"='urun_alt_grubu')
 THEN RAISE EXCEPTION 'Legacy type requires separate review'; END IF;
 SELECT "Id" INTO tid FROM definition.attribute_types WHERE "Code"='urun_grubu';
 IF tid IS NULL THEN
  INSERT INTO definition.attribute_types ("Id","Code","NameI18n","DataType","IsActive","SortOrder","UseInFilter","CreatedAt","IsDeleted")
  VALUES(gen_random_uuid(),'urun_grubu','{"tr":"Ürün Grubu"}','select',true,1200,false,now(),false) RETURNING "Id" INTO tid;
  RAISE NOTICE 'Added attribute type: 1';
 ELSIF NOT EXISTS(SELECT 1 FROM definition.attribute_types WHERE "Id"=tid AND "DataType"='select' AND "IsActive" AND NOT "IsDeleted")
 THEN RAISE EXCEPTION 'Existing type inactive/incompatible; preserved'; END IF;
 IF EXISTS(SELECT 1 FROM definition.product_groups WHERE "IsActive" AND NOT "IsDeleted"
  GROUP BY btrim("NameI18n"->>'tr') HAVING count(*)>1)
 THEN RAISE EXCEPTION 'Ambiguous group names'; END IF;
 IF EXISTS(SELECT 1 FROM definition.product_groups WHERE "IsActive" AND NOT "IsDeleted" AND coalesce(btrim("NameI18n"->>'tr'),'')='')
 THEN RAISE EXCEPTION 'Group name missing'; END IF;
 IF EXISTS(SELECT 1 FROM definition.attribute_values WHERE "AttributeTypeId"=tid
  GROUP BY btrim("NameI18n"->>'tr') HAVING count(*)>1)
 THEN RAISE EXCEPTION 'Duplicate existing option names'; END IF;
 INSERT INTO definition.attribute_values ("Id","AttributeTypeId","NameI18n","ExtraData","IsActive","SortOrder","CreatedAt","IsDeleted")
 SELECT gen_random_uuid(),tid,g."NameI18n"||jsonb_build_object('tr',btrim(g."NameI18n"->>'tr')),
  jsonb_build_object('productGroupCode',g."Code"),true,(10*row_number() OVER (ORDER BY g."Code"))::integer,now(),false
 FROM definition.product_groups g WHERE g."IsActive" AND NOT g."IsDeleted"
 AND NOT EXISTS(SELECT 1 FROM definition.attribute_values v WHERE v."AttributeTypeId"=tid AND btrim(v."NameI18n"->>'tr')=btrim(g."NameI18n"->>'tr'));
 GET DIAGNOSTICS n=ROW_COUNT; RAISE NOTICE 'Added options: %',n;
 IF EXISTS(SELECT * FROM old_types EXCEPT SELECT * FROM definition.attribute_types)
 OR EXISTS(SELECT * FROM old_values EXCEPT SELECT * FROM definition.attribute_values)
 THEN RAISE EXCEPTION 'Existing definitions changed'; END IF;
END $$;
-- REPORT
SELECT json_build_object('database',current_database(),'host',inet_server_addr(),'readonly',current_setting('transaction_read_only'),
 'local_active_groups',(SELECT count(*) FROM definition.product_groups WHERE "IsActive" AND NOT "IsDeleted"),
 'type',(SELECT json_build_object('code',"Code",'dataType',"DataType",'active',"IsActive") FROM definition.attribute_types WHERE "Code"='urun_grubu' AND NOT "IsDeleted"),
 'active_options',(SELECT count(*) FROM definition.attribute_values v JOIN definition.attribute_types t ON t."Id"=v."AttributeTypeId" WHERE t."Code"='urun_grubu' AND v."IsActive" AND NOT v."IsDeleted"),
 'product_values',(SELECT count(*) FROM catalog.product_attributes a JOIN definition.attribute_types t ON t."Id"=a."AttributeTypeId" WHERE t."Code"='urun_grubu' AND NOT a."IsDeleted"),
 'group_links',(SELECT count(*) FROM definition.product_group_attributes a JOIN definition.attribute_types t ON t."Id"=a."AttributeTypeId" WHERE t."Code"='urun_grubu' AND NOT a."IsDeleted"));
