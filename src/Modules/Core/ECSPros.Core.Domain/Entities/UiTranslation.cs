using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

/// <summary>
/// Admin paneli ve frontend arayüzünde kullanılan statik metinlerin çevirileri.
/// Namespace → Key → Lang → Value hiyerarşisi ile organize edilir.
/// </summary>
public class UiTranslation : BaseEntity
{
    /// <summary>Grup adı (common, catalog, orders, inventory, crm, pos …)</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Çeviri anahtarı (save, cancel, product_name …)</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Dil kodu (tr, en …)</summary>
    public string Lang { get; set; } = string.Empty;

    /// <summary>Çeviri değeri</summary>
    public string Value { get; set; } = string.Empty;
}
