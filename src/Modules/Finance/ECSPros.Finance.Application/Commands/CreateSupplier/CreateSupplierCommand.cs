using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Finance.Application.Commands.CreateSupplier;

public record CreateSupplierCommand(
    string Code,
    string Name,
    string? TaxOffice,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Notes) : IRequest<Result<Guid>>;
