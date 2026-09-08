-- .59 reference alignment; source is a read-only JSON snapshot.
-- Target .241 only. Caller must append ROLLBACK or COMMIT.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
SELECT pg_advisory_xact_lock(hashtextextended('erp-group-mapping:erp:nebim',8317));
LOCK TABLE definition.product_groups,definition.attribute_types,definition.attribute_values,
 definition.product_group_attributes,definition.product_group_axis_sub_attributes,
 integration.erp_reference_items,integration.marketplace_category_mappings IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE doc ON COMMIT DROP AS SELECT __SNAPSHOT__ d;
CREATE TEMP TABLE sg ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.product_groups,d->'groups') x;
CREATE TEMP TABLE st ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.attribute_types,d->'types') x;
CREATE TEMP TABLE sv ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.attribute_values,d->'values') x;
CREATE TEMP TABLE sl ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.product_group_attributes,d->'links') x JOIN sg ON sg."Id"=x."ProductGroupId";
CREATE TEMP TABLE sa ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.product_group_axis_sub_attributes,d->'axes') x JOIN sg ON sg."Id"=x."ProductGroupId";
CREATE TEMP TABLE sd ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::integration.erp_reference_items,d->'dictionary') x;
CREATE TEMP TABLE sm ON COMMIT DROP AS SELECT x.* FROM doc,jsonb_populate_recordset(NULL::integration.marketplace_category_mappings,d->'mappings') x;
DO $$ BEGIN
 IF current_database()<>'ecommerce_db' OR inet_server_addr()<>'192.168.0.241'::inet THEN RAISE EXCEPTION 'Wrong target'; END IF;
 IF (SELECT count(*) FROM sg)<300 OR (SELECT count(*) FROM sg)>500 OR (SELECT count(*) FROM sm)<>203 THEN RAISE EXCEPTION 'Source scope changed'; END IF;
 IF EXISTS(SELECT 1 FROM sm WHERE "MappingKind"<>'direct' OR "FirmPlatformId" IS NOT NULL OR "TargetExternalId" IS NULL) THEN RAISE EXCEPTION 'Unsupported source mapping'; END IF;
 IF EXISTS(SELECT 1 FROM sm GROUP BY "ProductGroupId" HAVING count(*)>1) OR EXISTS(SELECT 1 FROM sm GROUP BY "TargetExternalId" HAVING count(*)>1) THEN RAISE EXCEPTION 'Ambiguous source mapping'; END IF;
 IF EXISTS(SELECT 1 FROM st s JOIN definition.attribute_types t USING("Code") WHERE s."DataType"<>t."DataType") THEN RAISE EXCEPTION 'Attribute datatype change requires separate migration review'; END IF;
END $$;
CREATE TEMP TABLE changes(kind text,n bigint) ON COMMIT DROP;
CREATE TEMP TABLE before_groups ON COMMIT DROP AS SELECT * FROM definition.product_groups;
CREATE TEMP TABLE before_types ON COMMIT DROP AS SELECT * FROM definition.attribute_types;
CREATE TEMP TABLE before_values ON COMMIT DROP AS SELECT * FROM definition.attribute_values;
CREATE TEMP TABLE before_links ON COMMIT DROP AS SELECT * FROM definition.product_group_attributes;
CREATE TEMP TABLE before_axes ON COMMIT DROP AS SELECT * FROM definition.product_group_axis_sub_attributes;
CREATE TEMP TABLE before_dictionary ON COMMIT DROP AS SELECT * FROM integration.erp_reference_items;
CREATE TEMP TABLE before_mappings ON COMMIT DROP AS SELECT * FROM integration.marketplace_category_mappings;

