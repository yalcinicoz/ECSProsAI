SELECT
 (SELECT count(*) FROM catalog.product_attributes) AS pa_total,
 (SELECT count(*) FROM catalog.product_variant_attributes) AS pva_total,
 (SELECT count(*) FROM definition.product_group_attributes) AS pga_total,
 (SELECT count(*) FROM definition.attribute_values) AS av_total;
