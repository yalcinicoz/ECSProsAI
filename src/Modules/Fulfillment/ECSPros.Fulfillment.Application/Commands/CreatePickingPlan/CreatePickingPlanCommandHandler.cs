using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePickingPlan;

public class CreatePickingPlanCommandHandler : IRequestHandler<CreatePickingPlanCommand, Result<Guid>>
{
    private readonly IFulfillmentDbContext _context;
    private readonly IPublisher _publisher;

    public CreatePickingPlanCommandHandler(IFulfillmentDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(CreatePickingPlanCommand request, CancellationToken cancellationToken)
    {
        if (!request.OrderIds.Any())
            return Result.Failure<Guid>("Toplama planı en az bir sipariş içermelidir.");

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var planNumber = $"PICK-{now:yyyyMMdd}-{suffix}";

        var plan = new PickingPlan
        {
            PlanNumber = planNumber,
            WarehouseId = request.WarehouseId,
            PlanType = request.PlanType,
            Status = "pending",
            PlannedBy = request.PlannedBy,
            PlannedAt = now,
            CreatedBy = request.PlannedBy
        };

        // Her sipariş için bir sorting bin oluştur
        var assignedOrders = new List<AssignedOrder>();
        int binNumber = 1;
        foreach (var orderId in request.OrderIds.Distinct())
        {
            var bin = new SortingBin
            {
                OrderId = orderId,
                BinNumber = binNumber,
                Status = "empty",
                CreatedBy = request.PlannedBy
            };
            plan.Bins.Add(bin);
            assignedOrders.Add(new AssignedOrder(orderId, binNumber));
            binNumber++;
        }

        _context.PickingPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);

        // Domain event yayınla — Order modülü siparişleri "processing" yapar
        var createdEvent = new PickingPlanCreatedEvent(plan.Id, request.WarehouseId, request.PlannedBy, assignedOrders);
        await _publisher.Publish(createdEvent, cancellationToken);

        return Result.Success(plan.Id);
    }
}
