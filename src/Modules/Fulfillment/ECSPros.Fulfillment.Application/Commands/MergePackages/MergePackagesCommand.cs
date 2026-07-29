using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.MergePackages;

/// <summary>Paket birleştirme — İSTİSNA akışı (karar 2026-07-19): normal olan paket
/// başına ayrı fatura+kargodur. Ayrı permission (order.packages.merge) + zorunlu
/// gerekçe ister; kargoya verilmiş/etiketi basılmış paket birleştirilemez. Eski
/// paket numaraları kod geçmişine yazılır ve havuza geri dönmez.</summary>
public record MergePackagesCommand(
    List<Guid> PackageIds,
    string Reason,
    Guid MergedBy) : IRequest<Result<Guid>>;
