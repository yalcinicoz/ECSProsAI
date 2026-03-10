using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CancelOrder;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;
    private readonly IPublisher _publisher;

    public CancelOrderCommandHandler(IOrderDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<bool>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<bool>("Sipariş bulunamadı.");

        try
        {
            order.Cancel(request.CancelledBy, request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in order.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        order.ClearDomainEvents();

        return Result.Success(true);
    }
}
