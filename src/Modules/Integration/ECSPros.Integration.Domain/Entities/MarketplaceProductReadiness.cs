using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Yükleme hazırlık denetimi sonucu (§3) — ürün × pazaryeri düzeyinde materialize edilir
/// (firma geneli; mağaza sayfası kendi kanal-açık adaylarıyla kesiştirir). Türetilmiş veridir,
/// ReadinessService.RecomputeAsync ile her an yeniden üretilebilir. Varyant ekseni özellikler
/// (Beden gibi) denetim DIŞIDIR — varyant verisinden gelir, F4 gönderim payload'ının işidir.
/// </summary>
public class MarketplaceProductReadiness : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Marketplace { get; set; } = string.Empty;
    public Guid? FirmPlatformId { get; set; }

    public string Status { get; set; } = "missing_info";      // ready | missing_info

    /// <summary>Kodlu neden listesi (jsonb): [{code, attr?, detail?}] — kodlar:
    /// no_category_mapping | pool_assignment_pending | rule_no_match | broken_mapping |
    /// required_attr_missing | value_unmapped</summary>
    public string ReasonsJson { get; set; } = "[]";

    public string? ResolvedCategoryExternalId { get; set; }
    public string? ResolvedCategoryPath { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
