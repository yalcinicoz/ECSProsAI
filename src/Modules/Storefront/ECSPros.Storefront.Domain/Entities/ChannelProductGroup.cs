using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Katman 2 karar kaydı: hangi ürün grubunun hangi kanalda satılacağı.
/// Bu kaydın varlığı "satış kararı verildi" anlamına gelir.
/// </summary>
public class ChannelProductGroup : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid ProductGroupId { get; set; }

    /// <summary>draft | active | inactive</summary>
    public string Status { get; set; } = "draft";
}
