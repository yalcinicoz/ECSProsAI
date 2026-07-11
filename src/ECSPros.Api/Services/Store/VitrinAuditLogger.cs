using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Api.Services.Store;

public interface IVitrinAuditLogger
{
    /// <summary>Vitrin admin işlemini iam.audit_logs'a yazar (spec: kim/neyi/ne zaman +
    /// eski-yeni değer + IP/UA). Hata admin işlemini DÜŞÜRMEZ — loglanır, yutulur.</summary>
    Task LogAsync(
        HttpContext http, string action, string entityType, Guid entityId,
        object? oldValues, object? newValues, Guid firmPlatformId,
        string? title = null, CancellationToken ct = default);
}

/// <summary>
/// G13: vitrin değişiklik geçmişi — IAM'ın audit_logs tablosuna yazar (Faz 4a'dan beri
/// tablo+listeleme vardı, İLK yazan burası). ActionType/EntityType adları spec'ten:
/// Created/Updated/Deleted/Activated/Deactivated/Published/Rollback/Previewed;
/// BannerBlock/Slide/TabItem/Rule/PublishedSnapshot... Context jsonb'si platform kimliği,
/// kullanıcı adı ve blok başlığını taşır — GET /api/pages/audit-logs bunlarla süzer.
/// </summary>
public class VitrinAuditLogger(IIamDbContext db, ILogger<VitrinAuditLogger> logger) : IVitrinAuditLogger
{
    /// <summary>Spec EntityType eşlemesi — blok tipi → audit varlık adı.</summary>
    public static string BlockEntityType(string blockType) => blockType switch
    {
        "banner" => "BannerBlock",
        "slider" => "SliderBlock",
        "story" => "StoryBannerBlock",
        "carousel" => "CarouselProductBlock",
        "infinity" => "InfinityProductBlock",
        "tabs" => "TabsBlock",
        "collection" => "CollectionBlock",
        "categories" => "CategoriesBlock",
        "brands" => "BrandsBlock",
        "instagram" => "InstagramBlock",
        "announcement" => "AnnouncementBlock",
        _ => "Block",
    };

    /// <summary>Öğe varlık adı (spec: Slide/StoryItem/TabItem; diğer tiplerde genel ad).</summary>
    public static string ItemEntityType(string blockType) => blockType switch
    {
        "slider" => "Slide",
        "story" => "StoryItem",
        "tabs" => "TabItem",
        _ => "BlockItem",
    };

    /// <summary>Vitrin varlık adları — audit listesi bu kümeyle süzülür.</summary>
    public static readonly string[] EntityTypes =
    [
        "BannerBlock", "SliderBlock", "StoryBannerBlock", "CarouselProductBlock",
        "InfinityProductBlock", "TabsBlock", "CollectionBlock", "CategoriesBlock",
        "BrandsBlock", "InstagramBlock", "AnnouncementBlock", "Block",
        "Slide", "StoryItem", "TabItem", "BlockItem", "Rule", "PublishedSnapshot", "PagePlacement",
    ];

    public async Task LogAsync(
        HttpContext http, string action, string entityType, Guid entityId,
        object? oldValues, object? newValues, Guid firmPlatformId,
        string? title = null, CancellationToken ct = default)
    {
        try
        {
            var context = new Dictionary<string, object>
            {
                ["firmPlatformId"] = firmPlatformId.ToString(),
                ["userName"] = http.User.FindFirst("full_name")?.Value
                    ?? http.User.Identity?.Name ?? "",
            };
            if (title is not null) context["title"] = title;

            db.AuditLogs.Add(new AuditLog
            {
                UserId = Guid.TryParse(
                    http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? http.User.FindFirst("sub")?.Value, out var uid) ? uid : null,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                OldValues = Sozluk(oldValues),
                NewValues = Sozluk(newValues),
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
                    ? ua[..Math.Min(ua.Length, 500)] : null,
                Context = context,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vitrin audit yazılamadı: {Action} {EntityType} {EntityId}",
                action, entityType, entityId);
        }
    }

    /// <summary>Nesneyi audit sözlüğüne çevirir ("data" anahtarı altında JSON değer —
    /// eski/yeni karşılaştırma paneli düz JSON gösterir).</summary>
    private static Dictionary<string, object>? Sozluk(object? deger)
    {
        if (deger is null) return null;
        if (deger is Dictionary<string, object> d) return d;
        return new Dictionary<string, object>
        {
            ["data"] = System.Text.Json.JsonSerializer.SerializeToElement(deger,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
        };
    }
}
