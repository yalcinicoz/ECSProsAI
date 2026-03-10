using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetTransferDetail;

public record GetTransferDetailQuery(Guid Id) : IRequest<Result<TransferDetailDto>>;

public record TransferDetailDto(
    Guid Id,
    string Code,
    Guid FromWarehouseId,
    string FromWarehouseCode,
    Guid ToWarehouseId,
    string ToWarehouseCode,
    string TransferType,
    string Status,
    Guid RequestedBy,
    DateTime RequestedAt,
    string? Notes,
    List<TransferItemDto> Items,
    DateTime CreatedAt);

public record TransferItemDto(
    Guid Id,
    Guid VariantId,
    int RequestedQuantity,
    int PickedQuantity,
    int DeliveredQuantity,
    Guid? FromLocationId,
    Guid? ToLocationId,
    string Status);

public class GetTransferDetailQueryHandler : IRequestHandler<GetTransferDetailQuery, Result<TransferDetailDto>>
{
    private readonly IInventoryDbContext _db;

    public GetTransferDetailQueryHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<TransferDetailDto>> Handle(GetTransferDetailQuery request, CancellationToken ct)
    {
        var transfer = await _db.TransferRequests
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (transfer is null)
            return Result.Failure<TransferDetailDto>("Transfer bulunamadı.");

        var dto = new TransferDetailDto(
            transfer.Id, transfer.Code,
            transfer.FromWarehouseId, transfer.FromWarehouse.Code,
            transfer.ToWarehouseId, transfer.ToWarehouse.Code,
            transfer.TransferType, transfer.Status,
            transfer.RequestedBy, transfer.RequestedAt, transfer.Notes,
            transfer.Items.Select(i => new TransferItemDto(
                i.Id, i.VariantId, i.RequestedQuantity, i.PickedQuantity,
                i.DeliveredQuantity, i.FromLocationId, i.ToLocationId, i.Status)).ToList(),
            transfer.CreatedAt);

        return Result.Success(dto);
    }
}
