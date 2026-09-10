namespace ECSPros.Api.Services.Inventory;

/// <summary>
/// Stok otoritesi (raf ekranları kurgusu §0, 2026-09-10). Canlı <c>Legacy:Sync</c> fiyat/stok dilimi açıkken göz bazlı
/// adetler eski sistemden yeniden yazılır → panelde yapılan raf hareketi ezilir. Bu yüzden yazan raf uçları
/// otorite eskideyken 409 döner; ekranlar "aynalama" kipinde yalnız okur. Go-live'da <c>Legacy:Sync:Stock=false</c>
/// (ya da Sync kapalı) → otorite panel.
/// </summary>
public sealed class StockAuthority(IConfiguration config)
{
    public const string Legacy = "legacy", Panel = "panel";

    public bool LegacyOwnsStock =>
        config.GetValue("Legacy:Sync:Enabled", false) && config.GetValue("Legacy:Sync:Stock", true)
        && !config.GetValue("Legacy:Sync:DryRun", false);

    public string Current => LegacyOwnsStock ? Legacy : Panel;

    public const string LegacyMessage =
        "Stok otoritesi eski sistemde (Legacy:Sync:Stock açık) — raf hareketleri eski panelden yapılır; buradaki değişiklik senkronla ezilirdi.";
}
