SELECT "Code", "NameI18n" FROM definition.product_groups
WHERE "NameI18n"::text ILIKE '%termos%'
   OR "NameI18n"::text ILIKE '%mug%'
   OR "NameI18n"::text ILIKE '%kupa%'
   OR "NameI18n"::text ILIKE '%bardak%'
   OR "NameI18n"::text ILIKE '%kadeh%'
   OR "NameI18n"::text ILIKE '%sürahi%'
ORDER BY "Code";
