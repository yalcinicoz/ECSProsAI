using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.SplitOrderIntoPackages;

/// <summary>Siparişi tedarikçiye göre paketlere böler (karar 2026-07-19): her
/// tedarikçi grubu için bir paket açılır, kalemler pakete atanır. Tedarikçisi
/// olmayan kalemler tek pakette toplanır. Sipariş zaten paketlenmişse reddedilir
/// (elle düzenleme paket ekranından yapılır).</summary>
public record SplitOrderIntoPackagesCommand(
    Guid OrderId,
    Guid PackedBy) : IRequest<Result<List<Guid>>>;
