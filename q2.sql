SELECT p."Code", p."NameI18n", g."Code" AS grp
FROM catalog.products p
JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
WHERE g."Code" IN ('tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh')
ORDER BY g."Code", p."Code";
