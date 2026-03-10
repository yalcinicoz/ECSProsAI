using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.MarkShipped;

public record MarkShippedCommand(
    Guid OrderId,
    Guid? FirmIntegrationId,
    string? TrackingNumber,
    int PackageCount,
    Guid UpdatedBy) : IRequest<Result<Guid>>;
