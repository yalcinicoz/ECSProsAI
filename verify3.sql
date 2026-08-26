-- Renk ataması: varyant bazında örnek + hex
SELECT g."Code" AS grp, p."Code", av."NameI18n"->>'tr' AS renk, av."HexCode"
FROM catalog.product_variant_attributes pva
JOIN catalog.product_variants v ON v."Id" = pva."VariantId"
JOIN catalog.products p ON p."Id" = v."ProductId"
JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
JOIN definition.attribute_types t ON t."Id" = pva."AttributeTypeId"
JOIN definition.attribute_values av ON av."Id" = pva."AttributeValueId"
WHERE t."Code" = 'renk'
  AND g."Code" IN ('tlm_termos','tlm_mug')
ORDER BY av."SortOrder", p."Code"
LIMIT 40;
