using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetReturnDetail;

public class GetReturnDetailQueryHandler : IRequestHandler<GetReturnDetailQuery, Result<ReturnDetailDto>>
{
    private readonly IOrderDbContext _context;

    public GetReturnDetailQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ReturnDetailDto>> Handle(GetReturnDetailQuery request, CancellationToken cancellationToken)
    {
        var @return = await _context.Returns
            .Include(r => r.Items)
            .Include(r => r.Refunds)
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (@return is null)
            return Result.Failure<ReturnDetailDto>("İade talebi bulunamadı.");

        var dto = new ReturnDetailDto(
            @return.Id,
            @return.ReturnNumber,
            @return.OrderId,
            @return.MemberId,
            @return.ReturnType,
            @return.CustomerNotes,
            @return.Status,
            @return.ReturnTrackingNumber,
            @return.ReturnCargoSentAt,
            @return.ReturnCargoReceivedAt,
            @return.InspectionNotes,
            @return.InspectionCompletedAt,
            @return.RefundMethod,
            @return.RefundStatus,
            @return.RefundAmount,
            @return.CreatedAt,
            @return.Items.Select(i => new ReturnItemDto(
                i.Id,
                i.OrderItemId,
                i.VariantId,
                i.Quantity,
                i.ReturnReasonId,
                i.CustomerNotes,
                i.UnitRefundAmount,
                i.TotalRefundAmount,
                i.Status,
                i.InspectionResult,
                i.InspectionNotes)).ToList(),
            @return.Refunds.Select(r => new ReturnRefundDto(
                r.Id,
                r.RefundMethod,
                r.Amount,
                r.Status,
                r.ProcessedAt)).ToList());

        return Result.Success(dto);
    }
}
