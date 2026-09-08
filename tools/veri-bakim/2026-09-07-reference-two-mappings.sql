-- Source .59 snapshot is read-only. Only these two approved missing mappings.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='30s';
SELECT pg_advisory_xact_lock(hashtextextended('erp-group-mapping:erp:nebim',8317));
LOCK TABLE integration.marketplace_category_mappings IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE definition.product_groups, integration.erp_reference_items IN SHARE MODE;
CREATE TEMP TABLE reference_doc ON COMMIT DROP AS SELECT __SNAPSHOT__ d;
CREATE TEMP TABLE expected_pairs(code,erp) ON COMMIT DROP AS VALUES ('tlm_fondoten','279'),('grp_9','60');
CREATE TEMP TABLE pending_reference_mapping ON COMMIT DROP AS
SELECT g."Id" gid,e.erp,s."TargetName",s."TargetPath"
FROM expected_pairs e
JOIN definition.product_groups g ON g."Code"=e.code AND g."IsActive" AND NOT g."IsDeleted"
JOIN integration.erp_reference_items d ON d."TargetSystem"='erp:nebim' AND d."Kind"='product_group' AND d."Code"=e.erp AND d."IsActive" AND NOT d."IsDeleted"
JOIN (SELECT x.* FROM reference_doc,jsonb_populate_recordset(NULL::integration.marketplace_category_mappings,d->'mappings') x) s
ON s."TargetExternalId"=e.erp AND s."MappingKind"='direct' AND s."FirmPlatformId" IS NULL
JOIN (SELECT x.* FROM reference_doc,jsonb_populate_recordset(NULL::definition.product_groups,d->'groups') x) sg
ON sg."Id"=s."ProductGroupId" AND sg."Code"=e.code;
DO $$ BEGIN
 IF current_database()<>'ecommerce_db' OR inet_server_addr()<>'192.168.0.241'::inet THEN RAISE EXCEPTION 'Wrong target'; END IF;
 IF (SELECT count(*) FROM pending_reference_mapping)<>2 OR (SELECT count(DISTINCT gid) FROM pending_reference_mapping)<>2 THEN RAISE EXCEPTION 'Source/target drift'; END IF;
 IF EXISTS(SELECT 1 FROM integration.marketplace_category_mappings m JOIN pending_reference_mapping p
 ON (m."ProductGroupId"=p.gid OR m."TargetExternalId"=p.erp OR coalesce(m."RulesJson"::text,'') LIKE '%"'||p.erp||'"%' OR coalesce(m."PoolJson"::text,'') LIKE '%"'||p.erp||'"%')
 WHERE m."Marketplace"='erp:nebim' AND NOT m."IsDeleted")
 THEN RAISE EXCEPTION 'Existing mapping preserved; stop'; END IF;
END $$;
WITH inserted AS (
INSERT INTO integration.marketplace_category_mappings
("Id","Marketplace","ProductGroupId","FirmPlatformId","MappingKind","TargetExternalId","TargetName","TargetPath","Status","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),'erp:nebim',gid,NULL,'direct',erp,"TargetName","TargetPath",'active',now(),false FROM pending_reference_mapping
RETURNING "Id","ProductGroupId","TargetExternalId")
SELECT json_build_object('inserted_mappings',json_agg(inserted)) FROM inserted;
-- Caller appends ROLLBACK for rehearsal, COMMIT for explicit Apply.
