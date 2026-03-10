using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.StartPickingPlan;

public class StartPickingPlanCommandHandler : IRequestHandler<StartPickingPlanCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;

    public StartPickingPlanCommandHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(StartPickingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _context.PickingPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result.Failure<bool>("Toplama planı bulunamadı.");

        try
        {
            plan.Start(request.StartedBy);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
