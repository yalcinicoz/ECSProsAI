using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>
/// Satış kanalının faturalama ayarı (FE0 §2.3). Gönderim yöntemi kanala özeldir:
/// manual (yalnız kayıt) | integrator_api | erp | marketplace — bkz. <see cref="InvoiceSendMethods"/>.
/// Kanal (core_firm_platforms) gevşek referanstır; satır yoksa yöntem "manual" kabul edilir.
/// </summary>
public class ChannelInvoiceSettings : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string SendMethod { get; set; } = InvoiceSendMethods.Manual;
}
