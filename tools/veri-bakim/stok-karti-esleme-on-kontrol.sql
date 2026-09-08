-- Salt-okunur: ürün kartı grup uygulamasından önce mevcut veri sözleşmesi.
SELECT table_schema,table_name,column_name,data_type
FROM information_schema.columns
WHERE (table_schema='integration' AND table_name IN ('erp_variant_data','erp_reference_items'))
   OR (table_schema='catalog' AND table_name='product_attributes')
ORDER BY table_schema,table_name,ordinal_position;
SELECT count(*) AS aktif_urun FROM catalog.products WHERE NOT "IsDeleted";
SELECT "Code", "NameI18n"->>'tr' AS ad FROM definition.attribute_types
WHERE NOT "IsDeleted" AND "Code" IN ('urun_grubu','ortam');
