using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Queries.GetVariantByBarcode;

public record GetVariantByBarcodeQuery(string Barcode) : IRequest<Result<VariantByBarcodeDto>>;

public record VariantByBarcodeDto(
    Guid ProductId,
    Guid VariantId,
    string Sku,
    string ProductCode,
    Dictionary<string, string> ProductNameI18n);
