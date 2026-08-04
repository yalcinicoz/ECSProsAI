using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetOrders;

public record GetOrdersQuery(
    string? Status = null,
    Guid? MemberId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    List<string>? Statuses = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    Guid? FirmPlatformId = null,
    string? PaymentMethod = null) : IRequest<Result<PagedOrderResult>>;
    // PaymentMethod (2026-08-04): kart | kapida-nakit | kapida-kart | none (= yöntemi olmayan eski kayıtlar)

public record PagedOrderResult(List<OrderListDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record OrderListDto(
    Guid Id,
    string OrderNumber,
    Guid? MemberId,
    string Status,
    string PaymentStatus,
    decimal GrandTotal,
    string CurrencyCode,
    DateTime CreatedAt,
    string? RecipientName = null,
    string? PaymentMethod = null);   // 2026-08-04: kart | kapida-nakit | kapida-kart | null
