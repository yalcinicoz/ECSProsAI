using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.AddTransferItem;

public record AddTransferItemCommand(
    Guid TransferRequestId,
    Guid VariantId,
    int RequestedQuantity,
    Guid? FromLocationId,
    Guid? ToLocationId
) : IRequest<Result<Guid>>;

public class AddTransferItemCommandHandler : IRequestHandler<AddTransferItemCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _db;

    public AddTransferItemCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(AddTransferItemCommand request, CancellationToken ct)
    {
        var transfer = await _db.TransferRequests
            .FirstOrDefaultAsync(t => t.Id == request.TransferRequestId, ct);

        if (transfer is null)
            return Result.Failure<Guid>("Transfer bulunamadı.");

        if (transfer.Status != "draft")
            return Result.Failure<Guid>("Sadece taslak transferlere kalem eklenebilir.");

        if (request.RequestedQuantity < 1)
            return Result.Failure<Guid>("Miktar en az 1 olmalıdır.");

        var item = new TransferRequestItem
        {
            Id = Guid.NewGuid(),
            TransferRequestId = transfer.Id,
            VariantId = request.VariantId,
            RequestedQuantity = request.RequestedQuantity,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.TransferRequestItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return Result.Success(item.Id);
    }
}
