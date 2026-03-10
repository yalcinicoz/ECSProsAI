using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.StartProcessing;

public class StartProcessingCommandHandler : IRequestHandler<StartProcessingCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;

    public StartProcessingCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(StartProcessingCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<bool>("Sipariş bulunamadı.");

        try
        {
            order.StartProcessing(request.UpdatedBy, request.PickingPlanId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
