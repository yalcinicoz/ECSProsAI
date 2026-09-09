using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string Module { get; set; } = string.Empty;
    public string PermissionType { get; set; } = string.Empty; // read, create, update, delete, manage (eski alan)
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // ── Kod sahipli katalog alanları (2026-09-09, Y0 / karar K4) ─────────────────
    /// <summary>page | action | field — tasarım §B.2. Katalogdan gelir, panel değiştirmez.</summary>
    public string Kind { get; set; } = "action";

    /// <summary>Modül içindeki sayfa/ekran grubu (panelde gruplama). Katalog varsayılanı,
    /// panel düzenleyebilir.</summary>
    public string? PageCode { get; set; }

    /// <summary>Yetki satış kanalı bazında mı veriliyor (K1 kapsam birimi = kanal)?
    /// false ise panelde kanal seçici görünmez, kapsam yok sayılır.</summary>
    public bool ChannelScoped { get; set; } = false;

    /// <summary>Kanal-kapsamlı bayrağını PANEL değiştirdi mi? false iken katalog (kod) değeri
    /// senkronda yazılır; panel değiştirince true olur ve kod bir daha ezmez (K4: kod sahipli
    /// varsayılan + panel son söz). Heuristik yok — açık bayrak.</summary>
    public bool ChannelScopedOverridden { get; set; } = false;

    /// <summary>Kodda karşılığı var mı? Senkronda katalogda bulunmayan kayıt false'a düşer ve
    /// pasife alınır; kayıtlar ve audit geçmişi korunur (tasarım §M.4).</summary>
    public bool IsCodeDefined { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
