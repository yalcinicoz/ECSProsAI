SELECT p."Code", p."NameI18n"->>'tr' AS name
FROM catalog.products p
JOIN definition.product_groups g ON g."Id" = p."ProductGroupId"
WHERE g."Code" IN ('tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh','tlm_kamp_matarasi','tlm_spor_matara','tlm_shaker','tlm_sogutucu_buzluk','tlm_termal_canta','tlm_kamp_yemek_seti','tlm_biberon','tlm_dripper')
AND (p."NameI18n"->>'tr') ~* '\y35\s*L\y|\y47\s*L\y|\y75\s*L\y|\y35L\b|\y47L\b|\y75L\b'
ORDER BY p."Code";
