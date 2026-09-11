using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.AiReporting;

public sealed class ProductCardReportRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ProductCardMovementRow
{
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public string Type { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Start from catalog, not current stock: never exclude a card because it has no stock row.
public static class ProductCardReportSource
{
    public const string Id = "productCards";
    public const string Capability = "reports.ai.productCards.scope";
    public static bool CanAccess(EfektifYetkiler rights) => rights.Var(ReportDictionary.UsePermission)
        && rights.Var(Permissions.CatalogProductsView) && rights.Var(Permissions.InventoryView)
        && rights.Kanallar(ReportDictionary.UsePermission) is null
        && rights.Kanallar(Permissions.CatalogProductsView) is null && rights.Kanallar(Permissions.InventoryView) is null;

    public static ReportEntityDefinition<ProductCardReportRow> Dictionary { get; } = new ReportEntityDefinition<ProductCardReportRow>(Permissions.CatalogProductsView)
        .Text("cards.id", "Ürün kartı kimliği", p => p.Id.ToString())
        .Text("cards.code", "Ürün kodu", p => p.Code)
        .Text("cards.name", "Güncel ürün adı", p => p.Name)
        .Value("cards.createdAt", "Ürün kartı açılış tarihi", p => p.CreatedAt)
        .Count("cards.count", "Ürün kartı sayısı")
        .Seal();
    public static ReportEntityDefinition<ProductCardMovementRow> Movements { get; } = new ReportEntityDefinition<ProductCardMovementRow>(Permissions.InventoryView)
        .Value("cardMovements.createdAt", "Hareket kayıt tarihi", m => m.CreatedAt)
        .Text("cardMovements.type", "Hareket tipi (kayıtlı kod)", m => m.Type, groupable: false)
        .Value("cardMovements.quantity", "Tek hareketin kayıtlı adedi (net değişim değildir)", m => m.Quantity)
        .Value("cardMovements.variantId", "Hareketin varyant kimliği", m => m.VariantId)
        .Seal();
    private static readonly ReportRelationDefinition<ProductCardReportRow, ProductCardMovementRow, Guid> Relation =
        new("cards.movements", "Raporun gözlem aralığında kartın herhangi bir varyantının kayıtlı hareketleri", Movements, p => p.Id, m => m.ProductId);

    public static IReadOnlyList<ReportField> Fields(IReadOnlySet<string> permissions) => !permissions.Contains(Capability) ? []
        : Dictionary.Describe(permissions).Concat(Relation.Describe(permissions)).ToArray();

    public static IQueryable<ProductCardReportRow> Query(OrderDbContext db, DynamicReportPlan plan, EfektifYetkiler rights,
        OrderReportScope? resolvedPeriod = null, DateTimeOffset? observedAt = null)
    {
        if (plan.Source != Id) throw new ArgumentException("Ürün kartı kaynağı bekleniyor.");
        if (!CanAccess(rights)) throw new UnauthorizedAccessException();
        plan.ValidateCardWindow();
        var period = resolvedPeriod ?? plan.Scope();
        var from = period.From.UtcDateTime; var to = period.To.UtcDateTime;
        IQueryable<ProductCardReportRow> cards = db.Database.SqlQueryRaw<ProductCardReportRow>("""
            SELECT p."Id", p."Code", p."NameI18n"->>'tr' AS "Name", p."CreatedAt"
            FROM catalog.products p WHERE NOT p."IsDeleted"
            """);
        if (plan.CardWindowMonths is int months)
        {
            // Explicit mode: period selects card-registration cohort, NOT movement dates.
            // Calendar months use the business timezone; incomplete windows never qualify.
            var asOf = (observedAt ?? DateTimeOffset.UtcNow).UtcDateTime;
            cards = db.Database.SqlQuery<ProductCardReportRow>($"""
                SELECT p."Id", p."Code", p."NameI18n"->>'tr' AS "Name", p."CreatedAt"
                FROM catalog.products p
                WHERE NOT p."IsDeleted" AND p."CreatedAt" >= {from} AND p."CreatedAt" < {to}
                  AND ((p."CreatedAt" AT TIME ZONE 'Europe/Istanbul') + make_interval(months => {months}))
                        AT TIME ZONE 'Europe/Istanbul' <= {asOf}
                """);
            if (plan.Predicate is null) return cards;
            var initialMovements = db.Database.SqlQuery<ProductCardMovementRow>($"""
                SELECT v."ProductId", m."VariantId", m."MovementType" AS "Type", m."Quantity", m."CreatedAt"
                FROM inventory.inv_stock_movements m
                JOIN catalog.product_variants v ON v."Id"=m."VariantId"
                JOIN catalog.products p ON p."Id"=v."ProductId"
                WHERE NOT p."IsDeleted" AND p."CreatedAt" >= {from} AND p."CreatedAt" < {to}
                  AND m."CreatedAt" >= p."CreatedAt"
                  AND m."CreatedAt" < ((p."CreatedAt" AT TIME ZONE 'Europe/Istanbul') + make_interval(months => {months}))
                        AT TIME ZONE 'Europe/Istanbul'
                """);
            var scopedPermissions = ReportSourceCatalog.ResolvePermissions(rights);
            var initialSchema = Dictionary.Predicates(_ => true);
            Relation.Bind(initialSchema, initialMovements, _ => true, scopedPermissions);
            return initialSchema.Apply(cards, plan.Predicate, scopedPermissions);
        }
        if (plan.Predicate is null) return cards;
        // Historical links retained even for a currently inactive/deleted variant. Do not mistake
        // a removed variant for absence of activity. The parent card itself must remain nondeleted.
        var movements = db.Database.SqlQueryRaw<ProductCardMovementRow>("""
            SELECT v."ProductId", m."VariantId", m."MovementType" AS "Type", m."Quantity", m."CreatedAt"
            FROM inventory.inv_stock_movements m JOIN catalog.product_variants v ON v."Id"=m."VariantId"
            """);
        var permissions = ReportSourceCatalog.ResolvePermissions(rights);
        var schema = Dictionary.Predicates(_ => true);
        Relation.Bind(schema, movements, m => m.CreatedAt >= from && m.CreatedAt < to, permissions);
        return schema.Apply(cards, plan.Predicate, permissions);
    }
}
