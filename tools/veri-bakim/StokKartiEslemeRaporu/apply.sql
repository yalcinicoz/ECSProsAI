-- Explicit Apply/Rehearse only. One transaction, bounded locks; no definitions, prices or stock writes.
LOCK TABLE integration.marketplace_category_mappings, definition.product_groups,
 definition.product_group_attributes, definition.attribute_types, definition.attribute_values IN SHARE MODE;
LOCK TABLE catalog.products, catalog.product_attributes IN SHARE ROW EXCLUSIVE MODE;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM integration.marketplace_category_mappings WHERE "Marketplace"='erp:nebim'
 AND "Status"='active' AND NOT "IsDeleted" AND "FirmPlatformId" IS NULL AND "MappingKind"<>'direct')
 THEN RAISE EXCEPTION 'Non-direct mappings require review'; END IF;
 IF (SELECT count(*) FROM definition.attribute_types WHERE "Code"='urun_grubu' AND "IsActive" AND NOT "IsDeleted")<>1
 THEN RAISE EXCEPTION 'Classification type not unique'; END IF;
END $$;
CREATE TEMP TABLE group_apply_plan ON COMMIT DROP AS
WITH source AS (
 SELECT product,array_agg(DISTINCT erp) codes FROM jsonb_to_recordset(@source) s(product text,erp text) GROUP BY product
), mapping AS (
 SELECT m."TargetExternalId" erp,(array_agg(DISTINCT m."ProductGroupId"))[1] gid
 FROM integration.marketplace_category_mappings m
 JOIN definition.product_groups g ON g."Id"=m."ProductGroupId" AND g."IsActive" AND NOT g."IsDeleted" AND g."Code"<>'gecici'
 WHERE m."Marketplace"='erp:nebim' AND m."Status"='active' AND NOT m."IsDeleted"
 AND m."FirmPlatformId" IS NULL AND m."MappingKind"='direct'
 GROUP BY m."TargetExternalId" HAVING count(DISTINCT m."ProductGroupId")=1
), defaults AS (
 SELECT a."ProductGroupId" gid,(array_agg(a."AttributeTypeId"))[1] tid,(array_agg(a."DefaultAttributeValueId"))[1] vid
 FROM definition.product_group_attributes a
 JOIN definition.attribute_types t ON t."Id"=a."AttributeTypeId" AND t."Code"='urun_grubu' AND t."IsActive" AND NOT t."IsDeleted"
 JOIN definition.attribute_values v ON v."Id"=a."DefaultAttributeValueId" AND v."AttributeTypeId"=t."Id" AND v."IsActive" AND NOT v."IsDeleted"
 WHERE NOT a."IsDeleted" GROUP BY a."ProductGroupId" HAVING count(*)=1
)
SELECT p."Id" pid,p."ProductGroupId" old_gid,m.gid,d.tid,d.vid,
 NOT EXISTS(SELECT 1 FROM catalog.product_attributes a WHERE a."ProductId"=p."Id" AND a."AttributeTypeId"=d.tid) fill
FROM catalog.products p JOIN source s ON s.product=p."Code" AND cardinality(s.codes)=1
JOIN mapping m ON m.erp=s.codes[1] JOIN defaults d ON d.gid=m.gid
WHERE NOT p."IsDeleted"
AND NOT EXISTS(SELECT 1 FROM catalog.product_attributes a WHERE a."ProductId"=p."Id" AND a."AttributeTypeId"=d.tid
 AND (a."IsDeleted" OR a."AttributeValueId" IS DISTINCT FROM d.vid OR a."CustomValue" IS NOT NULL));
DO $$ BEGIN
 IF (SELECT count(*) FROM group_apply_plan WHERE old_gid<>gid)>13433 OR (SELECT count(*) FROM group_apply_plan WHERE fill)>29088
 THEN RAISE EXCEPTION 'Change count exceeds reviewed scope'; END IF;
END $$;
WITH changed AS (
 UPDATE catalog.products p SET "ProductGroupId"=x.gid,"UpdatedAt"=now()
 FROM group_apply_plan x WHERE p."Id"=x.pid AND p."ProductGroupId"<>x.gid RETURNING p."Id"
), filled AS (
 INSERT INTO catalog.product_attributes ("Id","ProductId","AttributeTypeId","AttributeValueId","CreatedAt","IsDeleted")
 SELECT gen_random_uuid(),pid,tid,vid,now(),false FROM group_apply_plan WHERE fill
 ON CONFLICT ("ProductId","AttributeTypeId","AttributeValueId") DO NOTHING RETURNING "Id"
)
SELECT jsonb_build_object('group_changed',(SELECT count(*) FROM changed),'classification_filled',(SELECT count(*) FROM filled))::text;
