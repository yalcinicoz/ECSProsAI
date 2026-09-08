namespace ECSPros.Api.Services.ErpSource;

public sealed record ErpProductRow(
    string Code,
    string Name,
    string? InternetName,
    string? ProductGroupName,
    decimal BasePrice,
    decimal? BaseCost,
    int TaxRate,
    bool IsSaleOpen,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyDictionary<string, decimal?> Values)
{
    public string? ProductGroupCode { get; init; }
}

public sealed record ErpVariantAttributeRow(int TypeId, string Value, string? SourceCode = null);

public sealed record ErpVariantRow(
    string Barcode,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<ErpVariantAttributeRow> Attributes);

public sealed record ErpProductAttributeRow(
    string KeywordId,
    string Value,
    string? Key = null,
    string? SourceCode = null)
{
    public bool UseExistingDefinitionOnly { get; init; }
}

public sealed record ErpSupplierRow(string Code, string Name);

public sealed record ErpProductSupplierRow(string ProductCode, string SupplierCode);

public sealed record ErpProductSnapshot(
    ErpProductRow Product,
    IReadOnlyList<ErpVariantRow> Variants,
    IReadOnlyList<ErpProductAttributeRow> Attributes,
    ErpSupplierRow? Supplier);

public sealed record ErpSourceSyncReport(
    bool Success,
    bool DryRun,
    string Slice,
    int Changed,
    string Detail,
    string? Error,
    int DurationMs);
