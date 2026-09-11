namespace ECSPros.Catalog.Infrastructure.Migrations;

/// <summary>catalog.mv_product_stats tanımı — migration oluşturur, worker REFRESH ... CONCURRENTLY ile yeniler.</summary>
public static class ProductStatsSql
{
    public const string Refresh = "REFRESH MATERIALIZED VIEW CONCURRENTLY catalog.mv_product_stats;";

    public const string Create = """
CREATE MATERIALIZED VIEW IF NOT EXISTS catalog.mv_product_stats AS
WITH color_axis AS (
    SELECT ga."ProductGroupId", ga."AttributeTypeId"
    FROM definition.product_group_attributes ga
    WHERE ga."IsPrimaryAxis" AND NOT ga."IsDeleted"
),
vc AS (
    SELECT v."ProductId", v."Id" AS vid, va."AttributeValueId" AS color
    FROM catalog.product_variants v
    JOIN catalog.products p ON p."Id" = v."ProductId" AND NOT p."IsDeleted"
    LEFT JOIN color_axis ca ON ca."ProductGroupId" = p."ProductGroupId"
    LEFT JOIN catalog.product_variant_attributes va
           ON va."VariantId" = v."Id" AND va."AttributeTypeId" = ca."AttributeTypeId" AND NOT va."IsDeleted"
    WHERE NOT v."IsDeleted"
),
colors AS (
    SELECT vc."ProductId",
           COALESCE(vc.color, '00000000-0000-0000-0000-000000000000'::uuid) AS color,
           bool_or(EXISTS (SELECT 1 FROM catalog.product_images i WHERE i."VariantId" = vc.vid AND NOT i."IsDeleted")) AS has_img
    FROM vc GROUP BY 1, 2
),
img AS (
    SELECT i."ProductId", COUNT(*)::int AS image_count, MAX(i."CreatedAt") AS last_image_at
    FROM catalog.product_images i WHERE NOT i."IsDeleted" GROUP BY 1
),
stk AS (
    SELECT v."ProductId",
           COALESCE(SUM(s."Quantity"), 0)::int AS qty,
           COALESCE(SUM(s."Quantity" - s."ReservedQuantity"), 0)::int AS avail
    FROM inventory.inv_stocks s
    JOIN catalog.product_variants v ON v."Id" = s."VariantId" AND NOT v."IsDeleted"
    WHERE NOT s."IsDeleted" GROUP BY 1
),
col AS (
    SELECT "ProductId", COUNT(*)::int AS color_count, COUNT(*) FILTER (WHERE has_img)::int AS colors_with_image
    FROM colors GROUP BY 1
)
SELECT p."Id" AS "ProductId",
       CASE WHEN COALESCE(img.image_count, 0) = 0 THEN 'none'
            WHEN col.color_count IS NULL OR col.colors_with_image = col.color_count THEN 'full'
            WHEN col.colors_with_image = 0 THEN 'partial'
            ELSE 'partial' END AS "ImageState",
       COALESCE(col.color_count, 0) AS "ColorCount",
       COALESCE(col.colors_with_image, 0) AS "ColorsWithImage",
       COALESCE(img.image_count, 0) AS "ImageCount",
       img.last_image_at AS "LastImageAt",
       COALESCE(stk.qty, 0) AS "StockQuantity",
       COALESCE(stk.avail, 0) AS "StockAvailable",
       now() AS "RefreshedAt"
FROM catalog.products p
LEFT JOIN col ON col."ProductId" = p."Id"
LEFT JOIN img ON img."ProductId" = p."Id"
LEFT JOIN stk ON stk."ProductId" = p."Id"
WHERE NOT p."IsDeleted"
WITH DATA;
""";
}
