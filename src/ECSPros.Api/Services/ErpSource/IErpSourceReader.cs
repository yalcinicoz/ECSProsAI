namespace ECSPros.Api.Services.ErpSource;

public interface IErpSourceReader
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<ErpProductRow>> ReadProductsAsync(DateTime sinceUtc, CancellationToken ct);
    Task<IReadOnlyList<ErpVariantRow>> ReadVariantsAsync(string productCode, CancellationToken ct);
    Task<IReadOnlyList<ErpProductAttributeRow>> ReadProductAttributesAsync(string productCode, CancellationToken ct);
    Task<ErpProductSnapshot?> ReadProductSnapshotAsync(string productCode, CancellationToken ct);
    Task<string?> ResolveProductCodeByBarcodeAsync(string barcode, CancellationToken ct);
}

/// <summary>Ürün özelliklerini sınırlı bir ürün kodu kümesi için tek sorguda okur.</summary>
public interface IErpProductAttributeBatchReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<ErpProductAttributeRow>>> ReadProductAttributesAsync(
        IReadOnlyCollection<string> productCodes, CancellationToken ct);
}

/// <summary>V3'te ürünlere atanmış tedarikçi tanımlarını toplu ve salt-okunur verir.</summary>
public interface IErpSupplierCatalogReader
{
    Task<IReadOnlyList<ErpSupplierRow>> ReadSuppliersAsync(CancellationToken ct);
    Task<IReadOnlyList<ErpProductSupplierRow>> ReadProductSuppliersAsync(CancellationToken ct);
}
