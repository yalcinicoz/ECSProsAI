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
