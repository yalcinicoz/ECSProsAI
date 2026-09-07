WITH doc AS (SELECT __SNAPSHOT__ d),
sg AS (SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.product_groups,d->'groups') x),
st AS (SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.attribute_types,d->'types') x),
sv AS (SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.attribute_values,d->'values') x),
sl AS (SELECT x.* FROM doc,jsonb_populate_recordset(NULL::definition.product_group_attributes,d->'links') x)
SELECT json_build_object(
 'source_group_count',(SELECT count(*) FROM sg),'target_group_count',(SELECT count(*) FROM definition.product_groups WHERE NOT "IsDeleted" AND "IsActive"),
 'source_type_count',(SELECT count(*) FROM st),'target_type_count',(SELECT count(*) FROM definition.attribute_types WHERE NOT "IsDeleted" AND "IsActive"),
 'missing_groups',(SELECT json_agg(json_build_object('code',s."Code",'name',s."NameI18n"->>'tr')) FROM sg s WHERE NOT EXISTS(SELECT 1 FROM definition.product_groups t WHERE t."Code"=s."Code")),
 'missing_types',(SELECT json_agg(json_build_object('code',s."Code",'name',s."NameI18n"->>'tr','dataType',s."DataType")) FROM st s WHERE NOT EXISTS(SELECT 1 FROM definition.attribute_types t WHERE t."Code"=s."Code")),
 'type_conflicts',(SELECT json_agg(json_build_object('code',s."Code",'source',s."DataType",'target',t."DataType",'deleted',t."IsDeleted",'active',t."IsActive")) FROM st s JOIN definition.attribute_types t USING("Code") WHERE s."DataType"<>t."DataType" OR NOT t."IsActive" OR t."IsDeleted"),
 'missing_values_by_type',(SELECT json_agg(x) FROM (SELECT st."Code",count(*) missing FROM sv s JOIN st ON st."Id"=s."AttributeTypeId"
 LEFT JOIN definition.attribute_types t ON t."Code"=st."Code"
 WHERE NOT EXISTS(SELECT 1 FROM definition.attribute_values v WHERE v."AttributeTypeId"=t."Id" AND (v."Id"=s."Id" OR v."NameI18n"->>'tr'=s."NameI18n"->>'tr'))
 GROUP BY st."Code" ORDER BY st."Code") x),
 'missing_links',(SELECT count(*) FROM sl s JOIN sg ON sg."Id"=s."ProductGroupId" JOIN st ON st."Id"=s."AttributeTypeId"
 JOIN definition.product_groups g ON g."Code"=sg."Code" JOIN definition.attribute_types t ON t."Code"=st."Code"
 WHERE NOT EXISTS(SELECT 1 FROM definition.product_group_attributes a WHERE a."ProductGroupId"=g."Id" AND a."AttributeTypeId"=t."Id")),
 'missing_link_types',(SELECT json_agg(x) FROM (SELECT st."Code",count(*) missing FROM sl s JOIN sg ON sg."Id"=s."ProductGroupId" JOIN st ON st."Id"=s."AttributeTypeId"
 JOIN definition.product_groups g ON g."Code"=sg."Code" JOIN definition.attribute_types t ON t."Code"=st."Code"
 WHERE NOT EXISTS(SELECT 1 FROM definition.product_group_attributes a WHERE a."ProductGroupId"=g."Id" AND a."AttributeTypeId"=t."Id") GROUP BY st."Code") x));
