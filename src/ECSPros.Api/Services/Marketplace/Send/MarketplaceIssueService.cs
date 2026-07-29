using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace.Send;

/// <summary>
/// Sorun kuyruğu servis yüzeyi (§6). Kritik kural: otomatik kapanma — koşulu üreten tarama
/// (mutabakat/worker) her koşuda gördüğü koşulları upsert eder, kendi sahip olduğu tiplerin
/// artık görülmeyen açık kayıtlarını resolved yapar. Aynı koşul için açık duplicate açılmaz
/// (ConditionKey). "Yoksay" personel kararıdır; koşul sürerse bir sonraki taramada yeniden
/// açılır (bilinçli — yoksayılan sorun sessizce kaybolmaz).
/// </summary>
public sealed class MarketplaceIssueService(IIntegrationDbContext db)
{
    /// <summary>Koşulu upsert eder: açık kayıt varsa tazeler, yoksa açar. SaveChanges çağıran işin.</summary>
    public async Task UpsertOpenAsync(
        string marketplace, Guid firmPlatformId, string issueType, string conditionKey,
        string title, string? detail = null, string? suggestedAction = null,
        string? referenceType = null, Guid? referenceId = null, CancellationToken ct = default)
    {
        var existing = await db.MarketplaceIssues.FirstOrDefaultAsync(
            i => i.FirmPlatformId == firmPlatformId && i.ConditionKey == conditionKey && i.Status == "open", ct);
        if (existing is null)
        {
            db.MarketplaceIssues.Add(new MarketplaceIssue
            {
                Marketplace = marketplace,
                FirmPlatformId = firmPlatformId,
                IssueType = issueType,
                ConditionKey = conditionKey,
                Title = title,
                Detail = detail,
                SuggestedAction = suggestedAction,
                ReferenceType = referenceType,
                ReferenceId = referenceId
            });
        }
        else
        {
            existing.Title = title;
            existing.Detail = detail;
            existing.SuggestedAction = suggestedAction;
            existing.LastSeenAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Sahip olunan tiplerin bu taramada GÖRÜLMEYEN açık kayıtlarını otomatik kapatır.</summary>
    public async Task<int> ResolveStaleAsync(
        Guid firmPlatformId, IReadOnlyCollection<string> ownedTypes,
        IReadOnlySet<string> seenConditionKeys, CancellationToken ct = default)
    {
        var open = await db.MarketplaceIssues
            .Where(i => i.FirmPlatformId == firmPlatformId && i.Status == "open"
                        && ownedTypes.Contains(i.IssueType))
            .ToListAsync(ct);
        var resolved = 0;
        foreach (var issue in open.Where(i => !seenConditionKeys.Contains(i.ConditionKey)))
        {
            issue.Status = "resolved";
            issue.ResolvedAt = DateTime.UtcNow;
            issue.UpdatedAt = DateTime.UtcNow;
            resolved++;
        }
        return resolved;
    }

    public async Task<bool> DismissAsync(Guid id, Guid? userId, CancellationToken ct)
    {
        var issue = await db.MarketplaceIssues.FirstOrDefaultAsync(i => i.Id == id && i.Status == "open", ct);
        if (issue is null) return false;
        issue.Status = "dismissed";
        issue.ResolvedAt = DateTime.UtcNow;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.UpdatedBy = userId;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
