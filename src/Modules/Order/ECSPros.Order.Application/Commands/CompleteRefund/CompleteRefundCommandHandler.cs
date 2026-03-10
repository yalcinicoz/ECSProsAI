using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CompleteRefund;

public class CompleteRefundCommandHandler : IRequestHandler<CompleteRefundCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;

    public CompleteRefundCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(CompleteRefundCommand request, CancellationToken cancellationToken)
    {
        var @return = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (@return is null)
            return Result.Failure<bool>("İade talebi bulunamadı.");

        if (@return.Status != "received")
            return Result.Failure<bool>($"'{@return.Status}' durumundaki iade için geri ödeme yapılamaz.");

        var now = DateTime.UtcNow;

        var refund = new ReturnRefund
        {
            ReturnId = request.ReturnId,
            RefundMethod = request.RefundMethod,
            Amount = request.Amount,
            Status = "completed",
            Details = request.Details,
            ProcessedAt = now,
            ProcessedBy = request.ProcessedBy
        };

        _context.ReturnRefunds.Add(refund);

        @return.Status = "refunded";
        @return.RefundStatus = "completed";
        @return.RefundAmount = request.Amount;
        @return.UpdatedAt = now;
        @return.UpdatedBy = request.ProcessedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
