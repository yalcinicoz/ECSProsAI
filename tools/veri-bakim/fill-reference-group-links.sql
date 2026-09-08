-- Only missing urun_grubu links, using the read-only .59 snapshot.
-- No existing rows, product cards, mappings or source databases are changed.
BEGIN;
LOCK TABLE definition.product_groups, definition.attribute_types, definition.attribute_values IN SHARE MODE;
LOCK TABLE definition.product_group_attributes IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE original_links ON COMMIT DROP AS SELECT * FROM definition.product_group_attributes;
CREATE TEMP TABLE source_doc ON COMMIT DROP AS SELECT __SNAPSHOT__ d;
CREATE TEMP TABLE pending_links ON COMMIT DROP AS
WITH sg AS (SELECT x.* FROM source_doc,jsonb_populate_recordset(NULL::definition.product_groups,d->'groups') x),
st AS (SELECT x.* FROM source_doc,jsonb_populate_recordset(NULL::definition.attribute_types,d->'types') x),
sv AS (SELECT x.* FROM source_doc,jsonb_populate_recordset(NULL::definition.attribute_values,d->'values') x),
sl AS (SELECT x.* FROM source_doc,jsonb_populate_recordset(NULL::definition.product_group_attributes,d->'links') x)
SELECT g."Id" AS group_id,t."Id" AS type_id,s."IsVariant",s."IsRequired",s."IsPrimaryAxis",s."SortOrder",
 s."DefaultAttributeValueId" AS source_default,
 ARRAY(SELECT v."Id" FROM definition.attribute_values v JOIN sv ON sv."Id"=s."DefaultAttributeValueId"
 WHERE v."AttributeTypeId"=t."Id" AND v."IsActive" AND NOT v."IsDeleted"
 AND (v."Id"=sv."Id" OR v."NameI18n"->>'tr'=sv."NameI18n"->>'tr')) AS default_ids
FROM sl s JOIN sg ON sg."Id"=s."ProductGroupId" JOIN st ON st."Id"=s."AttributeTypeId"
JOIN definition.product_groups g ON g."Code"=sg."Code" AND g."IsActive" AND NOT g."IsDeleted"
JOIN definition.attribute_types t ON t."Code"=st."Code" AND t."DataType"=st."DataType" AND t."IsActive" AND NOT t."IsDeleted"
WHERE st."Code"='urun_grubu' AND NOT EXISTS(SELECT 1 FROM definition.product_group_attributes a
 WHERE a."ProductGroupId"=g."Id" AND a."AttributeTypeId"=t."Id");
DO $$ BEGIN
 IF current_database()<>'ecommerce_db' OR inet_server_addr()<>'192.168.0.241'::inet THEN RAISE EXCEPTION 'Unexpected target'; END IF;
 IF EXISTS(SELECT 1 FROM pending_links WHERE source_default IS NOT NULL AND cardinality(default_ids)<>1)
 THEN RAISE EXCEPTION 'Missing or ambiguous default mapping; nothing written'; END IF;
 IF EXISTS(SELECT 1 FROM pending_links GROUP BY group_id,type_id HAVING count(*)<>1)
 THEN RAISE EXCEPTION 'Ambiguous source links'; END IF;
END $$;
INSERT INTO definition.product_group_attributes
 ("Id","ProductGroupId","AttributeTypeId","IsVariant","IsRequired","IsPrimaryAxis","SortOrder","DefaultAttributeValueId","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),group_id,type_id,"IsVariant","IsRequired","IsPrimaryAxis","SortOrder",default_ids[1],now(),false FROM pending_links;
DO $$ BEGIN
 IF EXISTS(SELECT * FROM original_links EXCEPT SELECT * FROM definition.product_group_attributes)
 THEN RAISE EXCEPTION 'Existing link changed'; END IF;
END $$;
SELECT json_build_object('inserted_links',count(*),'with_reference_default',count(source_default)) FROM pending_links;
