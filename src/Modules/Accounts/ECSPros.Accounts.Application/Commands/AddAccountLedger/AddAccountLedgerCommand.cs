using ECSPros.Shared.Kernel.Common;
using MediatR;
namespace ECSPros.Accounts.Application.Commands.AddAccountLedger;
public record AddAccountLedgerCommand(
    Guid CurrentAccountId,
    string Currency,
    string? Description
) : IRequest<Result<Guid>>;
