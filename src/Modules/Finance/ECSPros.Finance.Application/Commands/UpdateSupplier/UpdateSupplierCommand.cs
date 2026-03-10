using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Finance.Application.Commands.UpdateSupplier;

public record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string? TaxOffice,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Notes,
    bool IsActive,
    Guid UpdatedBy) : IRequest<Result<bool>>;
