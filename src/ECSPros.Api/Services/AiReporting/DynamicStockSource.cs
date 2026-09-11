using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.AiReporting;

public sealed class DynamicStockRow
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string StockType { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
    public string? ProductCode { get; set; }
    public string? Barcode { get; set; }
    public string? GroupName { get; set; }
    public DateTime? ProductCreatedAt { get; set; }
    public string?[] Labels { get; set; } = [];
    public string?[] SearchValues { get; set; } = [];
}

public static class DynamicStockSource
{
    public static ReportEntityDefinition<DynamicStockRow> Dictionary(IReadOnlyList<ReportField> attributes)
    {
        if (attributes.Count > 256 || attributes.Any(a => !ReportAttributeCatalog.TryId(a.Id, out _))
            || attributes.Select(a => a.Id).Distinct().Count() != attributes.Count) throw new ReportCatalogException("Özellik sözlüğü geçerli değil.");
        var d = new ReportEntityDefinition<DynamicStockRow>(Permissions.InventoryView)
            .Text("stockType", "Stok türü", r => r.StockType)
            .Value("variantId", "Varyant kimliği", r => r.VariantId)
            .Value("warehouseId", "Depo kimliği (konum ayrıntısında)", r => r.WarehouseId)
            .Text("productCode", "Ürün kodu", r => r.ProductCode).RequirePermission("productCode", Permissions.CatalogProductsView)
            .Text("barcode", "Barkod", r => r.Barcode).RequirePermission("barcode", Permissions.CatalogProductsView)
            .Text("productGroup", "Ürün grubu", r => r.GroupName).RequirePermission("productGroup", Permissions.CatalogProductsView)
            .Value("productCreatedAt", "Stok kartı açılış tarihi", r => r.ProductCreatedAt).RequirePermission("productCreatedAt", Permissions.CatalogProductsView)
            .Measure("stock.quantity", "Stok miktarı", r => r.Quantity, "sum", "stockType", true)
            .Measure("stock.reserved", "Rezerve miktar", r => r.Reserved, "sum", "stockType", true)
            .Measure("stock.available", "Kullanılabilir miktar", r => r.Available, "sum", "stockType", true);
        for (var i = 0; i < attributes.Count; i++)
        {
            var slot = i;
            d.SetText(attributes[i].Id, attributes[i].Label, r => r.Labels[slot], r => r.SearchValues[slot])
                .RequirePermission(attributes[i].Id, Permissions.CatalogProductsView);
        }
        return d.Seal();
    }

