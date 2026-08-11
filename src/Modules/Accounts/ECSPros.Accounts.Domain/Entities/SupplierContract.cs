using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Accounts.Domain.Entities;

/// <summary>
/// P3a (2026-08-11): pazaryeri satıcı sözleşmesi — cari karta (CurrentAccount, supplier) bağlı
/// TEK kayıt. Hakediş gecikmesi (teslim + X gün, K4: satıcı bazlı), ödeme periyodu, kargo modu
/// (K3 üç mod) ve ciro basamağı dönem tipi burada tutulur. Kargo modu değişince satıcının
/// ApiClient.FulfillmentMode'u senkronlanır (yalnız seller_ships → 'supplier').
/// </summary>
public class SupplierContract : BaseEntity
{
    public Guid CurrentAccountId { get; set; }

    /// <summary>Hakediş uygunlaşma gecikmesi: teslim + X gün (K4 — satıcı bazlı).</summary>
    public int SettlementDelayDays { get; set; } = 14;

    /// <summary>weekly | monthly | immediate — ödeme çıkış periyodu (varsayılan haftalık).</summary>
    public string PayoutPeriod { get; set; } = "weekly";

    /// <summary>K3: platform_contract | seller_ships | seller_contract_we_ship.</summary>
    public string CargoMode { get; set; } = "platform_contract";

    /// <summary>Ciro basamağı dönemi: monthly | yearly | rolling12 (alt-karar: tanımda seçilir).</summary>
    public string TurnoverPeriodType { get; set; } = "monthly";

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<SupplierGroupRate> GroupRates { get; set; } = new List<SupplierGroupRate>();
    public ICollection<SupplierProductRate> ProductRates { get; set; } = new List<SupplierProductRate>();
    public ICollection<SupplierTurnoverTier> TurnoverTiers { get; set; } = new List<SupplierTurnoverTier>();
}

/// <summary>Sözleşmeye özel ürün grubu oranı (K1 katman 3) — grup başına tek satır.</summary>
public class SupplierGroupRate : BaseEntity
{
    public Guid ContractId { get; set; }
    public Guid ProductGroupId { get; set; }
    public decimal RatePercent { get; set; }
    public SupplierContract Contract { get; set; } = null!;
}

/// <summary>Ürün-özel oran (K1 katman 1 — en özel, her şeyi ezer).</summary>
public class SupplierProductRate : BaseEntity
{
    public Guid ContractId { get; set; }
    public Guid ProductId { get; set; }
    public decimal RatePercent { get; set; }
    public SupplierContract Contract { get; set; } = null!;
}

/// <summary>
/// Ciro basamağı (K1 katman 4): dönem cirosu MinTurnover'ı aşınca grup/sözleşme oranına
/// PUAN ayarı uygulanır (negatif = indirim; grup bazlılığı korunur). Yürürlük: sonraki
/// dönem başı (önerilen varsayılan) — çözücü dönemi sözleşmenin TurnoverPeriodType'ından okur.
/// </summary>
public class SupplierTurnoverTier : BaseEntity
{
    public Guid ContractId { get; set; }
    public decimal MinTurnover { get; set; }
    public decimal RateAdjustmentPercent { get; set; }
    public SupplierContract Contract { get; set; } = null!;
}

/// <summary>Platform varsayılan komisyon oranı (K1 katman 5) — ürün grubu başına tek satır;
/// dokümanlarda yayınlanan taban değerlerdir.</summary>
public class CommissionGroupRate : BaseEntity
{
    public Guid ProductGroupId { get; set; }
    public decimal RatePercent { get; set; }
}
