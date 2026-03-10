using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateReturn;

public class CreateReturnCommandHandler : IRequestHandler<CreateReturnCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;

    public CreateReturnCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateReturnCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        if (order.Status is not ("shipped" or "delivered"))
            return Result.Failure<Guid>("İade yalnızca kargoya verilmiş veya teslim edilmiş siparişler için yapılabilir.");

        if (!request.Items.Any())
            return Result.Failure<Guid>("İade en az bir kalem içermelidir.");

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var returnNumber = $"RET-{now:yyyyMMdd}-{suffix}";

        var @return = new Return
        {
            ReturnNumber = returnNumber,
            OrderId = request.OrderId,
            MemberId = request.MemberId,
            ReturnType = request.ReturnType,
            CustomerNotes = request.CustomerNotes,
            Status = "requested",
            RefundMethod = request.RefundMethod,
            RefundStatus = "pending",
            RefundAmount = 0
        };

        foreach (var item in request.Items)
        {
            @return.Items.Add(new ReturnItem
            {
                OrderItemId = item.OrderItemId,
                VariantId = item.VariantId,
                Quantity = item.Quantity,
                ReturnReasonId = item.ReturnReasonId,
                CustomerNotes = item.CustomerNotes,
                UnitRefundAmount = 0,
                TotalRefundAmount = 0,
                Status = "pending"
            });
        }

        _context.Returns.Add(@return);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(@return.Id);
    }
}
