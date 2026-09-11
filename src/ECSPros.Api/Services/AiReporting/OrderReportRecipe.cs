using System.Globalization;
using System.Text.RegularExpressions;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Api.Services.AiReporting;

public sealed record OrderReportRecipe(OrderReportScope Scope, GridRequest BaseFilters, bool Details)
{
    public const string Subject = "orders";
    public static IReadOnlyList<ReportField> Fields { get; } = new[]
    {
        Field("count", "Sipariş sayısı", "metric"), Field("amount", "Sipariş tutarı", "metric"),
        Field("orderNumber", "Sipariş no"), Field("createdAt", "Sipariş tarihi", operators: new[] { "between" }),
        Field("status", "Sipariş durumu"), Field("paymentStatus", "Ödeme durumu"), Field("paymentMethod", "Ödeme yöntemi"),
        Field("firmPlatformId", "Kanal"), Field("orderType", "Sipariş tipi"), Field("currencyCode", "Para birimi")
    };
    private static ReportField Field(string id, string label, string kind = "dimension", string[]? operators = null) =>
        new("orders." + id, label, kind, Permissions.OrdersView,
            id == "amount" ? "Kayıtlı GrandTotal; net satış/tahsilat/iade değildir. Para birimleri ayrı tutulur." : label,
            operators ?? (kind == "metric" ? Array.Empty<string>()
                : id is "orderNumber" or "orderType" or "firmPlatformId" or "currencyCode" ? new[] { "eq" } : new[] { "eq", "in" }));
    public static IReadOnlyList<ReportField> ForPermissions(IReadOnlySet<string> permissions) =>
        permissions.Contains(ReportDictionary.UsePermission) && permissions.Contains(Permissions.OrdersView) ? Fields : Array.Empty<ReportField>();

    public static OrderReportRecipe Parse(ReportDefinition recipe, IReadOnlySet<string> permissions)
    {
        if (ForPermissions(permissions).Count == 0) throw new UnauthorizedAccessException();
        var fields = Fields.ToDictionary(f => f.Id);
        bool Valid(string[]? ids, string kind, int min, int max) => ids is not null && ids.Length >= min && ids.Length <= max
            && ids.Distinct().Count() == ids.Length && ids.All(id => id is not null && fields.TryGetValue(id, out var f) && f.Kind == kind);
        if (recipe.Version != 1 || recipe.Subject != Subject || recipe.Presentation != "table" || recipe.Limit is < 1 or > 1000
            || !Valid(recipe.Metrics, "metric", 1, 2) || !Valid(recipe.Dimensions, "dimension", 1, 8)
            || !recipe.Dimensions!.Contains("orders.currencyCode") || recipe.Filters is null || recipe.Filters.Length > 8)
            throw new ArgumentException("Sipariş tarifi geçerli değil; para birimi kolonu zorunludur.");
        var dimensions = recipe.Dimensions!;
        var details = dimensions.Contains("orders.orderNumber");
        if (!details && dimensions.Any(d => d is "orders.createdAt" or "orders.paymentMethod"))
            throw new ArgumentException("Tarih/ödeme yöntemi kolonu detay raporunda kullanılabilir.");
        var filters = new List<GridFilter>();
        DateTimeOffset? from = null, to = null;
        var seen = new HashSet<string>();
        foreach (var f in recipe.Filters)
        {
            if (f?.Field is null || !seen.Add(f.Field) || !fields.TryGetValue(f.Field, out var field)
                || f.Operator is null || !field.Operators.Contains(f.Operator) || f.Values is null
                || f.Values.Length is < 1 or > 20 || f.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 128 || v.Any(char.IsControl))
                || f.Values.Distinct().Count() != f.Values.Length || (f.Operator == "eq" && f.Values.Length != 1))
                throw new ArgumentException("Sipariş filtresi geçerli değil.");
            if (f.Field == "orders.createdAt")
            {
                if (f.Values.Length != 2) throw new ArgumentException("Başlangıç ve bitiş tarihi gereklidir.");
                from = Date(f.Values[0]); to = Date(f.Values[1]);
            }
            else
            {
                if (f.Values.Any(v => v.Contains(','))) throw new ArgumentException("Filtre değerinde virgül desteklenmiyor.");
                if (f.Field == "orders.status" && f.Values.Any(v => !OrderGrid.Statuses.Contains(v))
                    || f.Field == "orders.paymentStatus" && f.Values.Any(v => !OrderGrid.PaymentStatuses.Contains(v))
                    || f.Field == "orders.paymentMethod" && f.Values.Any(v => !OrderGrid.PaymentMethods.Contains(v))
                    || f.Field == "orders.firmPlatformId" && f.Values.Any(v => !Guid.TryParse(v, out var id) || id == Guid.Empty))
                    throw new ArgumentException("Sipariş filtre değeri desteklenmiyor.");
                // OrderGrid text fields only support eq, not a list. Do not silently weaken IN.
                if (f.Operator == "in" && f.Field is "orders.orderNumber" or "orders.orderType")
                    throw new ArgumentException("Bu metin filtresinde tam eşleşme kullanılmalıdır.");
                filters.Add(new(f.Field[7..], f.Operator, string.Join(',', f.Values)));
            }
        }
        if (from is null || to is null || to <= from || to - from > TimeSpan.FromDays(366))
            throw new ArgumentException("Sipariş raporunda en çok366 günlük başlangıç/bitiş aralığı zorunludur.");
        return new(new(from.Value, to.Value), new GridRequest { Filters = filters }, details);
    }
    private static DateTimeOffset Date(string value) => Regex.IsMatch(value, @"T.*(?:Z|[+-]\d{2}:\d{2})$")
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        ? date : throw new ArgumentException("Tarih saat dilimiyle ISO8601 biçiminde olmalıdır.");
}
