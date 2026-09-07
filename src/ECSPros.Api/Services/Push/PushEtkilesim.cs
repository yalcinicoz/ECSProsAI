using ECSPros.Order.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Push;

/// <summary>Etkileşim/iade push'ları (§4.4, §4.1 return_status) — panel eylemlerinden çağrılır; hata push'ta kalır.</summary>
public sealed class PushEtkilesim(IStorefrontDbContext sdb, IOrderDbContext odb, IProductService urun, PushKuyruk kuyruk, Npgsql.NpgsqlDataSource ds, ILogger<PushEtkilesim> logger)
{
    async Task<(string Name, string? Image)> UrunAsync(string code, CancellationToken ct)
    {
        try
        {
            await using var c = await ds.OpenConnectionAsync(ct);
            await using var cmd = new Npgsql.NpgsqlCommand("""
                SELECT COALESCE(p."NameI18n"->>'tr',''), (SELECT v."Id" FROM catalog.product_variants v WHERE v."ProductId"=p."Id" AND NOT v."IsDeleted" ORDER BY v."CreatedAt" LIMIT 1)
                  FROM catalog.products p WHERE p."Code"=@c AND NOT p."IsDeleted"
                """, c);
            cmd.Parameters.AddWithValue("c", code);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return (code, null);
            var name = r.GetString(0); Guid? vid = r.IsDBNull(1) ? null : r.GetGuid(1);
            if (vid is { } v)
            {
                var d = (await urun.GetVariantDisplayAsync([v], ct)).GetValueOrDefault(v);
                if (d is not null) return (name.Length > 0 ? name : d.ProductNameI18n.GetValueOrDefault("tr") ?? code, d.ImageUrl);
            }
            return (name.Length > 0 ? name : code, null);
        }
        catch { return (code, null); }
    }

    public async Task SoruCevaplandiAsync(Guid questionId, CancellationToken ct)
    {
        try
        {
            var q = await sdb.ProductQuestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == questionId, ct);
            if (q is null) return;
            var (name, img) = await UrunAsync(q.ProductCode, ct);
            await kuyruk.EnqueueAsync(new PushIstek("question_answered", q.MemberId, new Dictionary<string, string> { ["productName"] = name, ["productCode"] = q.ProductCode }, $"question_answered:{q.Id}", q.FirmPlatformId, ImageUrl: img), ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "question_answered push kuyruğa alınamadı"); }
    }

    public async Task YorumModereEdildiAsync(Guid reviewId, bool approved, CancellationToken ct)
    {
        try
        {
            var r = await sdb.ProductReviews.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reviewId, ct);
            if (r is null) return;
            var (name, img) = await UrunAsync(r.ProductCode, ct);
            await kuyruk.EnqueueAsync(new PushIstek(approved ? "review_approved" : "review_rejected", r.MemberId, new Dictionary<string, string> { ["productName"] = name, ["productCode"] = r.ProductCode }, $"review_{(approved ? "approved" : "rejected")}:{r.Id}", r.FirmPlatformId, ImageUrl: img), ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "review push kuyruğa alınamadı"); }
    }

    public async Task IadeDurumuAsync(Guid returnId, string durumEtiketi, string durumKodu, CancellationToken ct)
    {
        try
        {
            var ret = await odb.Returns.AsNoTracking().Where(x => x.Id == returnId).Select(x => new { x.OrderId, x.MemberId }).FirstOrDefaultAsync(ct);
            if (ret is null) return;
            var o = await odb.Orders.AsNoTracking().Where(x => x.Id == ret.OrderId).Select(x => new { x.OrderNumber, x.FirmPlatformId }).FirstOrDefaultAsync(ct);
            await kuyruk.EnqueueAsync(new PushIstek("return_status", ret.MemberId, new Dictionary<string, string> { ["orderNumber"] = o?.OrderNumber ?? "", ["returnStatusLabel"] = durumEtiketi }, $"return_status:{returnId}:{durumKodu}", o?.FirmPlatformId), ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "return_status push kuyruğa alınamadı"); }
    }
}
