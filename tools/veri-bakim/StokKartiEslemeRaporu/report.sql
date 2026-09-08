WITH source AS (
 SELECT product,array_agg(DISTINCT erp) codes FROM jsonb_to_recordset(@source) s(product text,erp text) GROUP BY product
), mapping AS (
 SELECT m."TargetExternalId" erp,array_agg(DISTINCT m."ProductGroupId") targets
 FROM integration.marketplace_category_mappings m
 JOIN definition.product_groups g ON g."Id"=m."ProductGroupId" AND g."IsActive" AND NOT g."IsDeleted"
 WHERE m."Marketplace"='erp:nebim' AND m."Status"='active' AND NOT m."IsDeleted"
 AND m."FirmPlatformId" IS NULL AND m."MappingKind"='direct' GROUP BY m."TargetExternalId"
), types AS (
 SELECT "Id" FROM definition.attribute_types WHERE "Code"='urun_grubu' AND "IsActive" AND NOT "IsDeleted"
), defaults AS (
 SELECT a."ProductGroupId" gid,array_agg(DISTINCT a."DefaultAttributeValueId") vals
 FROM definition.product_group_attributes a JOIN types t ON t."Id"=a."AttributeTypeId"
 JOIN definition.attribute_values v ON v."Id"=a."DefaultAttributeValueId" AND v."AttributeTypeId"=t."Id" AND v."IsActive" AND NOT v."IsDeleted"
 WHERE NOT a."IsDeleted" GROUP BY a."ProductGroupId"
), existing AS (
 SELECT a."ProductId" pid,count(*) n,array_agg(a."AttributeValueId") vals
 FROM catalog.product_attributes a JOIN types t ON t."Id"=a."AttributeTypeId" WHERE NOT a."IsDeleted" GROUP BY a."ProductId"
), classified AS (
 SELECT p."Code" product,old."Code" old_group,g."Code" new_group,s.codes[1] erp,
 CASE WHEN s.product IS NULL THEN 'v3_grup_bagi_yok'
 WHEN cardinality(s.codes)<>1 THEN 'v3_birden_fazla_grup'
 WHEN cardinality(m.targets) IS DISTINCT FROM 1 THEN 'tekil_dogrudan_esleme_yok'
 WHEN p."ProductGroupId"=g."Id" THEN 'grup_ayni' ELSE 'grup_degisecek' END group_status,
 CASE WHEN cardinality(m.targets) IS DISTINCT FROM 1 THEN 'esleme_bekliyor'
 WHEN (SELECT count(*) FROM types)<>1 OR cardinality(d.vals) IS DISTINCT FROM 1 THEN 'varsayilan_eksik_belirsiz'
 WHEN e.pid IS NULL THEN 'bos_ozellik_doldurulabilir'
 WHEN e.n=1 AND e.vals[1]=d.vals[1] THEN 'ozellik_ayni'
 ELSE 'mevcut_deger_korunacak_inceleme' END attribute_status
 FROM catalog.products p LEFT JOIN source s ON s.product=p."Code"
 LEFT JOIN mapping m ON cardinality(s.codes)=1 AND m.erp=s.codes[1]
 LEFT JOIN definition.product_groups old ON old."Id"=p."ProductGroupId"
 LEFT JOIN definition.product_groups g ON cardinality(m.targets)=1 AND g."Id"=m.targets[1]
 LEFT JOIN defaults d ON d.gid=g."Id" LEFT JOIN existing e ON e.pid=p."Id"
 WHERE NOT p."IsDeleted"
), counts AS (
 SELECT group_status,attribute_status,count(*) count FROM classified GROUP BY group_status,attribute_status
), examples AS (
 SELECT *,row_number() OVER(PARTITION BY group_status,attribute_status ORDER BY product) n FROM classified
)
SELECT jsonb_build_object('counts',(SELECT jsonb_agg(to_jsonb(c)) FROM counts c),
 'examples',(SELECT jsonb_agg(to_jsonb(e)-'n') FROM examples e WHERE n<=5),
 'unmatched_codes',(SELECT jsonb_agg(to_jsonb(u)) FROM (
 SELECT c.erp,count(*) count,(SELECT jsonb_agg(jsonb_build_object('code',m.erp,'targets',m.targets)) FROM mapping m WHERE lower(m.erp)=lower(c.erp)) case_candidates
 FROM classified c WHERE group_status='tekil_dogrudan_esleme_yok' GROUP BY c.erp ORDER BY count(*) DESC) u))::text;
