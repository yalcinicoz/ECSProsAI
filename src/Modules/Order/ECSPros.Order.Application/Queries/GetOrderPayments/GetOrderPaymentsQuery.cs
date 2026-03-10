using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderPayments;

public record GetOrderPaymentsQuery(Guid OrderId) : IRequest<Result<List<OrderPaymentDto>>>;

public record OrderPaymentDto(
    Guid Id,
    Guid OrderId,
    Guid PaymentMethodId,
    decimal Amount,
    string CurrencyCode,
    string Status,
    DateTime CreatedAt);

public class GetOrderPaymentsQueryHandler : IRequestHandler<GetOrderPaymentsQuery, Result<List<OrderPaymentDto>>>
{
    private readonly IOrderDbContext _db;

    public GetOrderPaymentsQueryHandler(IOrderDbContext db) => _db = db;

    public async Task<Result<List<OrderPaymentDto>>> Handle(GetOrderPaymentsQuery request, CancellationToken ct)
    {
        var items = await _db.OrderPayments
            .Where(p => p.OrderId == request.OrderId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new OrderPaymentDto(
                p.Id, p.OrderId, p.PaymentMethodId,
                p.Amount, p.CurrencyCode, p.Status, p.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
