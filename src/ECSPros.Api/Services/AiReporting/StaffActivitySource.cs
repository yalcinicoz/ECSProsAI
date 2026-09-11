using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.AiReporting;

// Query-only projection; no new table or mutation of operational records.
public sealed class StaffActivityRow
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string? ActorName { get; set; }
    public string Activity { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string OrderNumber { get; set; } = "";
    public Guid FirmPlatformId { get; set; }
}

public static class StaffActivitySource
{
    public const string Id = "staffActivities";
    public const string Capability = "reports.ai.staff.scope";
    public static bool CanAccess(EfektifYetkiler rights) => rights.Var(ReportDictionary.UsePermission)
        && rights.Var(Permissions.IamUsersView) && rights.Kanallar(Permissions.IamUsersView) is null
        && (rights.Var(Permissions.FulfillmentView) || rights.Var(Permissions.OrdersInvoicesView));

    public static ReportEntityDefinition<StaffActivityRow> Dictionary { get; } = new ReportEntityDefinition<StaffActivityRow>(Permissions.IamUsersView)
        .Text("staff.actorId", "Personel kimliği", r => r.ActorId.ToString())
        .Text("staff.actorName", "Personelin güncel adı (eksik/silinmiş kullanıcıda boş)", r => r.ActorName)
        .Text("staff.activity", "İşlem türü: toplama okutması / paket tamamlanması / panel fatura kaydı", r => r.Activity)
        .Text("staff.orderNumber", "Bağlı sipariş no", r => r.OrderNumber)
        .Value("staff.channelId", "Yetkili satış kanalı kimliği", r => r.FirmPlatformId)
        .Value("staff.occurredAt", "İşlem kayıt zamanı", r => r.OccurredAt)
        .Count("staff.count", "İşlem kaydı sayısı (ürün adedi, süre veya puan değildir)")
        .Seal();

    public static IQueryable<StaffActivityRow> Query(OrderDbContext db, DynamicReportPlan plan, EfektifYetkiler rights,
        OrderReportScope? resolvedPeriod = null)
    {
        if (plan.Source != Id) throw new ArgumentException("Personel işlem kaynağı bekleniyor.");
        if (!CanAccess(rights)) throw new UnauthorizedAccessException();
        var period = resolvedPeriod ?? plan.Scope();
        var args = new List<object> {
            new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = period.From.UtcDateTime },
            new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = period.To.UtcDateTime }
        };
        string Channel(string permission, string parameter)
        {
            var domain = rights.Kanallar(permission); var report = rights.Kanallar(ReportDictionary.UsePermission);
            var channels = domain is null ? report?.ToArray() : report is null ? domain.ToArray() : domain.Intersect(report).ToArray();
            if (channels is null) return "TRUE";
            args.Add(new NpgsqlParameter(parameter, NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = channels });
            return $"o.\"FirmPlatformId\" = ANY (@{parameter})";
        }
        var branches = new List<string>();
        if (rights.Var(Permissions.FulfillmentView)) branches.Add($"""
            SELECT l."Id", l."ActorId", l."Action" AS "Activity", l."CreatedAt" AS "OccurredAt",
                   o."OrderNumber", o."FirmPlatformId"
            FROM fulfillment.ful_operation_logs l JOIN "order".ord_orders o ON o."Id"=l."OrderId"
            WHERE NOT l."IsDeleted" AND NOT o."IsDeleted" AND l."ActorId" <> '00000000-0000-0000-0000-000000000000'::uuid
              AND l."Action" IN ('line_picked','package_packed') AND l."CreatedAt">=@from AND l."CreatedAt"<@to
              AND {Channel(Permissions.FulfillmentView, "operationChannels")}
            """);
        // External registration/import is not invoice issuance by the registering user.
        // Package-triggered and manual internal invoices cannot reliably be distinguished today.
        if (rights.Var(Permissions.OrdersInvoicesView)) branches.Add($"""
            SELECT i."Id", i."CreatedBy" AS "ActorId", 'invoice_recorded'::text AS "Activity", i."CreatedAt" AS "OccurredAt",
                   o."OrderNumber", o."FirmPlatformId"
            FROM "order".ord_invoices i JOIN "order".ord_orders o ON o."Id"=i."OrderId"
            WHERE NOT i."IsDeleted" AND NOT o."IsDeleted" AND i."LegacyInvoiceId" IS NULL AND i."NumberSource"='internal'
              AND i."CreatedBy" IS NOT NULL AND i."CreatedBy" <> '00000000-0000-0000-0000-000000000000'::uuid
              AND i."CreatedAt">=@from AND i."CreatedAt"<@to AND {Channel(Permissions.OrdersInvoicesView, "invoiceChannels")}
            """);
        var sql = $"""
            SELECT a.*, CASE WHEN u."Id" IS NULL THEN NULL ELSE concat_ws(' ', u."FirstName", u."LastName") END AS "ActorName"
            FROM ({string.Join(" UNION ALL ", branches)}) a
            LEFT JOIN iam.iam_users u ON u."Id"=a."ActorId" AND NOT u."IsDeleted"
            """;
        var rows = db.Database.SqlQueryRaw<StaffActivityRow>(sql, args.ToArray());
        return plan.Predicate is null ? rows : Dictionary.Predicates(_ => true).Apply(rows, plan.Predicate,
            ReportSourceCatalog.ResolvePermissions(rights));
    }
}
