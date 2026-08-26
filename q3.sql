SELECT g."Code" AS grp, at."Code" AS attr, pga."IsVariant", pga."IsRequired", pga."IsPrimaryAxis", pga."SortOrder"
FROM definition.product_group_attributes pga
JOIN definition.product_groups g ON g."Id" = pga."ProductGroupId"
JOIN definition.attribute_types at ON at."Id" = pga."AttributeTypeId"
WHERE g."Code" IN ('tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh')
  AND pga."IsDeleted" = false
ORDER BY g."Code", pga."SortOrder";
