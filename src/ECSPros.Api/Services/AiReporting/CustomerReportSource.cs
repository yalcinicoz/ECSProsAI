using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.AiReporting;

// Unmapped read model: no migration, customer data is never sent to the planner.
public sealed class CustomerReportRow
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Active { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public decimal OrderCount { get; set; }
    public decimal ReturnCount { get; set; }
}

public static class CustomerReportSource
{
    public const string Id = "customers";
    // Derived server capability, NOT a new IAM permission or a client-supplied grant.
    public const string GlobalScopeCapability = "reports.ai.customers.globalScope";
    public static bool CanAccess(EfektifYetkiler effective) => effective.Var(ReportDictionary.UsePermission)
        && effective.Var(Permissions.CrmMembersView) && effective.Kanallar(ReportDictionary.UsePermission) is null
        && effective.Kanallar(Permissions.CrmMembersView) is null;

    public static ReportEntityDefinition<CustomerReportRow> Dictionary { get; } = new ReportEntityDefinition<CustomerReportRow>(Permissions.CrmMembersView)
        .Text("customers.id", "Müşteri kartı kimliği", r => r.Id.ToString())
        .Text("customers.firstName", "Müşteri adı", r => r.FirstName)
        .Text("customers.lastName", "Müşteri soyadı", r => r.LastName)
        .Text("customers.active", "Güncel kart durumu (Aktif/Pasif)", r => r.Active)
        .Value("customers.createdAt", "Müşteri kartının açılış tarihi (işlem dönemi değildir)", r => r.CreatedAt)
        .Count("customers.count", "Müşteri kartı sayısı")
        .Measure("customers.orderCount", "Dönemdeki yetkili sipariş sayısı (iptaller dahil)", r => r.OrderCount, "sum", filterable: true)
        .RequirePermission("customers.orderCount", Permissions.OrdersView)
        .Measure("customers.returnCount", "Dönemdeki yetkili iade kaydı sayısı", r => r.ReturnCount, "sum", filterable: true)
        .RequirePermission("customers.returnCount", Permissions.OrdersReturnsView)
        .Seal();

    public static IQueryable<CustomerReportRow> Query(OrderDbContext db, DynamicReportPlan plan, EfektifYetkiler effective,
        OrderReportScope? resolvedPeriod = null)
    {
        if (plan.Source != Id) throw new ArgumentException("Müşteri kaynağı bekleniyor.");
        if (!CanAccess(effective)) throw new UnauthorizedAccessException();
        var period = resolvedPeriod ?? plan.Scope();
        var parameters = new List<object> {
            new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = period.From.UtcDateTime },
            new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = period.To.UtcDateTime }
        };
        string Channel(string subject, string name)
        {
            var ids = ReportSourceCatalog.ResolveChannels(subject, effective);
            if (ids is null) return "TRUE";
            parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids });
            return $"o.\"FirmPlatformId\" = ANY (@{name})";
        }
        const string empty = "SELECT NULL::uuid AS \"MemberId\", 0::numeric AS n WHERE FALSE";
        // Only server constants form SQL. Unauthorized domains are not queried at all.
        var orders = effective.Var(Permissions.OrdersView) ? $"""
            SELECT o."MemberId", COUNT(*)::numeric AS n FROM "order".ord_orders o
            WHERE NOT o."IsDeleted" AND o."MemberId" IS NOT NULL
              AND o."CreatedAt" >= @from AND o."CreatedAt" < @to AND {Channel("orders", "orderChannels")}
            GROUP BY o."MemberId"
            """ : empty;
        var returns = effective.Var(Permissions.OrdersReturnsView) ? $"""
            SELECT o."MemberId", COUNT(*)::numeric AS n FROM "order".ord_returns r
            JOIN "order".ord_orders o ON o."Id" = r."OrderId"
            WHERE NOT r."IsDeleted" AND NOT o."IsDeleted" AND o."MemberId" IS NOT NULL
              AND r."CreatedAt" >= @from AND r."CreatedAt" < @to AND {Channel("returns", "returnChannels")}
            GROUP BY o."MemberId"
            """ : empty;
        // Preaggregate independently: joining orders and returns must never multiply counts.
        // No period restriction on m.CreatedAt: old customers can have new activity.
        var sql = $"""
            SELECT m."Id", m."FirstName", m."LastName", m."CreatedAt",
                CASE WHEN m."IsActive" THEN 'Aktif' ELSE 'Pasif' END AS "Active",
                COALESCE(oc.n, 0)::numeric AS "OrderCount", COALESCE(rc.n, 0)::numeric AS "ReturnCount"
            FROM crm.crm_members m
            LEFT JOIN ({orders}) oc ON oc."MemberId" = m."Id"
            LEFT JOIN ({returns}) rc ON rc."MemberId" = m."Id"
            WHERE NOT m."IsDeleted" AND m."AnonymizedAt" IS NULL
            """;
        var rows = db.Database.SqlQueryRaw<CustomerReportRow>(sql, parameters.ToArray());
        return plan.Predicate is null ? rows : CustomerReportRelations.Apply(rows, db, plan.Predicate, period, effective);
    }
}
