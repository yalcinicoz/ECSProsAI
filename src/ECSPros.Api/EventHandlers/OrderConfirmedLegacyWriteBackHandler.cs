using ECSPros.Api.Services.Legacy;
using ECSPros.Catalog.Application.Services;
using ECSPros.Core.Application.Services;
using ECSPros.Crm.Application.Services;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// B4 (2026-07-23): Sipariş ONAYLANDIĞINDA (OrderConfirmedEvent) eski sisteme (juludedb)
/// geri-yazar — kullanıcı kararı: "sipariş kesinleşti" = confirmed. Yalnız Mishar siparişleri.
/// Eski üye kaydı yoksa AÇILIR (webmembers) ve dönen legacy Id yeni üyeye işlenir. HATA-GÜVENLİ:
/// tüm hatalar yutulur — geri-yazma başarısız olsa bile sipariş onayı ASLA bozulmaz; her deneme
/// integration_logs'a yazılır (başarısızlar sonradan tekrar denenebilir). Varsayılan DRY-RUN:
/// gerçek yazım için Legacy:MySqlConnection + WriteBack:Enabled=true + DryRun=false gerekir.
/// </summary>
public class OrderConfirmedLegacyWriteBackHandler(
    ILegacyGateway gateway,
    IConfiguration config,
    IOrderDbContext orderDb,
    ICrmDbContext crmDb,
    ICatalogDbContext catalogDb,
    ICoreDbContext coreDb,
    IIntegrationDbContext integrationDb,
    ILogger<OrderConfirmedLegacyWriteBackHandler> logger) : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent e, CancellationToken ct)
    {
        try
        {
            if (!gateway.IsConfigured || !config.GetValue("Legacy:WriteBack:Enabled", false))
                return; // katman kapalı — sessizce geç

            var order = await orderDb.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == e.OrderId, ct);
            if (order is null) return;
            if (order.LegacyOrderId is not null) return; // zaten yazılmış (idempotent)

            // Yalnız Mishar
            var misharFp = await coreDb.FirmPlatforms.AsNoTracking()
                .Where(p => p.Code == "mishar").Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (misharFp is null || order.FirmPlatformId != misharFp) return;

            // Üye bilgisi (varsa) — yoksa misafir: teslimat alıcı bilgisinden
            ECSPros.Crm.Domain.Entities.Member? member = null;
            if (order.MemberId is { } mid)
                member = await crmDb.Members.FirstOrDefaultAsync(m => m.Id == mid, ct);

            var (recFirst, recLast) = SplitName(order.ShippingRecipientName);
            string firstName = member?.FirstName ?? recFirst;
            string lastName = member?.LastName ?? recLast;

            // Geo Guid → isim (eski sistem metin ister)
            string cityName = await NameOfCity(order.ShippingCityId, ct);
            string districtName = await NameOfDistrict(order.ShippingDistrictId, ct);
            string? neighborhoodName = order.ShippingNeighborhoodId is { } nid ? await NameOfNeighborhood(nid, ct) : null;

            // Kalemler: yeni VariantId → barkod + ürün kodu (katalogdan)
            var lines = new List<LegacyOrderLine>();
            foreach (var it in order.Items)
            {
                var vc = await catalogDb.ProductVariants.AsNoTracking()
                    .Where(v => v.Id == it.VariantId)
                    .Select(v => new { v.Barcode, Code = catalogDb.Products.Where(p => p.Id == v.ProductId).Select(p => p.Code).FirstOrDefault() })
                    .FirstOrDefaultAsync(ct);
                lines.Add(new LegacyOrderLine(
                    LegacyVariantId: 0, Barcode: vc?.Barcode ?? "", ProductCode: vc?.Code ?? it.Sku,
                    ProductName: it.ProductName, VariantInfo: it.VariantInfo,
                    UnitPrice: it.UnitPrice, Quantity: it.Quantity, Discount: it.DiscountAmount));
            }

            var data = new LegacyOrderWriteback(
                OrderId: order.Id, NewOrderNumber: order.OrderNumber, ExistingLegacyMemberId: member?.LegacyMemberId,
                FirstName: firstName, LastName: lastName, Email: member?.Email, Phone: member?.Phone ?? order.ShippingRecipientPhone,
                IdentityNumber: member?.IdentityNumber,
                RecipientName: order.ShippingRecipientName, RecipientPhone: order.ShippingRecipientPhone,
                AddressLine: order.ShippingAddressLine, PostalCode: order.ShippingPostalCode,
                CityName: cityName, DistrictName: districtName, NeighborhoodName: neighborhoodName,
                Subtotal: order.Subtotal, Discount: order.TotalDiscount, Expense: order.TotalExpense,
                Tax: order.TotalTax, GrandTotal: order.GrandTotal, Currency: order.CurrencyCode,
                SourceChannel: "YeniSite", OrderDate: order.CreatedAt, Lines: lines);

            var result = await gateway.WriteOrderBackAsync(data, ct);

            integrationDb.IntegrationLogs.Add(new IntegrationLog
            {
                FirmIntegrationId = Guid.Empty,
                ServiceType = "legacy",
                OperationType = "writeback_order",
                Status = result.Success ? (result.DryRun ? "dry_run" : "success") : "failure",
                RequestPayload = Trunc(result.PlanSql, 8000),
                ErrorMessage = result.Success ? null : Trunc(result.Message, 1000),
                ReferenceId = order.Id,
                ReferenceType = "Order",
            });
            await integrationDb.SaveChangesAsync(ct);

            if (result is { Success: true, DryRun: false, LegacyOrderId: { } lid })
            {
                order.LegacyOrderId = lid;
                if (member is not null && member.LegacyMemberId is null && result.LegacyMemberId is { } lmid)
                    member.LegacyMemberId = lmid;
                await orderDb.SaveChangesAsync(ct);
                if (member is not null) await crmDb.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            // ASLA fırlatma — sipariş onayı geri-yazmadan bağımsız tamamlanır
            logger.LogError(ex, "Legacy geri-yazma handler hatası (orderId={OrderId})", e.OrderId);
        }
    }

    private async Task<string> NameOfCity(Guid id, CancellationToken ct) =>
        id == Guid.Empty ? "" : (await crmDb.Cities.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.NameI18n).FirstOrDefaultAsync(ct))?.GetValueOrDefault("tr") ?? "";
    private async Task<string> NameOfDistrict(Guid id, CancellationToken ct) =>
        id == Guid.Empty ? "" : (await crmDb.Districts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.NameI18n).FirstOrDefaultAsync(ct))?.GetValueOrDefault("tr") ?? "";
    private async Task<string> NameOfNeighborhood(Guid id, CancellationToken ct) =>
        (await crmDb.Neighborhoods.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.NameI18n).FirstOrDefaultAsync(ct))?.GetValueOrDefault("tr") ?? "";

    private static (string, string) SplitName(string full)
    {
        full = (full ?? "").Trim();
        var i = full.IndexOf(' ');
        return i < 0 ? (full, "") : (full[..i], full[(i + 1)..].Trim());
    }
    private static string? Trunc(string? s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