    public static IQueryable<DynamicStockRow> Query(OrderDbContext db, DynamicReportPlan plan,
        EfektifYetkiler effective, IReadOnlyList<ReportField> attributes)
    {
        var permissions = ReportSourceCatalog.ResolvePermissions(effective);
        if (!permissions.Contains(Permissions.InventoryView)) throw new UnauthorizedAccessException();
        if (plan.Source != "stock" || plan.StockGrain is not ("variant" or "location")
            || plan.From is not null || plan.To is not null || plan.Period is not null)
            throw new ArgumentException("Güncel stok kapsamı geçerli değil.");
        var used = new HashSet<string>((plan.Detail?.Columns ?? plan.Aggregate?.Dimensions ?? []).Concat(plan.Aggregate?.Measures ?? []));
        var budget = new ReportPredicateBudget();
        void Visit(ReportPredicate node, int depth)
        {
            budget.Visit(depth);
            if (node.Field is not null) used.Add(node.Field);
            foreach (var child in node.Children ?? []) Visit(child, depth + 1);
        }
        if (plan.Predicate is not null) Visit(plan.Predicate, 0);
        if (plan.StockGrain == "variant" && used.Contains("warehouseId"))
            throw new ArgumentException("Depo kolonu/koşulu için konum ayrıntısı seçilmeli.");
        var dictionary = Dictionary(attributes);
        var empty = Array.Empty<DynamicStockRow>().AsQueryable();
        if (plan.Predicate is not null) _ = dictionary.Predicates(_ => true).Apply(empty, plan.Predicate, permissions);
        if (plan.Detail is not null) _ = dictionary.Details(_ => true, r => r.Id).Build(empty, plan.Detail, new(), permissions);
        else _ = dictionary.Aggregates(_ => true).Build(empty, plan.Aggregate!, permissions);
        var chosen = attributes.Select((a, i) => (a, i)).Where(x => used.Contains(x.a.Id)).ToArray();
        if (chosen.Length > 16) throw new ArgumentException("Bir raporda en fazla16 özellik kullanılabilir.");
        var parameters = new List<object>();
        var joins = "";
        var labels = Enumerable.Repeat("NULL::text", attributes.Count).ToArray();
        var search = Enumerable.Repeat("NULL::text", attributes.Count).ToArray();
        foreach (var (a, i) in chosen)
        {
            ReportAttributeCatalog.TryId(a.Id, out var typeId);
            parameters.Add(new NpgsqlParameter("attr" + i, NpgsqlDbType.Uuid) { Value = typeId });
            joins += $$"""
                LEFT JOIN LATERAL (
                  SELECT array_to_string(array_agg(DISTINCT av."NameI18n"->>'tr' ORDER BY av."NameI18n"->>'tr'), ' / ') AS labels,
                    to_jsonb(array_agg(DISTINCT lower(translate(btrim(av."NameI18n"->>'tr'), 'Iİ', 'ıi'))))::text AS search
                  FROM (
                    SELECT va."AttributeValueId" AS value_id FROM catalog.product_variant_attributes va
                    WHERE va."VariantId"=v."Id" AND va."AttributeTypeId"=@attr{{i}} AND NOT va."IsDeleted"
                    UNION ALL
                    SELECT pa."AttributeValueId" FROM catalog.product_attributes pa
                    WHERE pa."ProductId"=p."Id" AND pa."AttributeTypeId"=@attr{{i}} AND NOT pa."IsDeleted"
                      AND NOT EXISTS (SELECT 1 FROM catalog.product_variant_attributes va
                        WHERE va."VariantId"=v."Id" AND va."AttributeTypeId"=@attr{{i}} AND NOT va."IsDeleted")
                  ) assignments
                  JOIN definition.attribute_values av ON av."Id"=assignments.value_id AND av."AttributeTypeId"=@attr{{i}}
                    AND NOT av."IsDeleted" AND av."IsActive"
                  JOIN definition.attribute_types at ON at."Id"=av."AttributeTypeId" AND NOT at."IsDeleted" AND at."IsActive"
                  WHERE NULLIF(btrim(av."NameI18n"->>'tr'),'') IS NOT NULL
                ) a{{i}} ON TRUE
                """;
            labels[i] = $"a{i}.labels"; search[i] = $"a{i}.search";
            joins += "\n";
        }
        var stock = plan.StockGrain == "variant" ? """
            SELECT md5("VariantId"::text || ':' || "StockType")::uuid AS "Id", "VariantId", "StockType",
                NULL::uuid AS "WarehouseId", SUM("Quantity"::numeric) AS "Quantity", SUM("ReservedQuantity"::numeric) AS "Reserved"
            FROM inventory.inv_stocks WHERE NOT "IsDeleted" GROUP BY "VariantId", "StockType"
            """ : """
            SELECT "Id", "VariantId", "StockType", "WarehouseId", "Quantity"::numeric AS "Quantity",
                "ReservedQuantity"::numeric AS "Reserved" FROM inventory.inv_stocks WHERE NOT "IsDeleted"
            """;
        var catalog = permissions.Contains(Permissions.CatalogProductsView);
        var productFields = catalog ? """p."Code" AS "ProductCode", v."Barcode", pg."NameI18n"->>'tr' AS "GroupName", p."CreatedAt" AS "ProductCreatedAt" """
            : """NULL::text AS "ProductCode", NULL::text AS "Barcode", NULL::text AS "GroupName", NULL::timestamptz AS "ProductCreatedAt" """;
        var catalogJoins = catalog ? """
            LEFT JOIN catalog.product_variants v ON v."Id"=s."VariantId" AND NOT v."IsDeleted"
            LEFT JOIN catalog.products p ON p."Id"=v."ProductId" AND NOT p."IsDeleted"
            LEFT JOIN definition.product_groups pg ON pg."Id"=p."ProductGroupId" AND NOT pg."IsDeleted"
            """ : "";
        var sql = $"""
            SELECT s.*, (s."Quantity"-s."Reserved") AS "Available", {productFields},
                ARRAY[{string.Join(",", labels)}]::text[] AS "Labels", ARRAY[{string.Join(",", search)}]::text[] AS "SearchValues"
            FROM ({stock}) s {catalogJoins} {joins}
            """;
        var rows = db.Database.SqlQueryRaw<DynamicStockRow>(sql, parameters.ToArray());
        return plan.Predicate is null ? rows : dictionary.Predicates(_ => true).Apply(rows, plan.Predicate, permissions);
    }
}
