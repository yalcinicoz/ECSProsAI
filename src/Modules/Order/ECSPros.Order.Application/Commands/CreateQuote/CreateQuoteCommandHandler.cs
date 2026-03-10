using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateQuote;

public class CreateQuoteCommandHandler : IRequestHandler<CreateQuoteCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;

    public CreateQuoteCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        if (!request.Items.Any())
            return Result.Failure<Guid>("Teklif en az bir kalem içermelidir.");

        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var quoteNumber = $"QUO-{DateTime.UtcNow:yyyyMMdd}-{suffix}";

        var items = request.Items.Select(i =>
        {
            var discountAmount = i.UnitPrice * i.Quantity * (i.DiscountRate / 100m);
            var taxableAmount = i.UnitPrice * i.Quantity - discountAmount;
            var taxAmount = Math.Round(taxableAmount * (i.TaxRate / 100m), 2);
            var total = Math.Round(taxableAmount + taxAmount, 2);
            return new QuoteItem
            {
                VariantId = i.VariantId,
                Sku = i.Sku,
                ProductName = i.ProductName,
                VariantInfo = i.VariantInfo,
                Quantity = i.Quantity,
                UnitType = i.UnitType,
                UnitPrice = i.UnitPrice,
                DiscountRate = i.DiscountRate,
                DiscountAmount = Math.Round(discountAmount, 2),
                TaxRate = i.TaxRate,
                TaxAmount = taxAmount,
                Total = total,
                Notes = i.Notes
            };
        }).ToList();

        var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
        var totalDiscount = items.Sum(i => i.DiscountAmount);
        var totalTax = items.Sum(i => i.TaxAmount);
        var grandTotal = items.Sum(i => i.Total);

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            Status = "draft",
            ValidUntil = request.ValidUntil,
            CurrencyCode = request.CurrencyCode,
            Subtotal = Math.Round(subtotal, 2),
            TotalDiscount = Math.Round(totalDiscount, 2),
            TotalTax = Math.Round(totalTax, 2),
            GrandTotal = Math.Round(grandTotal, 2),
            NotesToCustomer = request.NotesToCustomer,
            InternalNotes = request.InternalNotes,
            CreatedBy = request.CreatedBy
        };

        foreach (var item in items)
            quote.Items.Add(item);

        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
