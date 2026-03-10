using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Queries.GetPosSales;

public record GetPosSalesQuery(
    Guid? SessionId = null,
    Guid? RegisterId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PosSaleListDto>>>;

public record PosSaleListDto(
    Guid Id,
    string SaleNumber,
    Guid SessionId,
    Guid RegisterId,
    Guid? MemberId,
    string Status,
    decimal GrandTotal,
    DateTime CreatedAt);
