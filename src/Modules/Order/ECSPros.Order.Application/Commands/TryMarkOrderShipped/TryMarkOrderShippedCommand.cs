using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.TryMarkOrderShipped;

/// <summary>
/// OP5: paket kapanışı sonrası otomasyon — siparişin TÜM kalemleri son kontrolü geçtiyse
/// (FinalScanQuantity >= Quantity) sipariş 'shipped'e alınır. OrderShippedEvent'in stok
/// handler'ı yalnız 'reserved' kalan rezervasyonları işler; toplama okutmasında 'picked'
/// yapılanlar atlanır — çifte düşüm olmaz. Kısmi durumda sipariş processing'de kalır (K-17).
/// </summary>
public record TryMarkOrderShippedCommand(Guid OrderId, Guid ActorId) : IRequest<Result<bool>>;

public class TryMarkOrderShippedCommandHandler(IOrderDbContext db, IPublisher publisher)
    : IRequestHandler<TryMarkOrderShippedCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(TryMarkOrderShippedCommand request, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<bool>("Sipariş bulunamadı.");
        if (order.Status == "shipped") return Result.Success(true);

        var hepsiPaketlendi = order.Items.Count > 0
            && order.Items.All(i => i.FinalScanQuantity >= i.Quantity);
        if (!hepsiPaketlendi) return Result.Success(false);

        try
        {
            order.MarkShipped(request.ActorId);
        }
        catch (InvalidOperationException e)
        {
            return Result.Failure<bool>(e.Message);
        }
        await db.SaveChangesAsync(ct);
        foreach (var ev in order.DomainEvents)
            await publisher.Publish(ev, ct);
        order.ClearDomainEvents();
        return Result.Success(true);
    }
}
