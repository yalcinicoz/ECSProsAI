using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePackage;

public record CreatePackageCommand(
    Guid OrderId,
    Guid? ShipmentId,
    int PackageNumber,
    string Barcode,
    decimal? Weight,
    decimal? Width,
    decimal? Height,
    decimal? Length,
    decimal? Desi,
    Guid PackedBy) : IRequest<Result<Guid>>;
