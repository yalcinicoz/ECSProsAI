using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

// Selection by related activity, not a new report template or a change to activity counts.
public static class CustomerReportRelations
{
    public const string Orders = "customers.orders";

    public static IReadOnlyList<ReportField> Fields(IReadOnlySet<string> permissions)
    {
        if (!permissions.Contains(CustomerReportSource.GlobalScopeCapability)) return [];
        var own = CustomerReportSource.Dictionary.Describe(permissions);
        if (!permissions.Contains(Permissions.OrdersView)) return own;
        var related = ReportBusinessDictionary.Orders.Describe(permissions)
            .Concat(ReportBusinessDictionary.OrderItems.Describe(permissions))
            .Concat(ReportBusinessDictionary.OrderPayments.Describe(permissions))
            .Concat(ReportBusinessDictionary.OrderReturns.Describe(permissions))
            .Where(f => f.Kind == "relation" || f.Operators.Count > 0)
            .Select(f => f.Kind == "relation" ? f : f with { Kind = "filter" });
        return own.Concat(new[] { new ReportField(Orders, "Dönemdeki yetkili müşteri siparişleri", "relation",
            Permissions.OrdersView, "Sipariş oluşturulma dönemi içinde var/yok koşulu; müşteri adetlerini değiştirmez.", []) })
            .Concat(related).ToArray();
    }

    public static IQueryable<CustomerReportRow> Apply(IQueryable<CustomerReportRow> rows, OrderDbContext db,
        ReportPredicate predicate, OrderReportScope period, EfektifYetkiler effective)
    {
        var permissions = ReportSourceCatalog.ResolvePermissions(effective);
        var schema = CustomerReportSource.Dictionary.Predicates(_ => true);
        if (effective.Var(Permissions.OrdersView))
        {
            var from = period.From.UtcDateTime; var to = period.To.UtcDateTime;
            var orders = db.Orders.Where(o => o.MemberId != null && o.CreatedAt >= from && o.CreatedAt < to);
            schema.Relation(Orders, orders, m => (Guid?)m.Id, o => o.MemberId,
                OrderReportPredicates.Schema(effective, db));
        }
        return schema.Apply(rows, predicate, permissions);
    }
}
