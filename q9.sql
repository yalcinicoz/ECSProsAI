SELECT g."Code" AS grp, p."Code", p."NameI18n"->>'tr' AS name
FROM catalog.products p
JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
WHERE g."Code" IN ('tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh','tlm_kamp_matarasi','tlm_spor_matara','tlm_shaker','tlm_sogutucu_buzluk','tlm_termal_canta','tlm_kamp_yemek_seti','tlm_biberon','tlm_dripper')
ORDER BY g."Code", p."Code";
