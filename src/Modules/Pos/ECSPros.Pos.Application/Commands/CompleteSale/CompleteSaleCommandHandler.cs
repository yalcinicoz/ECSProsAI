using ECSPros.Pos.Application.Services;
using ECSPros.Pos.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Commands.CompleteSale;

public class CompleteSaleCommandHandler : IRequestHandler<CompleteSaleCommand, Result<Guid>>
{
    private readonly IPosDbContext _context;
    private readonly IPublisher _publisher;

    public CompleteSaleCommandHandler(IPosDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(CompleteSaleCommand request, CancellationToken cancellationToken)
    {
        if (!request.Items.Any())
            return Result.Failure<Guid>("Fiş en az bir kalem içermelidir.");

        var session = await _context.PosSessions
            .Include(s => s.Register)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.Status == "open", cancellationToken);

        if (session is null)
            return Result.Failure<Guid>("Açık oturum bulunamadı.");

        var register = session.Register;

        // Fiş numarası üret ve sıra no artır
        register.ReceiptSequence++;
        var saleNumber = $"{register.ReceiptPrefix}-{register.ReceiptSequence:D6}";

        // Kalemleri hesapla
        var items = request.Items.Select(i =>
        {
            var taxAmount = (i.UnitPrice - i.DiscountAmount / i.Quantity) * i.Quantity * (i.TaxRate / 100m);
            var lineTotal = i.UnitPrice * i.Quantity - i.DiscountAmount + taxAmount;
            return new PosSaleItem
            {
                VariantId = i.VariantId,
                Barcode = i.Barcode,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                TaxRate = i.TaxRate,
                TaxAmount = Math.Round(taxAmount, 2),
                LineTotal = Math.Round(lineTotal, 2)
            };
        }).ToList();

        var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
        var totalDiscount = items.Sum(i => i.DiscountAmount);
        var totalTax = items.Sum(i => i.TaxAmount);
        var grandTotal = items.Sum(i => i.LineTotal);

        var totalPaid = request.Payments.Sum(p => p.Amount);
        if (totalPaid < grandTotal)
            return Result.Failure<Guid>($"Ödeme tutarı yetersiz. Gerekli: {grandTotal:N2}, Ödenen: {totalPaid:N2}");

        var sale = new PosSale
        {
            SessionId = request.SessionId,
            RegisterId = register.Id,
            WarehouseId = register.WarehouseId,
            MemberId = request.MemberId,
            SaleNumber = saleNumber,
            Subtotal = Math.Round(subtotal, 2),
            TotalDiscount = Math.Round(totalDiscount, 2),
            TotalTax = Math.Round(totalTax, 2),
            GrandTotal = Math.Round(grandTotal, 2),
            Notes = request.Notes
        };

        foreach (var item in items)
            sale.Items.Add(item);

        foreach (var p in request.Payments)
            sale.Payments.Add(new PosSalePayment
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                TenderedAmount = p.TenderedAmount,
                ChangeAmount = p.ChangeAmount
            });

        sale.Complete(request.CompletedBy);

        _context.PosSales.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

        // Domain event'leri yayınla (stok düşümü vs.)
        foreach (var domainEvent in sale.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        sale.ClearDomainEvents();

        return Result.Success(sale.Id);
    }
}
