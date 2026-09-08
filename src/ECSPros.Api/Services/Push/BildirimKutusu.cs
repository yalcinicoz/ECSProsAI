using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Push;

/// <summary>
/// Uygulama içi "Bildirimlerim" (docs/BILDIRIMLERIM_BACKEND_ISTEGI.md, 2026-09-08): listenin ve durumların tek sahibi backend.
/// Görünür satır = üyenin, Inbox=true, silinmemiş (DismissedAt null), süresi dolmamış. Aynı olay üyenin birden çok cihazına
/// gittiğinde (dedupId aynı) listede TEK satır görünür; okuma/silme aynı dedupId'li tüm satırlara uygulanır.
/// Controller yalnız kimlik/zarf işini yapar; testler bu sınıfı doğrudan kullanır.
/// </summary>
public sealed class BildirimKutusu(IStorefrontDbContext sdb)
{
    public const int MaxPageSize = 50;
    public static readonly string[] Ikonlar = { "order", "cargo", "payment", "return", "favorite", "stock", "cart", "question", "review", "coupon", "campaign", "account", "info" };

    public sealed record Satir(Guid Id, string DedupId, string Type, string Class, string Title, string Body, string Link, string? ImageUrl,
        string Icon, DateTime CreatedAt, DateTime? ReadAt, bool DismissOnOpen);
    public sealed record Sayfa(IReadOnlyList<Satir> Items, int UnreadCount, int TotalCount, int Page, int PageSize, int TotalPages, bool HasNextPage);

    IQueryable<PushNotification> Gorunur(Guid memberId, Guid? firmPlatformId, DateTime now)
    {
        var q = sdb.PushNotifications.AsNoTracking()
            .Where(n => n.MemberId == memberId && n.Inbox && n.DismissedAt == null && n.ExpiresAt > now);
        if (firmPlatformId is { } fp) q = q.Where(n => n.FirmPlatformId == fp);
        // aynı dedupId'nin yalnız en yeni satırı (çok cihazlı üye)
        return q.Where(n => !sdb.PushNotifications.Any(o => o.MemberId == memberId && o.Inbox && o.DismissedAt == null && o.DedupId == n.DedupId
            && (o.CreatedAt > n.CreatedAt || (o.CreatedAt == n.CreatedAt && o.Id.CompareTo(n.Id) > 0))));
    }

    public async Task<Sayfa> ListeAsync(Guid memberId, Guid? firmPlatformId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var now = DateTime.UtcNow;
        var q = Gorunur(memberId, firmPlatformId, now);
        var total = await q.CountAsync(ct);
        var unread = await q.CountAsync(n => n.ReadAt == null, ct);
        var items = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new Satir(n.Id, n.DedupId, n.Type, n.Class, n.Title, n.Body, n.Link, n.ImageUrl, n.Icon, n.CreatedAt, n.ReadAt, n.DismissOnOpen))
            .ToListAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new Sayfa(items, unread, total, page, pageSize, pages, page < pages);
    }

    public Task<int> OkunmamisAsync(Guid memberId, Guid? firmPlatformId, CancellationToken ct)
        => Gorunur(memberId, firmPlatformId, DateTime.UtcNow).CountAsync(n => n.ReadAt == null, ct);

    /// <summary>Satırı (ve aynı dedupId'li kardeşlerini) okundu yapar; dismissOnOpen ise listeden düşürür. Yok/başkasının → false (404).</summary>
    public async Task<bool> OkunduAsync(Guid memberId, Guid id, CancellationToken ct)
    {
        var rows = await KardeslerAsync(memberId, id, ct);
        if (rows.Count == 0) return false;
        var now = DateTime.UtcNow;
        foreach (var n in rows) { n.ReadAt ??= now; if (n.DismissOnOpen) n.DismissedAt ??= now; }
        await sdb.SaveChangesAsync(ct);
        return true;
    }

    public async Task TumunuOkunduAsync(Guid memberId, Guid? firmPlatformId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = sdb.PushNotifications.Where(n => n.MemberId == memberId && n.Inbox && n.DismissedAt == null && n.ExpiresAt > now && n.ReadAt == null);
        if (firmPlatformId is { } fp) q = q.Where(n => n.FirmPlatformId == fp);
        foreach (var n in await q.ToListAsync(ct)) n.ReadAt = now;
        await sdb.SaveChangesAsync(ct);
    }

    /// <summary>Kullanıcı silmesi (geri alma yok). Yok/başkasının → false (404); zaten silinmiş → true (idempotent).</summary>
    public async Task<bool> SilAsync(Guid memberId, Guid id, CancellationToken ct)
    {
        var rows = await KardeslerAsync(memberId, id, ct, silinmisDahil: true);
        if (rows.Count == 0) return false;
        var now = DateTime.UtcNow;
        foreach (var n in rows) n.DismissedAt ??= now;
        await sdb.SaveChangesAsync(ct);
        return true;
    }

    public async Task TumunuSilAsync(Guid memberId, Guid? firmPlatformId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = sdb.PushNotifications.Where(n => n.MemberId == memberId && n.Inbox && n.DismissedAt == null && n.ExpiresAt > now);
        if (firmPlatformId is { } fp) q = q.Where(n => n.FirmPlatformId == fp);
        foreach (var n in await q.ToListAsync(ct)) n.DismissedAt = now;
        await sdb.SaveChangesAsync(ct);
    }

    async Task<List<PushNotification>> KardeslerAsync(Guid memberId, Guid id, CancellationToken ct, bool silinmisDahil = false)
    {
        var dedup = await sdb.PushNotifications.AsNoTracking().Where(n => n.Id == id && n.MemberId == memberId).Select(n => n.DedupId).FirstOrDefaultAsync(ct);
        if (dedup is null) return [];
        var q = sdb.PushNotifications.Where(n => n.MemberId == memberId && n.DedupId == dedup);
        if (!silinmisDahil) q = q.Where(n => n.DismissedAt == null);
        var rows = await q.ToListAsync(ct);
        // silinmiş satıra tekrar 'okundu': idempotent 200 → kardeş yoksa satırın kendisi
        return rows.Count > 0 ? rows : await sdb.PushNotifications.Where(n => n.Id == id).ToListAsync(ct);
    }

    /// <summary>§5: tür → varsayılan ikon (şablon tanımlı değilse ya da serbest metinli deneme gönderiminde).</summary>
    public static string IkonVarsayilan(string type) => type switch
    {
        "order_created" or "order_confirmed" or "order_cancelled" => "order",
        "order_shipped" or "order_delivered" => "cargo",
        "order_payment_pending" or "payment_pending" or "wallet_credit" => "payment",
        "return_status" => "return",
        "favorite_price_drop" or "favorite_back_in_stock" or "favorite_low_stock" => "favorite",
        "stock_alert" => "stock",
        "cart_reminder" or "cart_price_drop" => "cart",
        "question_answered" => "question",
        "order_review_invite" or "review_invite" or "review_approved" or "review_rejected" => "review",
        "coupon_assigned" or "coupon_expiring" => "coupon",
        "campaign" or "welcome" or "winback" or "viewed_reminder" => "campaign",
        _ => "info",
    };
}
