using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.SetReturnRefundBank;

/// <summary>FAZ 15.4g: iadenin havale bilgisi (IBAN + hesap sahibi) — ödeme yapılmadan önce girilir/düzeltilir
/// (eski "Müşteriye Ödemeler › IBAN Güncelle").</summary>
public record SetReturnRefundBankCommand(Guid ReturnId, string? Iban, string? AccountHolder, Guid UpdatedBy) : IRequest<Result<bool>>;

public class SetReturnRefundBankCommandHandler(IOrderDbContext db) : IRequestHandler<SetReturnRefundBankCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetReturnRefundBankCommand request, CancellationToken ct)
    {
        var iade = await db.Returns.FirstOrDefaultAsync(r => r.Id == request.ReturnId, ct);
        if (iade is null) return Result.Failure<bool>("İade bulunamadı.");
        if (iade.RefundStatus == ReturnConstants.RefundCompleted) return Result.Failure<bool>("Geri ödemesi tamamlanmış iadede banka bilgisi değiştirilemez.");
        var iban = request.Iban?.Replace(" ", "").ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(iban) && (iban.Length < 15 || iban.Length > 34))
            return Result.Failure<bool>("IBAN uzunluğu geçersiz.");
        iade.RefundIban = string.IsNullOrWhiteSpace(iban) ? null : iban;
        iade.RefundAccountHolder = string.IsNullOrWhiteSpace(request.AccountHolder) ? null : request.AccountHolder!.Trim();
        iade.UpdatedAt = DateTime.UtcNow; iade.UpdatedBy = request.UpdatedBy;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
