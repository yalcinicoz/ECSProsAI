using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.UpdateBinStatus;

public record UpdateBinStatusCommand(
    Guid BinId,
    string Status,
    Guid UpdatedBy) : IRequest<Result<bool>>;
