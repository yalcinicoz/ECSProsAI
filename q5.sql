SELECT av."Id", at."Code" AS attr, av."NameI18n", av."HexCode", av."SortOrder"
FROM definition.attribute_values av
JOIN definition.attribute_types at ON at."Id" = av."AttributeTypeId"
WHERE at."Code" IN ('hacim','renk') AND av."IsDeleted" = false
ORDER BY at."Code", av."SortOrder", av."NameI18n"::text;
