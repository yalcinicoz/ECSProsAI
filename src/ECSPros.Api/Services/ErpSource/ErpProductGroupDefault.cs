namespace ECSPros.Api.Services.ErpSource;

/// <summary>Fills only a missing group classification; never creates definitions or overwrites user data.</summary>
public static class ErpProductGroupDefault
{
    public const string InsertMissingSql = """
        WITH candidate AS (
            SELECT a."AttributeTypeId", a."DefaultAttributeValueId"
            FROM catalog.products p
            JOIN definition.product_groups g ON g."Id"=p."ProductGroupId" AND g."IsActive" AND NOT g."IsDeleted"
            JOIN definition.product_group_attributes a ON a."ProductGroupId"=g."Id" AND NOT a."IsDeleted"
            JOIN definition.attribute_types t ON t."Id"=a."AttributeTypeId"
                AND t."Code"='urun_grubu' AND t."IsActive" AND NOT t."IsDeleted"
            JOIN definition.attribute_values v ON v."Id"=a."DefaultAttributeValueId"
                AND v."AttributeTypeId"=t."Id" AND v."IsActive" AND NOT v."IsDeleted"
            WHERE p."Id"=@product AND NOT p."IsDeleted" AND g."Code"<>'gecici'
        )
        INSERT INTO catalog.product_attributes
            ("Id","ProductId","AttributeTypeId","AttributeValueId","CreatedAt","IsDeleted")
        SELECT gen_random_uuid(),@product,c."AttributeTypeId",c."DefaultAttributeValueId",now(),false
        FROM candidate c
        WHERE (SELECT count(*) FROM candidate)=1
          AND NOT EXISTS (
            SELECT 1 FROM catalog.product_attributes existing
            WHERE existing."ProductId"=@product AND existing."AttributeTypeId"=c."AttributeTypeId")
        ON CONFLICT ("ProductId","AttributeTypeId","AttributeValueId") DO NOTHING
        """;
}
