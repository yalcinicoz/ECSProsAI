using ECSPros.Finance.Application.Services;
using ECSPros.Finance.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Commands.CreateSupplierDelivery;

public record CreateSupplierDeliveryCommand(
    Guid SupplierId,
    Guid? InvoiceId,
    DateOnly DeliveryDate,
    string? DeliveryNoteNumber,
    Guid WarehouseId,
    string? Notes,
    List<CreateDeliveryItemDto> Items
) : IRequest<Result<Guid>>;

public record CreateDeliveryItemDto(
    Guid VariantId,
    int ExpectedQuantity,
    Guid? LocationId);

public class CreateSupplierDeliveryCommandHandler : IRequestHandler<CreateSupplierDeliveryCommand, Result<Guid>>
{
    private readonly IFinanceDbContext _db;

    public CreateSupplierDeliveryCommandHandler(IFinanceDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSupplierDeliveryCommand request, CancellationToken ct)
    {
        var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct);
        if (!supplierExists)
            return Result.Failure<Guid>("Tedarikçi bulunamadı.");

        if (!request.Items.Any())
            return Result.Failure<Guid>("Teslimat için en az bir kalem eklenmelidir.");

        var delivery = new SupplierDelivery
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            InvoiceId = request.InvoiceId,
            DeliveryDate = request.DeliveryDate,
            DeliveryNoteNumber = request.DeliveryNoteNumber,
            WarehouseId = request.WarehouseId,
            Status = "pending",
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            delivery.Items.Add(new SupplierDeliveryItem
            {
                Id = Guid.NewGuid(),
                DeliveryId = delivery.Id,
                VariantId = item.VariantId,
                ExpectedQuantity = item.ExpectedQuantity,
                ReceivedQuantity = 0,
                RejectedQuantity = 0,
                LocationId = item.LocationId,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.SupplierDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);

        return Result.Success(delivery.Id);
    }
}
