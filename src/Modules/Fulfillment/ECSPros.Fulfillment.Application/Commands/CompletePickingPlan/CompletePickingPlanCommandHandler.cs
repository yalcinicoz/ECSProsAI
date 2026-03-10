using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.CompletePickingPlan;

public class CompletePickingPlanCommandHandler : IRequestHandler<CompletePickingPlanCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;
    private readonly IPublisher _publisher;

    public CompletePickingPlanCommandHandler(IFulfillmentDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<bool>> Handle(CompletePickingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _context.PickingPlans
            .Include(p => p.Bins)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result.Failure<bool>("Toplama planı bulunamadı.");

        try
        {
            plan.Complete(request.CompletedBy);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in plan.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        plan.ClearDomainEvents();

        return Result.Success(true);
    }
}
