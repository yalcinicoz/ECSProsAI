using ECSPros.Finance.Application.Services;
using ECSPros.Finance.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Commands.CreateSupplierPayment;

public record CreateSupplierPaymentCommand(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentType,
    string? Notes
) : IRequest<Result<Guid>>;

public class CreateSupplierPaymentCommandHandler : IRequestHandler<CreateSupplierPaymentCommand, Result<Guid>>
{
    private readonly IFinanceDbContext _db;

    public CreateSupplierPaymentCommandHandler(IFinanceDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSupplierPaymentCommand request, CancellationToken ct)
    {
        var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct);
        if (!supplierExists)
            return Result.Failure<Guid>("Tedarikçi bulunamadı.");

        if (request.Amount <= 0)
            return Result.Failure<Guid>("Ödeme tutarı sıfırdan büyük olmalıdır.");

        var lastTx = await _db.SupplierTransactions
            .Where(t => t.SupplierId == request.SupplierId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.BalanceAfter)
            .FirstOrDefaultAsync(ct);

        var newBalance = lastTx - request.Amount;

        var payment = new SupplierPayment
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            PaymentType = request.PaymentType,
            Notes = request.Notes,
            Status = "completed",
            CreatedAt = DateTime.UtcNow
        };

        var transaction = new SupplierTransaction
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            TransactionType = "payment",
            Debit = 0,
            Credit = request.Amount,
            BalanceAfter = newBalance,
            ReferenceType = "supplier_payment",
            ReferenceId = payment.Id,
            Description = $"Ödeme: {request.PaymentType}",
            TransactionDate = request.PaymentDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.SupplierPayments.Add(payment);
        _db.SupplierTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);

        return Result.Success(payment.Id);
    }
}
