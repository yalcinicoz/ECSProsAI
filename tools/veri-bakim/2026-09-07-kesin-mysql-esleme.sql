-- Yalnız .241/ecommerce_db; mevcut kayıtları güncellemez veya silmez.
-- Plan güvenilir yerel JSON ile verilir. COMMIT/ROLLBACK çağırandadır.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='20s';
DO $$ BEGIN
 IF current_database()<>'ecommerce_db' OR inet_server_addr()<>'192.168.0.241'::inet
 THEN RAISE EXCEPTION 'Target rejected'; END IF;
END $$;
SELECT pg_advisory_xact_lock(hashtextextended('erp-group-mapping:erp:nebim',8317));
LOCK integration.marketplace_category_mappings IN SHARE ROW EXCLUSIVE MODE;
LOCK integration.erp_reference_items,definition.product_groups IN SHARE MODE;
CREATE TEMP TABLE desired ON COMMIT DROP AS
SELECT g."Id" AS group_id,p."groupCode",p.items,
 CASE WHEN jsonb_array_length(p.items)=1 THEN 'direct' ELSE 'pool' END AS kind
FROM jsonb_to_recordset(__PLAN__) p("groupCode" text,items jsonb)
JOIN definition.product_groups g ON g."Code"=p."groupCode" AND g."IsActive" AND NOT g."IsDeleted";
CREATE TEMP TABLE source_codes ON COMMIT DROP AS
SELECT group_id,x->>'externalId' AS code,x->>'name' AS name
FROM desired CROSS JOIN LATERAL jsonb_array_elements(items) x;
DO $$ BEGIN
 IF (SELECT count(*) FROM desired)<>5 OR (SELECT count(*) FROM source_codes)<>17
 OR (SELECT count(DISTINCT code) FROM source_codes)<>17
 THEN RAISE EXCEPTION 'Unexpected plan count'; END IF;
 IF EXISTS(SELECT 1 FROM source_codes c WHERE NOT EXISTS(
  SELECT 1 FROM integration.erp_reference_items r WHERE r."TargetSystem"='erp:nebim'
   AND r."Kind"='product_group' AND r."Code"=c.code AND r."Name"=c.name
   AND r."IsActive" AND NOT r."IsDeleted"))
 THEN RAISE EXCEPTION 'Dictionary changed or inactive'; END IF;
 -- Önceki eşlemeyi ezme. Tekrar koşuda sadece aynı planın aktif kaydı kabul edilir.
 IF EXISTS(SELECT 1 FROM integration.marketplace_category_mappings m JOIN desired d
 ON d.group_id=m."ProductGroupId" WHERE m."Marketplace"='erp:nebim' AND m."FirmPlatformId" IS NULL
 AND (m."IsDeleted" OR m."Status"<>'active' OR m."MappingKind"<>d.kind OR
  CASE WHEN d.kind='direct' THEN m."TargetExternalId" IS DISTINCT FROM d.items->0->>'externalId'
  ELSE m."PoolJson" IS DISTINCT FROM d.items END))
 THEN RAISE EXCEPTION 'Existing group mapping differs; nothing overwritten'; END IF;
 IF EXISTS(SELECT 1 FROM integration.marketplace_category_mappings m
 CROSS JOIN LATERAL (
 SELECT btrim(m."TargetExternalId") AS code
 UNION SELECT btrim(x->>'targetExternalId') FROM jsonb_array_elements(COALESCE(NULLIF(m."RulesJson",'null'::jsonb),'[]')) x
 UNION SELECT btrim(x->>'externalId') FROM jsonb_array_elements(COALESCE(NULLIF(m."PoolJson",'null'::jsonb),'[]')) x
 ) refs JOIN source_codes c ON c.code=refs.code
 WHERE m."Marketplace"='erp:nebim' AND m."FirmPlatformId" IS NULL AND m."ProductGroupId"<>c.group_id)
 THEN RAISE EXCEPTION 'ERP code already referenced by another group'; END IF;
END $$;
CREATE TEMP TABLE originals ON COMMIT DROP AS SELECT * FROM integration.marketplace_category_mappings;
WITH added AS (
INSERT INTO integration.marketplace_category_mappings
("Id","Marketplace","ProductGroupId","MappingKind","TargetExternalId","TargetName","TargetPath","PoolJson","Status","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),'erp:nebim',d.group_id,d.kind,
 CASE WHEN d.kind='direct' THEN d.items->0->>'externalId' END,
 CASE WHEN d.kind='direct' THEN d.items->0->>'name' END,
 CASE WHEN d.kind='direct' THEN d.items->0->>'path' END,
 CASE WHEN d.kind='pool' THEN d.items END,'active',now(),false
FROM desired d WHERE NOT EXISTS(SELECT 1 FROM integration.marketplace_category_mappings m
 WHERE m."Marketplace"='erp:nebim' AND m."FirmPlatformId" IS NULL AND m."ProductGroupId"=d.group_id)
RETURNING "Id")
SELECT json_build_object('addedMappings',count(*),'coveredCodes',17) FROM added;
DO $$ BEGIN
 IF EXISTS(SELECT * FROM originals EXCEPT SELECT * FROM integration.marketplace_category_mappings)
 THEN RAISE EXCEPTION 'Existing records changed'; END IF;
END $$;
