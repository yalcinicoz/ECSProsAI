using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Order.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.ReceiveReturn;

public class ReceiveReturnCommandHandler : IRequestHandler<ReceiveReturnCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;
    private readonly IPublisher _publisher;

    public ReceiveReturnCommandHandler(IOrderDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<bool>> Handle(ReceiveReturnCommand request, CancellationToken cancellationToken)
    {
        var @return = await _context.Returns
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (@return is null)
            return Result.Failure<bool>("İade talebi bulunamadı.");

        if (@return.Status != "approved")
            return Result.Failure<bool>($"'{@return.Status}' durumundaki iade teslim alınamaz.");

        var now = DateTime.UtcNow;
        // İade planı §2.3: geri ödeme uygun değilse (kapıda ödeme teslimsiz / pazaryeri / tahsilat yok) teslim alma
        // ile iade KAPANIR ("refunded" yanıltıcı olurdu); uygunsa geri ödeme adımına geçer.
        var geriOdemeYok = @return.RefundStatus == ReturnConstants.RefundNotApplicable;
        @return.Status = geriOdemeYok ? ReturnConstants.StatusClosed : ReturnConstants.StatusReceived;
        @return.ReturnCargoReceivedAt = now;
        @return.InspectionNotes = request.InspectionNotes;
        @return.InspectionCompletedAt = now;
        @return.InspectionCompletedBy = request.ReceivedBy;
        @return.UpdatedAt = now;
        @return.UpdatedBy = request.ReceivedBy;

        foreach (var item in @return.Items)
            item.Status = "received";

        await _context.SaveChangesAsync(cancellationToken);

        // Stok geri dön event'i — StockAlreadyIn kalemler (kargoya verilmemiş teslimatsız iade: stok hiç çıkmadı,
        // rezervasyon iade anında serbest bırakıldı) depo girişine GİRMEZ (çift sayım önlenir).
        var returnedItems = @return.Items
            .Where(i => !i.StockAlreadyIn)
            .Select(i => new ReturnedItem(i.VariantId, i.Quantity))
            .ToList();

        if (returnedItems.Count > 0)
            await _publisher.Publish(
                new ReturnReceivedEvent(@return.Id, @return.OrderId, request.WarehouseId, request.ReceivedBy, returnedItems),
                cancellationToken);

        return Result.Success(true);
    }
}
