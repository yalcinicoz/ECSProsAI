using System.Text.Json;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.SubmitPartnerProduct;

/// <summary>Partner façade `POST /products` — Kapı 1 (otomatik doğrulama) + pending submission
/// oluştur/güncelle. Sistem hatası → Result.Failure; doğrulama sonucu (kabul/red + gerekçeler)
/// → Result.Success içindeki SubmitPartnerProductResult (§3.8).</summary>
public record SubmitPartnerProductCommand(
    Guid SupplierId,
    Guid? ApiClientId,
    bool CanSetPrice,          // pricing.write scope'u var mı (Tip 2) → price zorunlu; yoksa yoksayılır
    PartnerProductBody Body,
    string RawJson) : IRequest<Result<SubmitPartnerProductResult>>;

public record SubmitPartnerProductResult(
    bool Accepted,
    Guid? SubmissionId,
    string? SupplierProductCode,
    string? Status,
    List<PartnerValidationError> Errors);

public record PartnerValidationError(string Field, string Code, string Message);

// ── İstek gövdesi (§3.8) ────────────────────────────────────────────
public record PartnerProductBody(
    string? SupplierProductCode,
    string? Group,
    Dictionary<string, string>? Name,
    Dictionary<string, string>? ShortDescription,
    Dictionary<string, string>? Description,
    Dictionary<string, JsonElement>? Attributes,   // değer: string VEYA dizi (çoklu değer)
    List<PartnerVariantBody>? Variants,
    List<PartnerImageBody>? Images);

public record PartnerVariantBody(
    Dictionary<string, string>? AxisValues,
    string? Sku,
    string? Barcode,
    int? Stock,
    PartnerPriceBody? Price);

public record PartnerPriceBody(decimal? Amount, string? Currency);

public record PartnerImageBody(string? Url, string? VariantRef, bool? Main);
