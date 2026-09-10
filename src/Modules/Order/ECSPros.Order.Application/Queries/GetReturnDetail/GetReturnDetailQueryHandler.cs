using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetReturnDetail;

public class GetReturnDetailQueryHandler : IRequestHandler<GetReturnDetailQuery, Result<ReturnDetailDto>>
{
    private readonly IOrderDbContext _context;
    private readonly ECSPros.Shared.Contracts.Channels.IChannelCapabilityResolver _kanal;

    public GetReturnDetailQueryHandler(IOrderDbContext context, ECSPros.Shared.Contracts.Channels.IChannelCapabilityResolver kanal)
    {
        _context = context;
        _kanal = kanal;
    }

    public async Task<Result<ReturnDetailDto>> Handle(GetReturnDetailQuery request, CancellationToken cancellationToken)
    {
        var @return = await _context.Returns
            .AsNoTracking()
            .Include(r => r.Items)
            .Include(r => r.Refunds)
            .AsSplitQuery()   // Faz 2: kardeş koleksiyon Include kartezyeni önlenir
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (@return is null)
            return Result.Failure<ReturnDetailDto>("İade talebi bulunamadı.");

        // İade planı §2.8: panel tutar alanı üst sınırla sınırlı — kural her seferinde ödeme satırlarından hesaplar.
        decimal ustSinir = 0m;
        if (@return.RefundStatus != Domain.Entities.ReturnConstants.RefundNotApplicable
            && @return.RefundStatus != Domain.Entities.ReturnConstants.RefundCompleted)
        {
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == @return.OrderId, cancellationToken);
            if (order is not null)
            {
                var sonuc = await Services.IadeOdemeDegerlendirme.DegerlendirAsync(_context, _kanal, order, @return.Id, cancellationToken);
                ustSinir = sonuc.Uygun ? sonuc.UstSinir : 0m;
            }
        }

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
                i.InspectionNotes,
                i.StockAlreadyIn)).ToList(),
            @return.Refunds.Select(r => new ReturnRefundDto(
                r.Id,
                r.RefundMethod,
                r.Amount,
                r.Status,
                r.ProcessedAt)).ToList(),
            @return.CargoReturnCode,
            @return.ImageUrls,
            @return.RefundNotApplicableReason,
            ustSinir);

        return Result.Success(dto);
    }
}
