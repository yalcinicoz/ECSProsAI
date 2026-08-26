-- Hacim ataması: ürün bazında örnek (termos + mug)
SELECT g."Code" AS grp, p."Code", av."NameI18n"->>'tr' AS hacim
FROM catalog.product_attributes pa
JOIN catalog.products p ON p."Id" = pa."ProductId"
JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
JOIN definition.attribute_types t ON t."Id" = pa."AttributeTypeId"
JOIN definition.attribute_values av ON av."Id" = pa."AttributeValueId"
WHERE t."Code" = 'hacim'
  AND g."Code" IN ('tlm_termos','tlm_mug','tlm_biberon')
ORDER BY g."Code", av."SortOrder", p."Code"
LIMIT 40;
