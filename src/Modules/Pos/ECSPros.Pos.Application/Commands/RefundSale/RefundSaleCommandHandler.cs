using ECSPros.Pos.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Commands.RefundSale;

public class RefundSaleCommandHandler : IRequestHandler<RefundSaleCommand, Result<bool>>
{
    private readonly IPosDbContext _context;
    private readonly IPublisher _publisher;

    public RefundSaleCommandHandler(IPosDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<bool>> Handle(RefundSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _context.PosSales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
            return Result.Failure<bool>("Satış bulunamadı.");

        try
        {
            sale.Refund(request.RefundedBy);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ex.Message);
        }

        if (!string.IsNullOrEmpty(request.Reason))
            sale.Notes = string.IsNullOrEmpty(sale.Notes)
                ? $"İade: {request.Reason}"
                : $"{sale.Notes} | İade: {request.Reason}";

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in sale.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        sale.ClearDomainEvents();

        return Result.Success(true);
    }
}
