using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

public static class DynamicReportMetadata
{
    public static IReadOnlyList<ReportField> Details(IReadOnlySet<string> permissions, string subject = "orders", IReadOnlyList<ReportField>? attributes = null) => subject switch
    {
        "orders" => ReportBusinessDictionary.Orders.DescribeDetails(permissions),
        "stock" => DynamicStockSource.Dictionary(attributes ?? []).DescribeDetails(permissions),
        MovementReportSource.Id => MovementReportSource.Dictionary.DescribeDetails(permissions),
        ReturnReportSource.Id => ReturnReportSource.Dictionary.DescribeDetails(permissions),
        CustomerReportSource.Id => permissions.Contains(CustomerReportSource.GlobalScopeCapability) ? CustomerReportSource.Dictionary.DescribeDetails(permissions) : [],
        StaffActivitySource.Id => permissions.Contains(StaffActivitySource.Capability) ? StaffActivitySource.Dictionary.DescribeDetails(permissions) : [],
        ProductCardReportSource.Id => permissions.Contains(ProductCardReportSource.Capability) ? ProductCardReportSource.Dictionary.DescribeDetails(permissions) : [],
        _ => []
    };
    public static IReadOnlyList<ReportField> Fields(IReadOnlySet<string> permissions, string subject = "orders", IReadOnlyList<ReportField>? attributes = null)
    {
        if (subject == "stock") return DynamicStockSource.Dictionary(attributes ?? []).Describe(permissions);
        if (subject == MovementReportSource.Id) return MovementReportSource.Dictionary.Describe(permissions);
        if (subject == ReturnReportSource.Id) return ReturnReportSource.Dictionary.Describe(permissions);
        if (subject == CustomerReportSource.Id) return CustomerReportRelations.Fields(permissions);
        if (subject == StaffActivitySource.Id) return permissions.Contains(StaffActivitySource.Capability) ? StaffActivitySource.Dictionary.Describe(permissions) : [];
        if (subject == ProductCardReportSource.Id) return ProductCardReportSource.Fields(permissions);
        if (subject != "orders") return [];
        if (!permissions.Contains(ReportDictionary.UsePermission) || !permissions.Contains(Permissions.OrdersView)) return [];
        return ReportBusinessDictionary.Orders.Describe(permissions)
            .Concat(ReportBusinessDictionary.OrderItems.Describe(permissions))
            .Concat(ReportBusinessDictionary.OrderPayments.Describe(permissions))
            .Concat(ReportBusinessDictionary.OrderReturns.Describe(permissions)).ToArray();
    }
}
