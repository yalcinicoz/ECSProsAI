using ECSPros.Integration.Application.Adapters;

namespace ECSPros.Integration.Infrastructure.Adapters;

public class AdapterResolver(
    IEnumerable<IMarketplaceAdapter> marketplaceAdapters,
    IEnumerable<ICargoAdapter> cargoAdapters,
    IEnumerable<IEInvoiceAdapter> eInvoiceAdapters) : IAdapterResolver
{
    public IMarketplaceAdapter GetMarketplaceAdapter(string serviceCode)
    {
        var adapter = marketplaceAdapters.FirstOrDefault(a => a.ServiceCode == serviceCode);
        return adapter ?? throw new InvalidOperationException($"Pazaryeri adaptörü bulunamadı: {serviceCode}");
    }

    public ICargoAdapter GetCargoAdapter(string serviceCode)
    {
        var adapter = cargoAdapters.FirstOrDefault(a => a.ServiceCode == serviceCode);
        return adapter ?? throw new InvalidOperationException($"Kargo adaptörü bulunamadı: {serviceCode}");
    }

    public IEInvoiceAdapter GetEInvoiceAdapter(string serviceCode)
    {
        var adapter = eInvoiceAdapters.FirstOrDefault(a => a.ServiceCode == serviceCode);
        return adapter ?? throw new InvalidOperationException($"e-Fatura adaptörü bulunamadı: {serviceCode}");
    }
}
