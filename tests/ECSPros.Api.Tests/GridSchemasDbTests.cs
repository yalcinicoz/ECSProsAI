using ECSPros.Api.Grid;
using ECSPros.Accounts.Infrastructure.Persistence;
using ECSPros.Accounts.Application.Queries.GetAccountGroups;
using ECSPros.Accounts.Application.Queries.GetCurrentAccounts;
using ECSPros.Catalog.Application.Queries.GetAttributeTypes;
using ECSPros.Catalog.Application.Queries.GetAdminProductSubmissions;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using ECSPros.Catalog.Application.Queries.GetProducts;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Cms.Application.Queries.GetPages;
using ECSPros.Cms.Infrastructure.Persistence;
using ECSPros.Crm.Application.Queries.GetMembers;
using ECSPros.Crm.Application.Tickets.Queries;
using ECSPros.Core.Application.Queries.GetPlatformTypes;
using ECSPros.Core.Application.Queries.GetIntegrationServices;
using ECSPros.Core.Application.Queries.GetFirms;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Crm.Application.Queries.GetMemberGroups;
using ECSPros.Crm.Infrastructure.Persistence;
using ECSPros.Finance.Application.Queries.GetSupplierInvoices;
using ECSPros.Finance.Infrastructure.Persistence;
using ECSPros.Fulfillment.Application.Queries.GetPickingPlans;
using ECSPros.Fulfillment.Infrastructure.Persistence;
using ECSPros.Iam.Application.Queries.GetAuditLogs;
using ECSPros.Iam.Application.Queries.GetUsers;
using ECSPros.Iam.Infrastructure.Persistence;
using ECSPros.Integration.Application.Queries.GetIntegrationLogs;
using ECSPros.Integration.Infrastructure.Persistence;
using ECSPros.Inventory.Application.Queries.GetTransfers;
using ECSPros.Inventory.Application.Queries.GetWarehouses;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Order.Application.Queries.GetGiftCards;
using ECSPros.Order.Application.Queries.GetInvoices;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Queries.GetQuotes;
using ECSPros.Order.Application.Queries.GetReturns;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Pos.Application.Queries.GetPosSales;
using ECSPros.Pos.Infrastructure.Persistence;
using ECSPros.Promotion.Application.Queries.GetCampaigns;
using ECSPros.Promotion.Application.Queries.GetCampaignTypes;
using ECSPros.Promotion.Application.Queries.GetCoupons;
using ECSPros.Promotion.Infrastructure.Persistence;
using ECSPros.Requests.Application.Queries.GetRequests;
using ECSPros.Requests.Infrastructure.Persistence;
using ECSPros.Storefront.Application.Queries.GetStockAlertsForAdmin;
using ECSPros.Storefront.Application.Queries.GetSavedSearchesForAdmin;
using ECSPros.Storefront.Application.Queries.GetChannelCategories;
using ECSPros.Storefront.Application.Queries.GetChannelProductsAdmin;
using ECSPros.Storefront.Application.Queries.GetCollectionsForModeration;
using ECSPros.Storefront.Application.Queries.GetContactMessages;
using ECSPros.Storefront.Application.Queries.GetReviewsForModeration;
using ECSPros.Storefront.Infrastructure.Persistence;
using ECSPros.Iam.Application.Yetkilendirme;
using ECSPros.Storefront.Application.Queries.GetNewsletterSubscriptions;
using ECSPros.Procurement.Application.Queries.GetPurchaseOrders;
using ECSPros.Procurement.Application.Queries.GetReceiptBatches;
using ECSPros.Procurement.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// DataGrid F4 — TÜM şemaların EF/Npgsql SQL çevirisi (SALT OKUNUR): her filtre alanı tipine uygun örnek değerle, her sıralama anahtarı
/// asc/desc ile gerçek DB'de COUNT/Take(1) çalıştırılır; çevrilemeyen ifade burada patlar. Yalnız ECSPROS_TEST_DB verildiğinde çalışır.
/// </summary>
[TestClass]
public sealed class GridSchemasDbTests
{
    private static NpgsqlDataSource? Ds()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson(); return b.Build();
    }
    private static DbContextOptions<T> Opt<T>(NpgsqlDataSource ds) where T : DbContext => new DbContextOptionsBuilder<T>().UseNpgsql(ds).Options;

    private static string SampleValue(GridFieldType t, IReadOnlyCollection<string>? allowed) => t switch
    {
        GridFieldType.Text => "a",
        GridFieldType.Enum => allowed is { Count: > 0 } ? string.Join(",", allowed.Take(2)) : "x",
        GridFieldType.Date => "2026-08-01,2026-09-08",
        GridFieldType.Number => "1",
        GridFieldType.Bool => "true",
        GridFieldType.Guid => Guid.Empty.ToString(),
        _ => "1",
    };

    private static async Task<List<string>> ExerciseAsync<T>(string ad, GridSchema<T> schema, IQueryable<T> baseQuery) where T : class
    {
        var hatalar = new List<string>();
        foreach (var key in schema.FilterableFields)
        {
            var type = schema.FieldTypeOf(key)!.Value;
            var req = new GridRequest { Filters = { new GridFilter(key, GridRequest.OpAuto, SampleValue(type, schema.AllowedValuesOf(key))) } };
            try { _ = await schema.ApplyFilters(baseQuery, req).CountAsync(); }
            catch (Exception ex) { hatalar.Add($"{ad}.filter[{key}:{type}] → {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
            // ikinci operatör: text startswith / number between / date gte / enum eq
            var op2 = type switch { GridFieldType.Text => "startswith", GridFieldType.Number => "between", GridFieldType.Date => "gte", GridFieldType.Enum => "eq", _ => null };
            if (op2 is null) continue;
            var val2 = type switch { GridFieldType.Number => "0;10", GridFieldType.Date => "2026-08-01", GridFieldType.Enum => (schema.AllowedValuesOf(key)?.FirstOrDefault() ?? "x"), _ => "a" };
            try { _ = await schema.ApplyFilters(baseQuery, new GridRequest { Filters = { new GridFilter(key, op2, val2) } }).CountAsync(); }
            catch (Exception ex) { hatalar.Add($"{ad}.filter[{key}:{op2}] → {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
        }
        foreach (var key in schema.SortableFields)
            foreach (var dir in new[] { "asc", "desc" })
            {
                try { _ = await schema.ApplySort(baseQuery, new GridRequest { Sort = key, Dir = dir }).Take(1).ToListAsync(); }
                catch (Exception ex) { hatalar.Add($"{ad}.sort[{key} {dir}] → {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
            }
        try { _ = await schema.ApplySort(baseQuery, null).Take(1).ToListAsync(); }
        catch (Exception ex) { hatalar.Add($"{ad}.sort[default] → {ex.Message.Split('\n')[0]}"); }
        return hatalar;
    }

    [TestMethod]
    public async Task All_grid_schemas_translate_to_sql()
    {
        var ds = Ds();
        if (ds is null) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }
        var hatalar = new List<string>();

        await using (var db = new OrderDbContext(Opt<OrderDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("orders", OrderGrid.Schema, db.Orders.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("invoices", InvoiceGrid.Schema, db.Invoices.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("returns", ReturnGrid.Schema, db.Returns.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("quotes", QuoteGrid.Schema, db.Quotes.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("gift-cards", GiftCardGrid.Schema, db.GiftCards.AsNoTracking()));
        }
        await using (var db = new CatalogDbContext(Opt<CatalogDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("products", ProductGrid.Schema, db.Products.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("product-groups", ProductGroupGrid.Schema, db.ProductGroups.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("attribute-types", AttributeTypeGrid.Schema, db.AttributeTypes.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("product-submissions", ProductSubmissionGrid.Schema, db.ProductSubmissions.AsNoTracking()));
            // Kanal Ürünleri: liste tabanı CATALOG ürünleri (kanal durumu bellekte çözülür, şemada yok).
            hatalar.AddRange(await ExerciseAsync("channel-products", ChannelProductGrid.Schema, db.Products.AsNoTracking()));
        }
        await using (var db = new CoreDbContext(Opt<CoreDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("firms", FirmGrid.Schema, db.Firms.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("platform-types", PlatformTypeGrid.Schema, db.PlatformTypes.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("integration-services", IntegrationServiceGrid.Schema, db.IntegrationServices.AsNoTracking()));
        }
        await using (var db = new CrmDbContext(Opt<CrmDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("member-groups", MemberGroupGrid.Schema, db.MemberGroups.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("members", MemberGrid.Schema, db.Members.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("tickets", TicketGrid.Schema, db.Tickets.AsNoTracking()));
        }
        await using (var db = new InventoryDbContext(Opt<InventoryDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("stocks", StockGrid.Schema, db.Stocks.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("warehouses", WarehouseGrid.Schema, db.Warehouses.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("transfers", TransferGrid.Schema, db.TransferRequests.AsNoTracking()));
        }
        await using (var db = new PromotionDbContext(Opt<PromotionDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("campaigns", CampaignGrid.Schema, db.Campaigns.AsNoTracking()));
            // 2026-09-09 ("tüm tablolar orders gibi olsun"): DataGrid'e alınan sayfalar
            hatalar.AddRange(await ExerciseAsync("coupons", CouponGrid.Schema, db.Coupons.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("campaign-types", CampaignTypeGrid.Schema, db.CampaignTypes.AsNoTracking()));
        }
        // "Tüm sütunlarda filtre" (2026-09-08): backend grid'e alınan 9 sayfa
        await using (var db = new IamDbContext(Opt<IamDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("users", UserGrid.Schema, db.Users.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("audit-logs", AuditLogGrid.Schema, db.AuditLogs.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("permission-logs", YetkiLogGrid.Schema, YetkiLogGrid.YalnizYetkiOlaylari(db.AuditLogs.AsNoTracking())));
            hatalar.AddRange(await ExerciseAsync("permission-catalog", YetkiKatalogGrid.Schema, db.Permissions.AsNoTracking()));
        }
        await using (var db = new FinanceDbContext(Opt<FinanceDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("supplier-invoices", SupplierInvoiceGrid.Schema, db.SupplierInvoices.AsNoTracking()));
        await using (var db = new FulfillmentDbContext(Opt<FulfillmentDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("picking-plans", PickingPlanGrid.Schema, db.PickingPlans.AsNoTracking()));
        await using (var db = new PosDbContext(Opt<PosDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("pos-sales", PosSaleGrid.Schema, db.PosSales.AsNoTracking()));
        await using (var db = new AccountsDbContext(Opt<AccountsDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("accounts", CurrentAccountGrid.Schema, db.CurrentAccounts.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("account-groups", AccountGroupGrid.Schema, db.AccountGroups.AsNoTracking()));
        }
        await using (var db = new StorefrontDbContext(Opt<StorefrontDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("reviews", ReviewModerationGrid.Schema, db.ProductReviews.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("contact-messages", ContactMessageGrid.Schema, db.ContactMessages.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("newsletter", NewsletterSubscriptionGrid.Schema, db.NewsletterSubscriptions.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("collections", CollectionModerationGrid.Schema, db.Collections.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("channel-categories", ChannelCategoryGrid.Schema, db.ChannelCategories.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("stock-alerts", StockAlertGrid.Schema, db.StockAlerts.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("saved-searches", SavedSearchGrid.Schema, db.SavedSearches.AsNoTracking()));
        }
        await using (var db = new ProcurementDbContext(Opt<ProcurementDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("purchase-orders", PurchaseOrderGrid.Schema, db.PurchaseOrders.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("receipts", ReceiptBatchGrid.Schema, db.ReceiptBatches.AsNoTracking()));
        }
        await using (var db = new CmsDbContext(Opt<CmsDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("cms-pages", PageGrid.Schema, db.Pages.AsNoTracking()));
        await using (var db = new RequestsDbContext(Opt<RequestsDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("requests", RequestGrid.Schema, db.ProjectRequests.AsNoTracking()));
        await using (var db = new IntegrationDbContext(Opt<IntegrationDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("integration-logs", IntegrationLogGrid.Schema, db.IntegrationLogs.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("tracking-outbox", TrackingOutboxGrid.Schema, db.TrackingEventOutbox.AsNoTracking()));
        }

        Assert.AreEqual(0, hatalar.Count, "SQL'e çevrilemeyen alanlar:\n" + string.Join("\n", hatalar));
    }
}
