using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Finance.Application.Queries.GetSupplierDetail;

public record GetSupplierDetailQuery(Guid SupplierId) : IRequest<Result<SupplierDetailDto>>;

public record SupplierDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? TaxOffice,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactPerson,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt);
