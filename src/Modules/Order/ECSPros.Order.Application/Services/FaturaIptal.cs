using ECSPros.Order.Domain.Entities;

namespace ECSPros.Order.Application.Services;

/// <summary>Fatura iptalinin tek uygulaması — CancelInvoiceCommand ve Teslimatsız İade (İade planı K3:
/// mal müşteriye hiç geçmedi → fatura İPTAL; iade faturası FE5'e bırakıldı) aynı kodu kullanır.</summary>
public static class FaturaIptal
{
    public static bool Uygula(IOrderDbContext db, Invoice invoice, Guid cancelledBy)
    {
        if (invoice.Status == "cancelled") return false;

        invoice.Status = "cancelled";
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = cancelledBy;

        // FE4: entegratöre gitmiş faturanın iptali de entegratöre bildirilir (K9: e-arşiv iptal; e-fatura iade — FE5)
        if (invoice.SendMethod == InvoiceSendMethods.IntegratorApi
            && invoice.IntegratorStatus is "sent" or "accepted")
        {
            invoice.IntegratorStatus = "cancel_queued";
            db.InvoiceDispatches.Add(new InvoiceDispatch
            {
                InvoiceId = invoice.Id, Action = InvoiceDispatchActions.Cancel,
                Status = InvoiceDispatchStatuses.Pending,
                IntegrationContractId = invoice.IntegrationContractId, NextAttemptAt = DateTime.UtcNow,
                CreatedBy = cancelledBy
            });
        }
        return true;
    }
}
