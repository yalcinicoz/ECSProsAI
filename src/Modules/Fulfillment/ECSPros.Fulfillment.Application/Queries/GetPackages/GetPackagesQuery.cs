using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Queries.GetPackages;

public record GetPackagesQuery(Guid? OrderId) : IRequest<Result<List<PackageDto>>>;

public record PackageDto(
    Guid Id,
    Guid OrderId,
    Guid FirmPlatformId,
    Guid? ShipmentId,
    string PackageNumber,
    int SequenceInOrder,
    Guid? SupplierId,
    string Barcode,
    string? CargoIntegrationCode,
    string? CargoIntegrationCodeSource,
    decimal? Weight,
    decimal? Desi,
    string Status,
    DateTime? PackedAt,
    DateTime? LabelPrintedAt,
    List<PackageItemDto> Items);

public record PackageItemDto(
    Guid Id,
    Guid OrderItemId,
    Guid VariantId,
    int Quantity);
