using System.Globalization;
using System.Text;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.Legacy;

/// <summary>
/// Geçici eski-sistem (juludedb) entegrasyon köprüsü (Part B, 2026-07-23). Çalışan API'nin
/// eski MySQL'e TEK bağlantı noktası. HATA-GÜVENLİ: bağlantı yapılandırılmamışsa (connection
/// string boş) IsConfigured=false ve tüm işlemler no-op — eski sistem erişilemese/kapalı olsa
/// bile yeni site normal çalışır. B4 sipariş geri-yazması buradan yürür; ÜÇ kademeli anahtar:
/// (1) MySqlConnection dolu, (2) WriteBack:Enabled=true, (3) WriteBack:DryRun=false → gerçek yazım.
/// DryRun=true iken eski DB'den OKUR (üye var mı) ama INSERT'leri ÇALIŞTIRMAZ, planı döndürür.
/// </summary>
public interface ILegacyGateway
{
    bool IsConfigured { get; }
    Task<LegacyWriteResult> WriteOrderBackAsync(LegacyOrderWriteback data, CancellationToken ct);
}

public sealed record LegacyWriteResult(
    bool Success, bool DryRun, int? LegacyOrderId, int? LegacyMemberId, string Message, string PlanSql);

public sealed record LegacyOrderLine(
    int LegacyVariantId, string Barcode, string ProductCode, string ProductName,
    string VariantInfo, decimal UnitPrice, int Quantity, decimal Discount);

public sealed record LegacyOrderWriteback(
    Guid OrderId, string NewOrderNumber, int? ExistingLegacyMemberId,
    string FirstName, string LastName, string? Email, string? Phone, string? IdentityNumber,
    string RecipientName, string RecipientPhone, string AddressLine, string? PostalCode,
    string CityName, string DistrictName, string? NeighborhoodName,
    decimal Subtotal, decimal Discount, decimal Expense, decimal Tax, decimal GrandTotal, string Currency,
    string SourceChannel, DateTime OrderDate, List<LegacyOrderLine> Lines);

