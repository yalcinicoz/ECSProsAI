namespace ECSPros.Api.Services.Marketplace.Reference;

/// <summary>
/// Pazaryeri referans verisi indirici — pazaryeri başına bir gerçekleme.
/// Kategori ağacını düz liste olarak döner (path/is_leaf hesaplamasını senkron
/// motoru yapar); özellik+değerler kategori başına ayrı çağrıdır (Trendyol/HB modeli).
/// </summary>
public interface IMarketplaceReferenceDownloader
{
    /// <summary>IntegrationService.Code ile eşleşir (trendyol, hepsiburada, ...).</summary>
    string ServiceCode { get; }

    /// <summary>Ardışık kategori-özellik istekleri arasındaki bekleme (kota koruması).</summary>
    TimeSpan AttributeRequestDelay { get; }

    Task<List<RefCategoryDto>> DownloadCategoriesAsync(CancellationToken ct = default);

    Task<List<RefAttributeDto>> DownloadCategoryAttributesAsync(string categoryExternalId, CancellationToken ct = default);
}

public sealed record RefCategoryDto(
    string ExternalId,
    string? ParentExternalId,
    string Name,
    string RawJson);

public sealed record RefAttributeDto(
    string ExternalId,
    string? Code,
    string Name,
    bool IsRequired,
    bool AllowCustom,
    bool IsVariantAxis,
    bool IsMultiValue,
    string ValueMode,          // id | code | literal — gönderimde değerin hangi kimliği beklenir
    string RawJson,
    List<RefAttributeValueDto> Values);

public sealed record RefAttributeValueDto(
    string? ExternalId,
    string? Code,
    string Value);
