SELECT DISTINCT jsonb_object_keys("Payload") AS alan
FROM (SELECT "Payload" FROM integration.erp_variant_data WHERE NOT "IsDeleted" LIMIT 100) d;
SELECT count(*) AS aktif_esleme FROM integration.marketplace_category_mappings
WHERE "Marketplace"='erp:nebim' AND "Status"='active' AND NOT "IsDeleted";
