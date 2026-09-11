using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Authorization;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Source adapter for the generic predicate compiler; no report templates or string SQL.</summary>
public static class OrderReportPredicates
{
    public static IQueryable<OrderEntity> Apply(IQueryable<OrderEntity> source, ReportPredicate predicate,
        EfektifYetkiler effective, IOrderDbContext? db)
        => Schema(effective, db).Apply(source, predicate, ReportSourceCatalog.ResolvePermissions(effective));

    public static ReportPredicateSchema<OrderEntity> Schema(EfektifYetkiler effective, IOrderDbContext? db)
    {
        var channels = ReportSourceCatalog.ResolveChannels("orders", effective);
        var permissions = ReportSourceCatalog.ResolvePermissions(effective);
        var schema = ReportBusinessDictionary.Orders.Predicates(
            o => !o.IsDeleted && (channels == null || channels.Contains(o.FirmPlatformId)));
        if (db is not null)
        {
            // Scoped catalog permission cannot authorize a global catalog projection.
            // Avoid even constructing that projection when the capability is absent.
            if (permissions.Contains(Permissions.CatalogProductsView))
                ReportBusinessDictionary.OrderItems.Bind(schema, db.ReportLines(), i => !i.IsDeleted, permissions);
            ReportBusinessDictionary.OrderPayments.Bind(schema, db.OrderPayments, p => !p.IsDeleted, permissions);
            if (effective.Var(Permissions.OrdersReturnsView))
            {
                var returnChannels = effective.Kanallar(Permissions.OrdersReturnsView)?.ToArray();
                ReportBusinessDictionary.OrderReturns.Bind(schema, db.Returns,
                    r => !r.IsDeleted && !r.Order.IsDeleted && (returnChannels == null || returnChannels.Contains(r.Order.FirmPlatformId)), permissions);
            }
        }
        return schema;
    }
}
