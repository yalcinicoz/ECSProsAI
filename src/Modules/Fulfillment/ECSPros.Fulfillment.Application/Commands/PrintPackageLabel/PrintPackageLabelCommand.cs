using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.PrintPackageLabel;

public record PrintPackageLabelCommand(
    Guid PackageId,
    Guid PrintedBy) : IRequest<Result<bool>>;
