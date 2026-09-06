using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>
/// Kanal yuvası → seri bağı (FE0 §2.3). (Kanal, tip) tekildir; bağlanan serinin tipi yuva tipiyle
/// AYNI olmak zorundadır (komut doğrular). Aynı seri birden fazla kanalda kullanılabilir; seri
/// pasife alınırken bu bağlar yerine-geçecek seriye tek işlemde taşınır (DeactivateInvoiceSeries).
/// </summary>
public class ChannelInvoiceSeriesBinding : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    public Guid InvoiceSeriesId { get; set; }

    public InvoiceSeries InvoiceSeries { get; set; } = null!;
}
