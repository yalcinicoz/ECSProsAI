using ECSPros.Pos.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Queries.GetPosSaleDetail;

public class GetPosSaleDetailQueryHandler : IRequestHandler<GetPosSaleDetailQuery, Result<PosSaleDetailDto>>
{
    private readonly IPosDbContext _context;

    public GetPosSaleDetailQueryHandler(IPosDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PosSaleDetailDto>> Handle(GetPosSaleDetailQuery request, CancellationToken cancellationToken)
    {
        var sale = await _context.PosSales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
            return Result.Failure<PosSaleDetailDto>("Satış bulunamadı.");

        var dto = new PosSaleDetailDto(
            sale.Id,
            sale.SaleNumber,
            sale.SessionId,
            sale.RegisterId,
            sale.MemberId,
            sale.Status,
            sale.Subtotal,
            sale.TotalDiscount,
            sale.TotalTax,
            sale.GrandTotal,
            sale.Notes,
            sale.CreatedAt,
            sale.Items.Select(i => new PosSaleItemDto(
                i.Id,
                i.VariantId,
                i.Barcode,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.DiscountAmount,
                i.TaxRate,
                i.TaxAmount,
                i.LineTotal)).ToList(),
            sale.Payments.Select(p => new PosSalePaymentDto(
                p.Id,
                p.PaymentMethod,
                p.Amount,
                p.TenderedAmount,
                p.ChangeAmount)).ToList());

        return Result.Success(dto);
    }
}
