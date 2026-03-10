using ECSPros.Order.Application.Services;
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
        @return.Status = "received";
        @return.ReturnCargoReceivedAt = now;
        @return.InspectionNotes = request.InspectionNotes;
        @return.InspectionCompletedAt = now;
        @return.InspectionCompletedBy = request.ReceivedBy;
        @return.UpdatedAt = now;
        @return.UpdatedBy = request.ReceivedBy;

        foreach (var item in @return.Items)
            item.Status = "received";

        await _context.SaveChangesAsync(cancellationToken);

        // Stok geri dön event'i
        var returnedItems = @return.Items
            .Select(i => new ReturnedItem(i.VariantId, i.Quantity))
            .ToList();

        await _publisher.Publish(
            new ReturnReceivedEvent(@return.Id, @return.OrderId, request.WarehouseId, request.ReceivedBy, returnedItems),
            cancellationToken);

        return Result.Success(true);
    }
}
