using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Infrastructure.Messaging;
using ECSPros.Storefront.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Satıcıya Soru Sor — "sorunuz cevaplandı" üye e-postası (2026-09-02). Yalnız İLK
/// cevapta gönderilir (cevap güncellemesi yeniden e-posta doğurmaz — çağıran karar verir).
/// Fire-and-forget: kendi scope'unu açar, tüm hataları yutup loglar — moderasyon akışı
/// e-posta hatasıyla ASLA düşmez (StockAlertNotifier sözleşmesiyle aynı).
/// </summary>
public class UrunSoruCevapEpostasi(
    IServiceScopeFactory scopeFactory,
    ILogger<UrunSoruCevapEpostasi> logger)
{
    public void ArkaPlandaGonder(Guid soruId)
    {
        _ = Task.Run(async () =>
        {
            try { await GonderAsync(soruId); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Soru cevabı e-postası gönderilemedi: {SoruId}", soruId);
            }
        });
    }

    private async Task GonderAsync(Guid soruId)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var storefrontDb = sp.GetRequiredService<IStorefrontDbContext>();
        var soru = await storefrontDb.ProductQuestions.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == soruId);
        if (soru?.Answer is not { Length: > 0 }) return;

        var crmDb = sp.GetRequiredService<ICrmDbContext>();
        var eposta = await crmDb.Members.AsNoTracking()
            .Where(m => m.Id == soru.MemberId)
            .Select(m => m.Email)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(eposta)) return;

        var linkBuilder = sp.GetRequiredService<IStoreLinkBuilder>();
        var urunLink = await linkBuilder.BuildAsync(soru.FirmPlatformId, "/urun/" + soru.ProductCode);
        var sorularimLink = await linkBuilder.BuildAsync(soru.FirmPlatformId, "/sorularim");

        var govde = $"""
            <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto;color:#333">
              <h2 style="font-size:18px">Sorunuz cevaplandı 💬</h2>
              <p style="background:#f6f6f6;border-radius:10px;padding:10px 14px"><strong>Sorunuz:</strong> {System.Net.WebUtility.HtmlEncode(soru.Question)}</p>
              <p style="background:#fff7ef;border-radius:10px;padding:10px 14px"><strong>Satıcı cevabı:</strong> {System.Net.WebUtility.HtmlEncode(soru.Answer)}</p>
              {(urunLink is null ? "" : $"""<p><a href="{urunLink}" style="display:inline-block;background:#f27a1a;color:#fff;padding:10px 18px;border-radius:10px;text-decoration:none">Ürüne Git</a></p>""")}
              <p style="font-size:12px;color:#888">Tüm sorularınızı{(sorularimLink is null ? " Hesabım → Sorularım sayfasında" : $""" <a href="{sorularimLink}">Hesabım → Sorularım</a> sayfasında""")} görebilirsiniz.</p>
            </div>
            """;

        var emailService = sp.GetRequiredService<IEmailService>();
        await emailService.SendAsync(eposta, "Ürün sorunuz cevaplandı", govde);
        logger.LogInformation("Soru cevabı e-postası gönderildi: {SoruId} → {Email}", soruId, eposta);
    }
}
