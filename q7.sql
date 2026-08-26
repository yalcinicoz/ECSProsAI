SELECT 'product_attributes' AS tbl, at."Code" AS attr, count(*) AS n
FROM catalog.product_attributes pa
JOIN definition.attribute_types at ON at."Id" = pa."AttributeTypeId"
WHERE pa."IsDeleted" = false
GROUP BY at."Code"
UNION ALL
SELECT 'variant_attributes', at."Code", count(*)
FROM catalog.product_variant_attributes pva
JOIN definition.attribute_types at ON at."Id" = pva."AttributeTypeId"
WHERE pva."IsDeleted" = false
GROUP BY at."Code"
ORDER BY tbl, attr;