-- Match stable codes; preserve existing target IDs.
INSERT INTO definition.product_groups ("Id","Code","NameI18n","IsActive","SortOrder","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),s."Code",s."NameI18n",s."IsActive",s."SortOrder",now(),false FROM sg s
WHERE NOT EXISTS(SELECT 1 FROM definition.product_groups t WHERE t."Code"=s."Code");
UPDATE definition.product_groups t SET "NameI18n"=s."NameI18n","IsActive"=true,"IsDeleted"=false,"DeletedAt"=NULL,"DeletedBy"=NULL,"SortOrder"=s."SortOrder","UpdatedAt"=now()
FROM sg s WHERE t."Code"=s."Code" AND (t."NameI18n",t."IsActive",t."IsDeleted",t."SortOrder") IS DISTINCT FROM (s."NameI18n",true,false,s."SortOrder");
CREATE TEMP TABLE gm ON COMMIT DROP AS SELECT s."Id" sid,t."Id" tid FROM sg s JOIN definition.product_groups t USING("Code");
INSERT INTO definition.attribute_types ("Id","Code","NameI18n","DataType","IsActive","SortOrder","UseInFilter","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),s."Code",s."NameI18n",s."DataType",true,s."SortOrder",s."UseInFilter",now(),false FROM st s
WHERE NOT EXISTS(SELECT 1 FROM definition.attribute_types t WHERE t."Code"=s."Code");
UPDATE definition.attribute_types t SET "NameI18n"=s."NameI18n","IsActive"=true,"IsDeleted"=false,"DeletedAt"=NULL,"DeletedBy"=NULL,"SortOrder"=s."SortOrder","UseInFilter"=s."UseInFilter","UpdatedAt"=now()
FROM st s WHERE t."Code"=s."Code" AND (t."NameI18n",t."IsActive",t."IsDeleted",t."SortOrder",t."UseInFilter") IS DISTINCT FROM (s."NameI18n",true,false,s."SortOrder",s."UseInFilter");
CREATE TEMP TABLE tm ON COMMIT DROP AS SELECT s."Id" sid,t."Id" tid FROM st s JOIN definition.attribute_types t USING("Code");

-- Source IDs are authoritative only when already in the same target type.
-- Otherwise exact Turkish label must identify a single existing target value.
CREATE TEMP TABLE vm ON COMMIT DROP AS
SELECT s."Id" sid,tm.tid typeid,
 CASE WHEN EXISTS(SELECT 1 FROM definition.attribute_values v WHERE v."Id"=s."Id" AND v."AttributeTypeId"=tm.tid)
 THEN ARRAY[s."Id"] ELSE ARRAY(SELECT v."Id" FROM definition.attribute_values v WHERE v."AttributeTypeId"=tm.tid AND NOT v."IsDeleted" AND v."NameI18n"->>'tr'=s."NameI18n"->>'tr') END candidates,
 NULL::uuid tid FROM sv s JOIN tm ON tm.sid=s."AttributeTypeId";
SELECT json_agg(x) AS ambiguous_values FROM (
 SELECT st."Code",sv."NameI18n",sv."SortOrder",sv."ExtraData",sv."HexCode",vm.candidates,
 (SELECT json_agg(json_build_object('id',v."Id",'name',v."NameI18n",'sort',v."SortOrder",'hex',v."HexCode",'extra',v."ExtraData")) FROM definition.attribute_values v WHERE v."Id"=ANY(vm.candidates)) targets
 FROM vm JOIN sv ON sv."Id"=vm.sid JOIN st ON st."Id"=sv."AttributeTypeId" WHERE cardinality(vm.candidates)>1 LIMIT 20) x;
-- Ambiguous labels stay untouched; never merge/repoint existing product values by name.
INSERT INTO changes SELECT 'ambiguous_values_preserved',count(*) FROM vm WHERE cardinality(candidates)>1;
UPDATE vm SET tid=CASE WHEN cardinality(candidates)=1 THEN candidates[1] WHEN cardinality(candidates)=0 THEN gen_random_uuid() ELSE NULL END;
-- Multiple source values with the same label cannot be collapsed into one target ID.
-- Retain an exact source/target ID match if present; leave the other rows unresolved.
WITH conflicting AS (SELECT tid FROM vm WHERE tid IS NOT NULL GROUP BY tid HAVING count(*)>1),
 skipped AS (UPDATE vm SET tid=NULL WHERE tid IN(SELECT tid FROM conflicting) AND sid<>tid RETURNING sid)
INSERT INTO changes SELECT 'colliding_source_values_preserved',count(*) FROM skipped;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM vm WHERE tid IS NOT NULL GROUP BY tid HAVING count(*)>1) THEN RAISE EXCEPTION 'Source values collapse to same target'; END IF;
END $$;
INSERT INTO definition.attribute_values ("Id","AttributeTypeId","NameI18n","ExtraData","IsActive","SortOrder","HexCode","CreatedAt","IsDeleted")
SELECT vm.tid,vm.typeid,s."NameI18n",s."ExtraData",true,s."SortOrder",s."HexCode",now(),false FROM sv s JOIN vm ON vm.sid=s."Id" WHERE cardinality(vm.candidates)=0;
UPDATE definition.attribute_values t SET "NameI18n"=s."NameI18n","ExtraData"=coalesce(s."ExtraData",t."ExtraData"),"IsActive"=true,"IsDeleted"=false,"DeletedAt"=NULL,"DeletedBy"=NULL,"SortOrder"=s."SortOrder","HexCode"=s."HexCode","UpdatedAt"=now()
FROM sv s JOIN vm ON vm.sid=s."Id" WHERE t."Id"=vm.tid
AND (t."NameI18n",t."ExtraData",t."IsActive",t."IsDeleted",t."SortOrder",t."HexCode") IS DISTINCT FROM (s."NameI18n",coalesce(s."ExtraData",t."ExtraData"),true,false,s."SortOrder",s."HexCode");

CREATE TEMP TABLE wanted_links ON COMMIT DROP AS
SELECT gm.tid gid,tm.tid typeid,s."IsVariant",s."IsRequired",s."IsPrimaryAxis",s."SortOrder",vm.tid defaultid
FROM sl s JOIN gm ON gm.sid=s."ProductGroupId" JOIN tm ON tm.sid=s."AttributeTypeId" LEFT JOIN vm ON vm.sid=s."DefaultAttributeValueId";
DO $$ BEGIN
 IF (SELECT count(*) FROM wanted_links)<>(SELECT count(*) FROM sl) OR EXISTS(SELECT 1 FROM sl WHERE "DefaultAttributeValueId" IS NOT NULL AND "DefaultAttributeValueId" NOT IN(SELECT sid FROM vm WHERE tid IS NOT NULL)) THEN RAISE EXCEPTION 'Incomplete template reference'; END IF;
END $$;
INSERT INTO definition.product_group_attributes ("Id","ProductGroupId","AttributeTypeId","IsVariant","IsRequired","IsPrimaryAxis","SortOrder","DefaultAttributeValueId","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),gid,typeid,"IsVariant","IsRequired","IsPrimaryAxis","SortOrder",defaultid,now(),false FROM wanted_links w
WHERE NOT EXISTS(SELECT 1 FROM definition.product_group_attributes t WHERE t."ProductGroupId"=w.gid AND t."AttributeTypeId"=w.typeid);
UPDATE definition.product_group_attributes t SET "IsVariant"=w."IsVariant","IsRequired"=w."IsRequired","IsPrimaryAxis"=w."IsPrimaryAxis","SortOrder"=w."SortOrder","DefaultAttributeValueId"=w.defaultid,"IsDeleted"=false,"DeletedAt"=NULL,"DeletedBy"=NULL,"UpdatedAt"=now()
FROM wanted_links w WHERE t."ProductGroupId"=w.gid AND t."AttributeTypeId"=w.typeid
AND (t."IsVariant",t."IsRequired",t."IsPrimaryAxis",t."SortOrder",t."DefaultAttributeValueId",t."IsDeleted") IS DISTINCT FROM (w."IsVariant",w."IsRequired",w."IsPrimaryAxis",w."SortOrder",w.defaultid,false);
UPDATE definition.product_group_attributes t SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
WHERE NOT t."IsDeleted" AND t."ProductGroupId" IN(SELECT tid FROM gm) AND NOT EXISTS(SELECT 1 FROM wanted_links w WHERE w.gid=t."ProductGroupId" AND w.typeid=t."AttributeTypeId");

CREATE TEMP TABLE wanted_axes ON COMMIT DROP AS
SELECT gm.tid gid,ta.tid axisid,ts.tid subid,s."IsRequired",s."SortOrder" FROM sa s JOIN gm ON gm.sid=s."ProductGroupId" JOIN tm ta ON ta.sid=s."AxisAttributeTypeId" JOIN tm ts ON ts.sid=s."SubAttributeTypeId";
INSERT INTO definition.product_group_axis_sub_attributes ("Id","ProductGroupId","AxisAttributeTypeId","SubAttributeTypeId","IsRequired","SortOrder","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),gid,axisid,subid,"IsRequired","SortOrder",now(),false FROM wanted_axes w
WHERE NOT EXISTS(SELECT 1 FROM definition.product_group_axis_sub_attributes t WHERE t."ProductGroupId"=w.gid AND t."AxisAttributeTypeId"=w.axisid AND t."SubAttributeTypeId"=w.subid);
UPDATE definition.product_group_axis_sub_attributes t SET "IsRequired"=w."IsRequired","SortOrder"=w."SortOrder","IsDeleted"=false,"DeletedAt"=NULL,"DeletedBy"=NULL,"UpdatedAt"=now()
FROM wanted_axes w WHERE t."ProductGroupId"=w.gid AND t."AxisAttributeTypeId"=w.axisid AND t."SubAttributeTypeId"=w.subid
AND (t."IsRequired",t."SortOrder",t."IsDeleted") IS DISTINCT FROM (w."IsRequired",w."SortOrder",false);
UPDATE definition.product_group_axis_sub_attributes t SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
WHERE NOT t."IsDeleted" AND t."ProductGroupId" IN(SELECT tid FROM gm) AND NOT EXISTS(SELECT 1 FROM wanted_axes w WHERE w.gid=t."ProductGroupId" AND w.axisid=t."AxisAttributeTypeId" AND w.subid=t."SubAttributeTypeId");

INSERT INTO integration.erp_reference_items ("Id","TargetSystem","Kind","Code","Name","ParentCode","IsActive","FirstSeenAt","LastSeenAt","Source","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),'erp:nebim','product_group',s."Code",s."Name",s."ParentCode",true,now(),now(),'manual',now(),false FROM sd s
WHERE NOT EXISTS(SELECT 1 FROM integration.erp_reference_items t WHERE t."TargetSystem"='erp:nebim' AND t."Kind"='product_group' AND t."Code"=s."Code" AND NOT t."IsDeleted");
UPDATE integration.erp_reference_items t SET "Name"=s."Name","ParentCode"=s."ParentCode","IsActive"=true,"LastSeenAt"=now(),"UpdatedAt"=now()
FROM sd s WHERE t."TargetSystem"='erp:nebim' AND t."Kind"='product_group' AND t."Code"=s."Code" AND NOT t."IsDeleted"
AND (t."Name",t."ParentCode",t."IsActive") IS DISTINCT FROM (s."Name",s."ParentCode",true);
CREATE TEMP TABLE wanted_mappings ON COMMIT DROP AS SELECT gm.tid gid,s.* FROM sm s JOIN gm ON gm.sid=s."ProductGroupId";
-- Preserve non-Nebim and platform-specific rules. Source scope is general Nebim group mappings.
UPDATE integration.marketplace_category_mappings t SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now()
WHERE t."Marketplace"='erp:nebim' AND t."FirmPlatformId" IS NULL AND NOT t."IsDeleted" AND NOT EXISTS(SELECT 1 FROM wanted_mappings w WHERE w.gid=t."ProductGroupId");
UPDATE integration.marketplace_category_mappings t SET "MappingKind"='direct',"TargetExternalId"=w."TargetExternalId","TargetName"=w."TargetName","TargetPath"=w."TargetPath","RulesJson"=NULL,"PoolJson"=NULL,"Status"='active',"StatusNote"=NULL,"UpdatedAt"=now()
FROM wanted_mappings w WHERE t."Marketplace"='erp:nebim' AND t."FirmPlatformId" IS NULL AND NOT t."IsDeleted" AND t."ProductGroupId"=w.gid
AND (t."MappingKind",t."TargetExternalId",t."TargetName",t."TargetPath",t."RulesJson",t."PoolJson",t."Status",t."StatusNote")
IS DISTINCT FROM ('direct',w."TargetExternalId",w."TargetName",w."TargetPath",NULL::jsonb,NULL::jsonb,'active',NULL::text);
INSERT INTO integration.marketplace_category_mappings ("Id","Marketplace","ProductGroupId","FirmPlatformId","MappingKind","TargetExternalId","TargetName","TargetPath","Status","CreatedAt","IsDeleted")
SELECT gen_random_uuid(),'erp:nebim',w.gid,NULL,'direct',w."TargetExternalId",w."TargetName",w."TargetPath",'active',now(),false FROM wanted_mappings w
WHERE NOT EXISTS(SELECT 1 FROM integration.marketplace_category_mappings t WHERE t."Marketplace"='erp:nebim' AND t."FirmPlatformId" IS NULL AND NOT t."IsDeleted" AND t."ProductGroupId"=w.gid);
DO $$ BEGIN
 IF (SELECT count(*) FROM wanted_axes)<>(SELECT count(*) FROM sa) OR (SELECT count(*) FROM wanted_mappings)<>(SELECT count(*) FROM sm) THEN RAISE EXCEPTION 'Incomplete mapping'; END IF;
 IF (SELECT count(*) FROM integration.marketplace_category_mappings WHERE "Marketplace"='erp:nebim' AND "FirmPlatformId" IS NULL AND NOT "IsDeleted")<>203 THEN RAISE EXCEPTION 'Mapping count mismatch'; END IF;
END $$;
INSERT INTO changes SELECT 'groups_inserted',count(*) FROM definition.product_groups t WHERE NOT EXISTS(SELECT 1 FROM before_groups b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'groups_updated',count(*) FROM definition.product_groups t JOIN before_groups b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
INSERT INTO changes SELECT 'types_inserted',count(*) FROM definition.attribute_types t WHERE NOT EXISTS(SELECT 1 FROM before_types b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'types_updated',count(*) FROM definition.attribute_types t JOIN before_types b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
INSERT INTO changes SELECT 'values_inserted',count(*) FROM definition.attribute_values t WHERE NOT EXISTS(SELECT 1 FROM before_values b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'values_updated',count(*) FROM definition.attribute_values t JOIN before_values b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
INSERT INTO changes SELECT 'links_inserted',count(*) FROM definition.product_group_attributes t WHERE NOT EXISTS(SELECT 1 FROM before_links b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'links_updated',count(*) FROM definition.product_group_attributes t JOIN before_links b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
INSERT INTO changes SELECT 'axes_inserted',count(*) FROM definition.product_group_axis_sub_attributes t WHERE NOT EXISTS(SELECT 1 FROM before_axes b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'axes_updated',count(*) FROM definition.product_group_axis_sub_attributes t JOIN before_axes b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
INSERT INTO changes SELECT 'dictionary_inserted',count(*) FROM integration.erp_reference_items t WHERE NOT EXISTS(SELECT 1 FROM before_dictionary b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'dictionary_updated',count(*) FROM integration.erp_reference_items t JOIN before_dictionary b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
INSERT INTO changes SELECT 'mappings_inserted',count(*) FROM integration.marketplace_category_mappings t WHERE NOT EXISTS(SELECT 1 FROM before_mappings b WHERE b."Id"=t."Id");
INSERT INTO changes SELECT 'mappings_updated',count(*) FROM integration.marketplace_category_mappings t JOIN before_mappings b USING("Id") WHERE to_jsonb(t) IS DISTINCT FROM to_jsonb(b);
DO $$ BEGIN
 IF EXISTS(SELECT gid,typeid,"IsVariant","IsRequired","IsPrimaryAxis","SortOrder",defaultid FROM wanted_links
 EXCEPT SELECT t."ProductGroupId",t."AttributeTypeId",t."IsVariant",t."IsRequired",t."IsPrimaryAxis",t."SortOrder",t."DefaultAttributeValueId" FROM definition.product_group_attributes t WHERE NOT t."IsDeleted") THEN RAISE EXCEPTION 'Template postcondition failed'; END IF;
 IF EXISTS(SELECT gid,axisid,subid,"IsRequired","SortOrder" FROM wanted_axes
 EXCEPT SELECT "ProductGroupId","AxisAttributeTypeId","SubAttributeTypeId","IsRequired","SortOrder" FROM definition.product_group_axis_sub_attributes WHERE NOT "IsDeleted") THEN RAISE EXCEPTION 'Axis postcondition failed'; END IF;
 IF EXISTS(SELECT gid,"TargetExternalId" FROM wanted_mappings
 EXCEPT SELECT "ProductGroupId","TargetExternalId" FROM integration.marketplace_category_mappings WHERE "Marketplace"='erp:nebim' AND "FirmPlatformId" IS NULL AND NOT "IsDeleted" AND "Status"='active' AND "MappingKind"='direct') THEN RAISE EXCEPTION 'ERP postcondition failed'; END IF;
 IF EXISTS(SELECT "Id" FROM before_groups EXCEPT SELECT "Id" FROM definition.product_groups)
 OR EXISTS(SELECT "Id" FROM before_values EXCEPT SELECT "Id" FROM definition.attribute_values)
 OR EXISTS(SELECT "Id" FROM before_types EXCEPT SELECT "Id" FROM definition.attribute_types) THEN RAISE EXCEPTION 'Existing identity lost'; END IF;
END $$;
SELECT json_object_agg(kind,n) FROM changes;
SELECT json_build_object('active_groups',(SELECT count(*) FROM definition.product_groups WHERE NOT "IsDeleted" AND "IsActive"),'mapped_erp_codes',(SELECT count(DISTINCT "TargetExternalId") FROM integration.marketplace_category_mappings WHERE "Marketplace"='erp:nebim' AND "FirmPlatformId" IS NULL AND NOT "IsDeleted" AND "Status"='active'));
