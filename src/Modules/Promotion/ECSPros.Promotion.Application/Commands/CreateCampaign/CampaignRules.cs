using ECSPros.Promotion.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.CreateCampaign;

/// <summary>Kampanya kayıt kuralları (oluşturma + güncelleme ortak): ad zorunlu, kod sunucuda üretilir.</summary>
public static class CampaignRules
{
    /// <summary>En az bir dilde boş olmayan ad girilmiş olmalı (panel yalnız "tr" gönderir).</summary>
    public static bool AdGecerli(Dictionary<string, string>? nameI18n) =>
        nameI18n is not null && nameI18n.Values.Any(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Panelde kod alanı yok (2026-09-11): KMP-yyyyMMdd-XXXX (4 harf/rakam), çakışırsa yeniden denenir.
    /// Silinmiş kampanyalar da sayılır (soft delete filtresi aşılır) — kod geçmişte tek kalsın.</summary>
    public static async Task<string> KodUretAsync(IPromotionDbContext db, CancellationToken ct)
    {
        const string alfabe = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // karışan karakterler (0/O, 1/I) yok
        var gun = DateTime.UtcNow.ToString("yyyyMMdd");
        for (var deneme = 0; deneme < 20; deneme++)
        {
            var ek = new string(Enumerable.Range(0, 4).Select(_ => alfabe[Random.Shared.Next(alfabe.Length)]).ToArray());
            var kod = $"KMP-{gun}-{ek}";
            if (!await db.Campaigns.IgnoreQueryFilters().AnyAsync(c => c.Code == kod, ct))
                return kod;
        }
        return $"KMP-{gun}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}
