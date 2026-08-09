using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingTaskCandidates;

/// <summary>OP1: görev oluşturma önizlemesi — filtreye uyan siparişler + özet sayılar.
/// Karma depolu siparişler depo filtresi verildiğinde listeye girmez ama sayısı raporlanır
/// (unutulmasınlar diye ayrı sekmede gösterilir).</summary>
public record GetPickingTaskCandidatesQuery(
    List<Guid>? FirmPlatformIds,
    Guid? WarehouseId,
    int? MinItems,
    int? MaxItems,
    Guid? CargoIntegrationId,
    Guid? ShippingCityId,
    DateTime? From,
    DateTime? To,
    int PreviewLimit = 300) : IRequest<Result<PickingTaskCandidatesDto>>;

public record CandidatePreviewDto(
    Guid OrderId, string OrderNumber, Guid FirmPlatformId, DateTime CreatedAt,
    int TotalQuantity, Guid? CargoIntegrationId, string? CargoName,
    Guid ShippingCityId, List<Guid> WarehouseIds, bool KarmaDepolu);

public record PickingTaskCandidatesDto(
    int ToplamSiparis,
    int TekUrunlu,
    int CokUrunlu,
    int KarmaDepoluHaricTutulan,
    List<CandidatePreviewDto> Onizleme);

public class GetPickingTaskCandidatesQueryHandler(IOrderPickingReader reader)
    : IRequestHandler<GetPickingTaskCandidatesQuery, Result<PickingTaskCandidatesDto>>
{
    public async Task<Result<PickingTaskCandidatesDto>> Handle(
        GetPickingTaskCandidatesQuery request, CancellationToken ct)
    {
        var filtre = new PickingTaskFilter(
            request.FirmPlatformIds, request.WarehouseId, request.MinItems, request.MaxItems,
            request.CargoIntegrationId, request.ShippingCityId, request.From, request.To);

        // Karma depolu sayısı için depo filtresiz koşu + depo filtreli sonuç tek okumada:
        // reader depo kuralını bellek tarafında uyguladığından filtresiz alıp burada ayırıyoruz.
        var hepsi = await reader.GetCandidatesAsync(filtre with { WarehouseId = null }, ct);

        var karmaHaric = 0;
        List<PickingCandidate> adaylar;
        if (request.WarehouseId is { } depo)
        {
            adaylar = hepsi.Where(c => c.WarehouseIds.Count == 1 && c.WarehouseIds[0] == depo).ToList();
            karmaHaric = hepsi.Count(c => c.KarmaDepolu && c.WarehouseIds.Contains(depo));
        }
        else
        {
            adaylar = hepsi;
        }

        return Result.Success(new PickingTaskCandidatesDto(
            adaylar.Count,
            adaylar.Count(a => a.TekUrunlu),
            adaylar.Count(a => !a.TekUrunlu),
            karmaHaric,
            adaylar.Take(request.PreviewLimit)
                .Select(a => new CandidatePreviewDto(a.OrderId, a.OrderNumber, a.FirmPlatformId,
                    a.CreatedAt, a.TotalQuantity, a.CargoIntegrationId, a.CargoName,
                    a.ShippingCityId, a.WarehouseIds, a.KarmaDepolu))
                .ToList()));
    }
}
