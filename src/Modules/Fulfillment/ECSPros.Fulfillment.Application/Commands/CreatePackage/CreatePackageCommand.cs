using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePackage;

/// <summary>Tek paket oluşturur. Paket numarası kanala özel seriden otomatik üretilir
/// (dışarıdan verilmez); sipariş içi sıra otomatik atanır (F2, 2026-07-19).</summary>
public record CreatePackageCommand(
    Guid OrderId,
    Guid? ShipmentId,
    string? Barcode,
    decimal? Weight,
    decimal? Width,
    decimal? Height,
    decimal? Length,
    decimal? Desi,
    Guid PackedBy,
    Guid? SupplierId = null,
    List<CreatePackageItem>? Items = null) : IRequest<Result<Guid>>;

public record CreatePackageItem(Guid OrderItemId, Guid VariantId, int Quantity);
