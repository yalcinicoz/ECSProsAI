SELECT at."Code" AS attr, count(pa."Id") AS used,
       count(pa."Id") FILTER (WHERE pa."AttributeValueId" IS NOT NULL) AS with_value,
       count(pa."Id") FILTER (WHERE pa."CustomValue" IS NOT NULL) AS with_custom
FROM catalog.product_attributes pa
JOIN definition.attribute_types at ON at."Id" = pa."AttributeTypeId"
WHERE at."Code" IN ('hacim','renk') AND pa."IsDeleted" = false
GROUP BY at."Code";
