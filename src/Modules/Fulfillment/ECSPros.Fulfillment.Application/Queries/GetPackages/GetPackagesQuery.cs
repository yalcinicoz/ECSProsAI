using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Queries.GetPackages;

public record GetPackagesQuery(Guid? OrderId) : IRequest<Result<List<PackageDto>>>;

public record PackageDto(
    Guid Id,
    Guid OrderId,
    Guid? ShipmentId,
    int PackageNumber,
    string Barcode,
    decimal? Weight,
    decimal? Desi,
    string Status,
    DateTime? PackedAt);
