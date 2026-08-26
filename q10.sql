SELECT g."Code" AS grp, count(p."Id") AS products, count(v."Id") AS variants
FROM catalog.products p
JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
LEFT JOIN catalog.product_variants v ON v."ProductId" = p."Id"
WHERE g."Code" IN ('tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh','tlm_kamp_matarasi','tlm_spor_matara','tlm_shaker','tlm_sogutucu_buzluk','tlm_termal_canta','tlm_kamp_yemek_seti','tlm_biberon','tlm_dripper')
GROUP BY g."Code" ORDER BY g."Code";
