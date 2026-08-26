-- Grup özellik atamaları (içecek grupları)
SELECT g."Code" AS grp, at."Code" AS attr, pga."IsVariant", pga."IsPrimaryAxis"
FROM definition.product_group_attributes pga
JOIN definition.product_groups g ON g."Id" = pga."ProductGroupId"
JOIN definition.attribute_types at ON at."Id" = pga."AttributeTypeId"
WHERE g."Code" IN ('tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh','tlm_kamp_matarasi','tlm_spor_matara','tlm_shaker','tlm_sogutucu_buzluk','tlm_termal_canta','tlm_kamp_yemek_seti','tlm_biberon','tlm_dripper')
  AND at."Code" IN ('hacim','renk')
ORDER BY g."Code", at."Code";
