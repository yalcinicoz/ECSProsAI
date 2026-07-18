using ECSPros.Accounts.Application.Commands.PostAccountTransaction;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CompleteRefund;

public class CompleteRefundCommandHandler : IRequestHandler<CompleteRefundCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;
    private readonly ISender _sender;

    public CompleteRefundCommandHandler(IOrderDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<Result<bool>> Handle(CompleteRefundCommand request, CancellationToken cancellationToken)
    {
        var @return = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken);

        if (@return is null)
            return Result.Failure<bool>("İade talebi bulunamadı.");

        if (@return.Status != "received")
            return Result.Failure<bool>($"'{@return.Status}' durumundaki iade için geri ödeme yapılamaz.");

        if (request.Amount <= 0)
            return Result.Failure<bool>("Geri ödeme tutarı sıfırdan büyük olmalıdır.");

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

        // Cüzdana iade: tutar önce üyenin cüzdan defterine alacak yazılır (cari çatı — Accounts).
        // Cüzdan yazımı başarısızsa iade tamamlanmaz; sipariş kaydı başarısız olursa ters kayıtla telafi edilir.
        Guid? walletTxId = null;
        if (request.RefundMethod == "wallet")
        {
            var posted = await _sender.Send(new PostAccountTransactionCommand(
                OwnerType: "member",
                OwnerId: @return.MemberId,
                ConceptCode: "wallet",
                TransactionType: "return_refund",
                Debit: 0,
                Credit: request.Amount,
                ReferenceType: "return_refund",
                ReferenceId: refund.Id,
                Description: $"İade geri ödemesi — {@return.ReturnNumber}"), cancellationToken);

            if (posted.IsFailure)
                return Result.Failure<bool>("Cüzdana iade yazılamadı: " + posted.Error);
            walletTxId = posted.Value!.TransactionId;
        }

        _context.ReturnRefunds.Add(refund);

        @return.Status = "refunded";
        @return.RefundStatus = "completed";
        @return.RefundAmount = request.Amount;
        @return.UpdatedAt = now;
        @return.UpdatedBy = request.ProcessedBy;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (walletTxId.HasValue)
            {
                // Telafi: sipariş tarafı kaydedilemedi, cüzdandaki alacağı ters kayıtla geri al
                await _sender.Send(new PostAccountTransactionCommand(
                    OwnerType: "member",
                    OwnerId: @return.MemberId,
                    ConceptCode: "wallet",
                    TransactionType: "storno",
                    Debit: request.Amount,
                    Credit: 0,
                    ReferenceType: "return_refund_storno",
                    ReferenceId: refund.Id,
                    Description: $"Ters kayıt (iade kaydı başarısız) — {@return.ReturnNumber}",
                    AllowNegativeBalance: true), CancellationToken.None);
            }
            throw;
        }

        return Result.Success(true);
    }
}
