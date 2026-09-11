using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Compiles only a validated stock recipe. User literals are always parameters.</summary>
public static class StockReportQuery
{
    public static NpgsqlCommand Create(string json, IReadOnlySet<string> permissions,
        IReadOnlyList<ReportField>? attributes = null, bool applyResultLimit = true)
    {
        var validation = ReportDefinitionValidator.Parse(json, permissions, attributes: attributes);
        if (!validation.IsValid) throw new ArgumentException(validation.Error, nameof(json));
        var recipe = validation.Definition!;
        var columns = recipe.Dimensions!.Concat(recipe.Metrics!).ToArray();
        var command = new NpgsqlCommand { CommandTimeout = 20 };
        var attributeIds = recipe.Dimensions!.Concat(recipe.Filters!.Select(f => f.Field!))
            .Where(id => ReportAttributeCatalog.TryId(id, out _)).Distinct().ToArray();
        var aliases = attributeIds.Select((id, i) => (id, alias: "a" + i)).ToDictionary(x => x.id, x => x.alias);
        string Expr(string id) => aliases.TryGetValue(id, out var alias)
            ? $"{alias}.labels" : Expression(id);
        var dimensions = recipe.Dimensions!.Select(Expr).ToArray();
        var select = columns.Select((id, index) => aliases.ContainsKey(id)
            ? $"array_to_string({Expr(id)}, ' / ') AS c{index}" : $"{Expr(id)} AS c{index}");
        var where = new List<string> { "NOT s.\"IsDeleted\"" };
        foreach (var filter in recipe.Filters!)
        {
            var name = "f" + command.Parameters.Count;
            var parameter = filter.Field == "warehouseId"
                ? new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                    { Value = filter.Values!.Select(Guid.Parse).ToArray() }
                : new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Text)
                    { Value = aliases.ContainsKey(filter.Field!)
                        ? filter.Values!.Select(v => v.Trim().ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))).ToArray()
                        : filter.Values! };
            command.Parameters.Add(parameter);
            where.Add(aliases.ContainsKey(filter.Field!)
                ? $"EXISTS (SELECT 1 FROM unnest({Expr(filter.Field!)}) label WHERE lower(translate(btrim(label), 'Iİ', 'ıi')) = ANY(@{name}))"
                : $"{Expr(filter.Field!)} = ANY(@{name})");
        }
        var needsProducts = attributeIds.Length > 0 || columns.Contains("productCode") || recipe.Filters.Any(f => f.Field == "productCode");
        var joins = needsProducts ? """
             LEFT JOIN catalog.product_variants v ON v."Id" = s."VariantId" AND NOT v."IsDeleted"
             LEFT JOIN catalog.products p ON p."Id" = v."ProductId" AND NOT p."IsDeleted"
            """ : "";
        foreach (var id in attributeIds)
        {
            ReportAttributeCatalog.TryId(id, out var typeId);
            var alias = aliases[id];
            var param = "type_" + alias;
            command.Parameters.AddWithValue(param, NpgsqlDbType.Uuid, typeId);
            // A lateral aggregate ALWAYS returns one row. A multi-value set never multiplies stock.
            // A variant's assignment overrides product assignment, including invalid/missing values.
            joins += $$"""
                 LEFT JOIN LATERAL (
                   SELECT array_agg(DISTINCT av."NameI18n"->>'tr' ORDER BY av."NameI18n"->>'tr')
                     FILTER (WHERE NULLIF(btrim(av."NameI18n"->>'tr'), '') IS NOT NULL) AS labels
                   FROM (
                     SELECT va."AttributeValueId" AS value_id
                     FROM catalog.product_variant_attributes va
                     WHERE va."VariantId" = v."Id" AND va."AttributeTypeId" = @{{param}} AND NOT va."IsDeleted"
                     UNION ALL
                     SELECT pa."AttributeValueId" FROM catalog.product_attributes pa
                     WHERE pa."ProductId" = p."Id" AND pa."AttributeTypeId" = @{{param}} AND NOT pa."IsDeleted"
                       AND NOT EXISTS (SELECT 1 FROM catalog.product_variant_attributes va
                         WHERE va."VariantId" = v."Id" AND va."AttributeTypeId" = @{{param}} AND NOT va."IsDeleted")
                   ) assignments
                   JOIN definition.attribute_values av ON av."Id" = assignments.value_id
                     AND av."AttributeTypeId" = @{{param}} AND NOT av."IsDeleted" AND av."IsActive"
                   JOIN definition.attribute_types at ON at."Id" = av."AttributeTypeId"
                     AND NOT at."IsDeleted" AND at."IsActive"
                 ) {{alias}} ON TRUE
                """;
        }
        // Joins are by unique PK, not reservations/payments/images: each stock row contributes once.
        command.CommandText = "SELECT " + string.Join(", ", select)
            + " FROM inventory.inv_stocks s " + joins
            + " WHERE " + string.Join(" AND ", where)
            + (dimensions.Length > 0 ? " GROUP BY " + string.Join(", ", dimensions)
                + " ORDER BY " + string.Join(", ", dimensions.Select(x => x + " NULLS LAST")) : "")
            + (applyResultLimit ? " LIMIT @resultLimit" : "");
        if (applyResultLimit) command.Parameters.AddWithValue("resultLimit", recipe.Limit + 1);
        return command;
    }

    public static string Expression(string id) => id switch
    {
        "stock.quantity" => "COALESCE(SUM(s.\"Quantity\"::bigint), 0)",
        "stock.reserved" => "COALESCE(SUM(s.\"ReservedQuantity\"::bigint), 0)",
        "stock.available" => "COALESCE(SUM(s.\"Quantity\"::bigint - s.\"ReservedQuantity\"::bigint), 0)",
        "productCode" => "p.\"Code\"",
        "warehouseId" => "s.\"WarehouseId\"",
        "stockType" => "s.\"StockType\"",
        _ => throw new ArgumentException("Desteklenmeyen rapor alanı.", nameof(id))
    };
}
