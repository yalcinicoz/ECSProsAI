SELECT at."Code", count(pga."Id") AS group_count
FROM definition.product_group_attributes pga
JOIN definition.attribute_types at ON at."Id" = pga."AttributeTypeId"
WHERE pga."IsDeleted" = false
GROUP BY at."Code"
ORDER BY group_count DESC, at."Code";
