using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CancelInvoice;

public class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;

    public CancelInvoiceCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
            return Result.Failure<bool>("Fatura bulunamadı.");

        if (invoice.Status == "cancelled")
            return Result.Failure<bool>("Fatura zaten iptal edilmiş.");

        invoice.Status = "cancelled";
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = request.CancelledBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
