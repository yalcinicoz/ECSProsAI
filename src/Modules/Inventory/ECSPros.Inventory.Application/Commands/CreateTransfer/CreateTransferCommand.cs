using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.CreateTransfer;

public record CreateTransferCommand(
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    string TransferType,
    string? Notes,
    Guid RequestedBy,
    List<CreateTransferItemDto> Items
) : IRequest<Result<Guid>>;

public record CreateTransferItemDto(
    Guid VariantId,
    int RequestedQuantity,
    Guid? FromLocationId,
    Guid? ToLocationId);

public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _db;

    public CreateTransferCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateTransferCommand request, CancellationToken ct)
    {
        var fromExists = await _db.Warehouses.AnyAsync(w => w.Id == request.FromWarehouseId, ct);
        if (!fromExists)
            return Result.Failure<Guid>("Kaynak depo bulunamadı.");

        var toExists = await _db.Warehouses.AnyAsync(w => w.Id == request.ToWarehouseId, ct);
        if (!toExists)
            return Result.Failure<Guid>("Hedef depo bulunamadı.");

        if (!request.Items.Any())
            return Result.Failure<Guid>("Transfer için en az bir ürün eklenmelidir.");

        var count = await _db.TransferRequests.CountAsync(ct);
        var code = $"TR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";

        var transfer = new TransferRequest
        {
            Id = Guid.NewGuid(),
            Code = code,
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            TransferType = request.TransferType,
            Status = "draft",
            RequestedBy = request.RequestedBy,
            RequestedAt = DateTime.UtcNow,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            transfer.Items.Add(new TransferRequestItem
            {
                Id = Guid.NewGuid(),
                TransferRequestId = transfer.Id,
                VariantId = item.VariantId,
                RequestedQuantity = item.RequestedQuantity,
                FromLocationId = item.FromLocationId,
                ToLocationId = item.ToLocationId,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.TransferRequests.Add(transfer);
        await _db.SaveChangesAsync(ct);

        return Result.Success(transfer.Id);
    }
}
