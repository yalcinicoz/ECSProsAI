using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetOrders;

public record GetOrdersQuery(
    string? Status = null,
    Guid? MemberId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedOrderResult>>;

public record PagedOrderResult(List<OrderListDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record OrderListDto(
    Guid Id,
    string OrderNumber,
    Guid MemberId,
    string Status,
    string PaymentStatus,
    decimal GrandTotal,
    string CurrencyCode,
    bool IsPosSale,
    DateTime CreatedAt);
