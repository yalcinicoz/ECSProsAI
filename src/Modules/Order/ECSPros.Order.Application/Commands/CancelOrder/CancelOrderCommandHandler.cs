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

        // İade planı R4 (2026-09-10): fatura kesilmeden önce yalnız İPTAL vardır; fatura kesildiyse iptal DEĞİL
        // Teslimatsız İade uygulanır. Domain fatura tablosunu bilmez → ön koşul burada.
        if (order.Status == "processing")
        {
            var faturaVar = await _context.Invoices.AsNoTracking()
                .AnyAsync(i => i.OrderId == order.Id && i.Status != "cancelled", cancellationToken);
            if (faturaVar)
                return Result.Failure<bool>("Faturası kesilmiş sipariş iptal edilemez, Teslimatsız İade uygulayın.");
        }

        // K4: toplama planı olan işlemdeki siparişte izin var; toplanmış ürünler rafa geri alınmalı (v1: yalnız uyarı notu).
        var reason = request.Reason;
        if (order.Status == "processing" && order.PickingPlanId.HasValue)
            reason = string.IsNullOrWhiteSpace(reason)
                ? "Toplama planı vardı — toplanmış ürünler rafa geri alınmalı."
                : reason + " (Toplama planı vardı — toplanmış ürünler rafa geri alınmalı.)";

        try
        {
            order.Cancel(request.CancelledBy, reason);
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
