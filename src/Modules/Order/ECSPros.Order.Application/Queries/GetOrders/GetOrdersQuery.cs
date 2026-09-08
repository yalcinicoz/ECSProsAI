using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
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
    string? PaymentMethod = null,
    bool? PaymentCollected = null,
    GridRequest? Grid = null) : IRequest<Result<PagedOrderResult>>;
    // Grid (2026-09-08, DataGrid F0): beyaz listeli f.* filtreleri + sort/dir (OrderGrid.Schema); null → eski davranış
    // PaymentMethod (2026-08-04): kart | kapida-nakit | kapida-kart | none (= yöntemi olmayan eski kayıtlar)
    // PaymentCollected (2026-08-04): true = ödemesi alınan (paid), false = alınmayan (paid dışı)

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
