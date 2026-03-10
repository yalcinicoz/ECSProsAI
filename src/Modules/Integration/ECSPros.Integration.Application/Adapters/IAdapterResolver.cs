namespace ECSPros.Integration.Application.Adapters;

public interface IAdapterResolver
{
    IMarketplaceAdapter GetMarketplaceAdapter(string serviceCode);
    ICargoAdapter GetCargoAdapter(string serviceCode);
    IEInvoiceAdapter GetEInvoiceAdapter(string serviceCode);
}
