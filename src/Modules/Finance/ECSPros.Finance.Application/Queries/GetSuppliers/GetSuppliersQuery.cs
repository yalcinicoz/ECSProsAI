using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Finance.Application.Queries.GetSuppliers;

public record GetSuppliersQuery(string? Search = null, bool ActiveOnly = true) : IRequest<Result<List<SupplierDto>>>;

public record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? ContactPerson,
    bool IsActive);
