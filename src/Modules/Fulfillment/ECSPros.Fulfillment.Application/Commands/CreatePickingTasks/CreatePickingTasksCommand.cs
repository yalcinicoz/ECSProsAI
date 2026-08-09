using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePickingTasks;

/// <summary>
/// OP1: filtreli toplama görevi oluşturma — adaylar tek/çok ürünlüye ayrılır, seçilen
/// tipler için ayrı planlar açılır; satırlar rezervasyon raflarından rota sıralı üretilir.
/// Sipariş plana bağlanınca mevcut PickingPlanCreatedEvent akışı (Order → processing) çalışır.
/// </summary>
public record CreatePickingTasksCommand(
    List<Guid>? FirmPlatformIds,
    Guid? WarehouseId,
    int? MinItems,
    int? MaxItems,
    Guid? CargoIntegrationId,
    Guid? ShippingCityId,
    DateTime? From,
    DateTime? To,
    bool CreateSingleItemTask,
    bool CreateMultiItemTask,
    Guid PlannedBy) : IRequest<Result<CreatedTasksDto>>;

public record CreatedTaskInfo(Guid PlanId, string PlanNumber, string PlanType, int OrderCount, int LineCount);
public record CreatedTasksDto(List<CreatedTaskInfo> Tasks);

public class CreatePickingTasksCommandHandler(
    IFulfillmentDbContext db,
    IOrderPickingReader reader,
    IPublisher publisher)
    : IRequestHandler<CreatePickingTasksCommand, Result<CreatedTasksDto>>
{
    public async Task<Result<CreatedTasksDto>> Handle(CreatePickingTasksCommand request, CancellationToken ct)
    {
        if (!request.CreateSingleItemTask && !request.CreateMultiItemTask)
            return Result.Failure<CreatedTasksDto>("En az bir görev tipi seçilmeli (tek/çok ürünlü).");

        var filtre = new PickingTaskFilter(
            request.FirmPlatformIds, request.WarehouseId, request.MinItems, request.MaxItems,
            request.CargoIntegrationId, request.ShippingCityId, request.From, request.To);
        var adaylar = await reader.GetCandidatesAsync(filtre, ct);
        if (adaylar.Count == 0)
            return Result.Failure<CreatedTasksDto>("Filtreye uyan sipariş bulunamadı.");

        var gruplar = new List<(string PlanType, List<PickingCandidate> Orders)>();
        if (request.CreateSingleItemTask)
        {
            var tekli = adaylar.Where(a => a.TekUrunlu).ToList();
            if (tekli.Count > 0) gruplar.Add(("single_item", tekli));
        }
        if (request.CreateMultiItemTask)
        {
            var coklu = adaylar.Where(a => !a.TekUrunlu).ToList();
            if (coklu.Count > 0) gruplar.Add(("bulk", coklu));
        }
        if (gruplar.Count == 0)
            return Result.Failure<CreatedTasksDto>("Seçilen görev tiplerine uyan sipariş yok.");

        var kaynak = await reader.GetLineSourcesAsync(
            gruplar.SelectMany(g => g.Orders).Select(o => o.OrderId).Distinct().ToList(), ct);
        var itemsByOrder = kaynak.Items.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());
        // Rezervasyonlar (sipariş, varyant) başına rota sıralı raf listesi
        var rezervByOrderVariant = kaynak.Reservations
            .GroupBy(r => (r.OrderId, r.VariantId))
            .ToDictionary(g => g.Key,
                g => g.OrderBy(r => r.SectionOrder).ThenBy(r => r.BinOrder).ThenBy(r => r.BinCode).ToList());

        var now = DateTime.UtcNow;
        var olusan = new List<CreatedTaskInfo>();
        var eventler = new List<PickingPlanCreatedEvent>();

        foreach (var (planType, orders) in gruplar)
        {
            var plan = new PickingPlan
            {
                PlanNumber = $"PICK-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                WarehouseId = request.WarehouseId ?? Guid.Empty,
                PlanType = planType,
                Status = "pending",
                PlannedBy = request.PlannedBy,
                PlannedAt = now,
                CreatedBy = request.PlannedBy
            };

            var assigned = new List<AssignedOrder>();
            var binNo = 1;
            var satirlar = new List<PickingPlanLine>();
            foreach (var order in orders)
            {
                plan.Bins.Add(new SortingBin
                {
                    OrderId = order.OrderId, BinNumber = binNo, Status = "empty", CreatedBy = request.PlannedBy
                });
                assigned.Add(new AssignedOrder(order.OrderId, binNo));
                binNo++;

                foreach (var item in itemsByOrder.GetValueOrDefault(order.OrderId) ?? [])
                {
                    var raflar = rezervByOrderVariant.GetValueOrDefault((order.OrderId, item.VariantId));
                    var raf = raflar?.FirstOrDefault(r => r.BinId != null) ?? raflar?.FirstOrDefault();
                    satirlar.Add(new PickingPlanLine
                    {
                        PickingPlanId = plan.Id,
                        OrderId = order.OrderId,
                        OrderItemId = item.OrderItemId,
                        VariantId = item.VariantId,
                        VariantBarcode = item.Barcode ?? "",
                        OrderNumber = order.OrderNumber,
                        OrderCreatedAt = order.CreatedAt,
                        DisplayName = string.Join(" ", new[] { item.ProductName, item.VariantInfo }
                            .Where(s => !string.IsNullOrWhiteSpace(s))),
                        Sku = item.Sku ?? "",
                        Quantity = item.Quantity,
                        SourceBinId = raf?.BinId,
                        SourceBinCode = raf?.BinCode,
                        Status = "pending",
                        CreatedBy = request.PlannedBy
                    });
                }
            }

            // Rota: raf koduna göre (kısım/raf PickingOrder'ı raf seçiminde uygulandı;
            // satır sırası personelin yürüyüş sırasıdır — rafsızlar sona)
            var sirali = satirlar
                .OrderBy(l => l.SourceBinCode is null)
                .ThenBy(l => l.SourceBinCode)
                .ThenBy(l => l.OrderNumber)
                .ToList();
            for (var i = 0; i < sirali.Count; i++) sirali[i].RouteOrder = i + 1;

            db.PickingPlans.Add(plan);
            foreach (var l in sirali) db.PickingPlanLines.Add(l);
            foreach (var order in orders)
            {
                db.OperationLogs.Add(new OperationLog
                {
                    OrderId = order.OrderId, PickingPlanId = plan.Id, Action = "task_created",
                    ActorId = request.PlannedBy, CreatedBy = request.PlannedBy,
                    Detail = new Dictionary<string, object>
                        { ["planNumber"] = plan.PlanNumber, ["planType"] = planType }
                });
            }

            olusan.Add(new CreatedTaskInfo(plan.Id, plan.PlanNumber, planType, orders.Count, sirali.Count));
            eventler.Add(new PickingPlanCreatedEvent(plan.Id, plan.WarehouseId, request.PlannedBy, assigned));
        }

        await db.SaveChangesAsync(ct);
        foreach (var ev in eventler)
            await publisher.Publish(ev, ct);

        return Result.Success(new CreatedTasksDto(olusan));
    }
}