public sealed class LegacyGateway(IConfiguration config, ILogger<LegacyGateway> logger) : ILegacyGateway
{
    private readonly string _conn = config["Legacy:MySqlConnection"] ?? "";
    private int MisharPlatform => config.GetValue("Legacy:MisharLegacyPlatformId", 41);
    private bool DryRun => config.GetValue("Legacy:WriteBack:DryRun", true);
    private string OrderStatus => config["Legacy:WriteBack:OrderStatus"] ?? "Onay Bekliyor";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_conn);

    public async Task<LegacyWriteResult> WriteOrderBackAsync(LegacyOrderWriteback d, CancellationToken ct)
    {
        if (!IsConfigured)
            return new(false, DryRun, null, null, "Eski DB bağlantısı yapılandırılmadı.", "");

        var plan = new StringBuilder();
        try
        {
            await using var c = new MySqlConnection(_conn);
            await c.OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);

            // 1) Üye: mevcut legacy Id → kullan; yoksa e-posta/telefonla bul; o da yoksa OLUŞTUR
            int memberId = d.ExistingLegacyMemberId ?? 0;
            if (memberId == 0) memberId = await FindMemberAsync(c, tx, d, ct);
            bool memberCreated = false;
            if (memberId == 0)
            {
                memberCreated = true;
                var insMember = BuildMemberInsert(d);
                plan.AppendLine("-- ÜYE OLUŞTUR").AppendLine(insMember.Sql);
                if (!DryRun) memberId = await ExecInsertAsync(c, tx, insMember, ct);
            }
            else plan.AppendLine($"-- Üye mevcut (webmembers.Id={memberId})");

            // 2) Teslimat adresi (her siparişte yeni satır — legacy deseni)
            var insAddr = BuildAddressInsert(d, memberId);
            plan.AppendLine("-- TESLİMAT ADRESİ").AppendLine(insAddr.Sql);
            int addressId = DryRun ? 0 : await ExecInsertAsync(c, tx, insAddr, ct);

            // 3) Sipariş
            var insOrder = BuildOrderInsert(d, memberId, addressId);
            plan.AppendLine("-- SİPARİŞ").AppendLine(insOrder.Sql);
            int orderId = DryRun ? 0 : await ExecInsertAsync(c, tx, insOrder, ct);

            // 4) Kalemler
            foreach (var line in d.Lines)
            {
                var insLine = BuildLineInsert(d, orderId, line);
                plan.AppendLine("-- KALEM: " + line.ProductCode).AppendLine(insLine.Sql);
                if (!DryRun) await ExecNonQueryAsync(c, tx, insLine, ct);
            }

            if (DryRun)
            {
                await tx.RollbackAsync(ct);
                return new(true, true, null, memberCreated ? null : memberId,
                    "DRY-RUN: plan üretildi, hiçbir şey yazılmadı.", plan.ToString());
            }

            await tx.CommitAsync(ct);
            logger.LogInformation("Sipariş geri-yazıldı: yeniNo={No} → legacyOrderId={Oid} (member={Mid})",
                d.NewOrderNumber, orderId, memberId);
            return new(true, false, orderId, memberId, "Yazıldı.", plan.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sipariş geri-yazma hatası: yeniNo={No}", d.NewOrderNumber);
            return new(false, DryRun, null, null, ex.Message, plan.ToString());
        }
    }

    private async Task<int> FindMemberAsync(MySqlConnection c, MySqlTransaction tx, LegacyOrderWriteback d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.Email) && string.IsNullOrWhiteSpace(d.Phone)) return 0;
        var sql = "SELECT Id FROM webmembers WHERE platformId=@p AND ((@em<>'' AND email=@em) OR (@ph<>'' AND phone=@ph)) LIMIT 1";
        await using var cmd = new MySqlCommand(sql, c, tx);
        cmd.Parameters.AddWithValue("@p", MisharPlatform);
        cmd.Parameters.AddWithValue("@em", d.Email ?? "");
        cmd.Parameters.AddWithValue("@ph", d.Phone ?? "");
        var r = await cmd.ExecuteScalarAsync(ct);
        return r is null or DBNull ? 0 : Convert.ToInt32(r);
    }

    private sealed record Stmt(string Sql, Dictionary<string, object?> P);

    private Stmt BuildMemberInsert(LegacyOrderWriteback d) => new(
        "INSERT INTO webmembers (platformId, tcKimlikNo, firstName, lastName, phone, email, memberTypeId, " +
        "smsSubscribed, emailSubscribed, isActive, kaynak, createdDate) " +
        "VALUES (@p, @tc, @fn, @ln, @ph, @em, 1, 0, 0, 1, @kaynak, NOW())",
        new() { ["@p"] = MisharPlatform, ["@tc"] = d.IdentityNumber ?? "", ["@fn"] = Cut(d.FirstName, 50),
            ["@ln"] = Cut(d.LastName, 50), ["@ph"] = Cut(d.Phone ?? "", 20), ["@em"] = Cut(d.Email ?? "", 100),
            ["@kaynak"] = "YeniSite" });

    private Stmt BuildAddressInsert(LegacyOrderWriteback d, int memberId) => new(
        "INSERT INTO webmemberaddresses (platformId, memberId, addressTitle, addressDetail, postalCode, " +
        "neighborhoodName, districtName, cityName, countryName, addressStatus, validationStatus, " +
        "contactFirstName, contactLastName, contactPhone, createdDate) " +
        "VALUES (@p, @mid, @title, @detail, @postal, @mah, @ilce, @il, @ulke, 1, 0, @cfn, @cln, @cphone, NOW())",
        new() { ["@p"] = MisharPlatform, ["@mid"] = memberId, ["@title"] = "Teslimat",
            ["@detail"] = Cut(d.AddressLine, 255), ["@postal"] = Cut(d.PostalCode ?? "", 10),
            ["@mah"] = Cut(d.NeighborhoodName ?? "", 50), ["@ilce"] = Cut(d.DistrictName, 50),
            ["@il"] = Cut(d.CityName, 50), ["@ulke"] = "Türkiye",
            ["@cfn"] = Cut(FirstWord(d.RecipientName), 50), ["@cln"] = Cut(RestWords(d.RecipientName), 50),
            ["@cphone"] = Cut(d.RecipientPhone, 20) });

    // orderNumber: yeni sistemin numarası aynen (M/T legacy serisiyle çakışmaz); kaynakSiparisId'de de
    // yeni no; siparisKaynagi kaynak kanalı; orderStatus config'ten (varsayılan "Onay Bekliyor").
    private Stmt BuildOrderInsert(LegacyOrderWriteback d, int memberId, int addressId) => new(
        "INSERT INTO oporders (orderNumber, platformId, sourcePlatformOrderNumber, kaynakSiparisId, siparisKaynagi, " +
        "orderStatus, orderDate, orderTime, memberId, shippingAddressId, invoiceAddressId, memberFirstName, memberLastName, " +
        "memberEMail, memberPhone, currency, exchangeRate, productTotal, taxTotal, expenseTotal, subTotal, discountTotal, " +
        "orderTotal, paidTotal, payableAmount, createdDate) " +
        "VALUES (@no, @p, @no, @no, @kaynak, @status, CURDATE(), CURTIME(), @mid, @addr, @addr, @fn, @ln, @em, @ph, @cur, 1, " +
        "@sub, @tax, @exp, @sub, @disc, @total, @total, @total, NOW())",
        new() { ["@no"] = Cut(d.NewOrderNumber, 50), ["@p"] = MisharPlatform, ["@kaynak"] = Cut(d.SourceChannel, 30),
            ["@status"] = OrderStatus, ["@mid"] = memberId, ["@addr"] = addressId, ["@fn"] = Cut(d.FirstName, 50),
            ["@ln"] = Cut(d.LastName, 50), ["@em"] = Cut(d.Email ?? "", 100), ["@ph"] = Cut(d.Phone ?? "", 20),
            ["@cur"] = string.IsNullOrWhiteSpace(d.Currency) ? "TL" : "TL",
            ["@sub"] = d.Subtotal, ["@tax"] = d.Tax, ["@exp"] = d.Expense, ["@disc"] = d.Discount, ["@total"] = d.GrandTotal });

    private Stmt BuildLineInsert(LegacyOrderWriteback d, int orderId, LegacyOrderLine l) => new(
        "INSERT INTO oporderlines (orderId, productVariantId, barcode, sellingPrice, quantity, collectedQuantity, " +
        "discountAmount, productCode, productName, color, variantValue, status, createdDate) " +
        "VALUES (@oid, @vid, @bc, @price, @qty, 0, @disc, @code, @name, @color, @vval, 0, NOW())",
        new() { ["@oid"] = orderId, ["@vid"] = l.LegacyVariantId, ["@bc"] = Cut(l.Barcode, 30),
            ["@price"] = l.UnitPrice, ["@qty"] = l.Quantity, ["@disc"] = l.Discount, ["@code"] = Cut(l.ProductCode, 30),
            ["@name"] = Cut(l.ProductName, 100), ["@color"] = Cut(FirstWord(l.VariantInfo), 30),
            ["@vval"] = Cut(RestWords(l.VariantInfo), 30) });

    private static async Task<int> ExecInsertAsync(MySqlConnection c, MySqlTransaction tx, Stmt s, CancellationToken ct)
    {
        await ExecNonQueryAsync(c, tx, s, ct);
        await using var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", c, tx);
        return Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));
    }

    private static async Task ExecNonQueryAsync(MySqlConnection c, MySqlTransaction tx, Stmt s, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(s.Sql, c, tx);
        foreach (var (k, v) in s.P) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);
    private static string FirstWord(string s) { s = (s ?? "").Trim(); var i = s.IndexOf(' '); return i < 0 ? s : s[..i]; }
    private static string RestWords(string s) { s = (s ?? "").Trim(); var i = s.IndexOf(' '); return i < 0 ? "" : s[(i + 1)..].Trim(); }
}
