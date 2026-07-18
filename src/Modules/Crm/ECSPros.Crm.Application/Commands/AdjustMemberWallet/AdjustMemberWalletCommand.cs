using ECSPros.Accounts.Application.Commands.PostAccountTransaction;
using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.AdjustMemberWallet;

/// <summary>Panelden manuel cüzdan düzeltmesi. Direction: credit = bakiye artar, debit = azalır.</summary>
public record AdjustMemberWalletCommand(
    Guid MemberId,
    string Direction,      // credit | debit
    decimal Amount,
    string Description) : IRequest<Result<AdjustMemberWalletResult>>;

public record AdjustMemberWalletResult(Guid TransactionId, decimal BalanceAfter);

public class AdjustMemberWalletCommandHandler
    : IRequestHandler<AdjustMemberWalletCommand, Result<AdjustMemberWalletResult>>
{
    private readonly ICrmDbContext _db;
    private readonly ISender _sender;

    public AdjustMemberWalletCommandHandler(ICrmDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Result<AdjustMemberWalletResult>> Handle(AdjustMemberWalletCommand r, CancellationToken ct)
    {
        if (r.Amount <= 0)
            return Result.Failure<AdjustMemberWalletResult>("Tutar sıfırdan büyük olmalıdır.");
        if (r.Direction != "credit" && r.Direction != "debit")
            return Result.Failure<AdjustMemberWalletResult>("Yön 'credit' veya 'debit' olmalıdır.");
        if (string.IsNullOrWhiteSpace(r.Description))
            return Result.Failure<AdjustMemberWalletResult>("Açıklama zorunludur (manuel düzeltme izlenebilir olmalı).");

        var member = await _db.Members
            .Where(m => m.Id == r.MemberId)
            .Select(m => new { m.Id, m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);
        if (member is null)
            return Result.Failure<AdjustMemberWalletResult>("Üye bulunamadı.");

        var posted = await _sender.Send(new PostAccountTransactionCommand(
            OwnerType: "member",
            OwnerId: r.MemberId,
            ConceptCode: "wallet",
            TransactionType: "manual_adjustment",
            Debit: r.Direction == "debit" ? r.Amount : 0,
            Credit: r.Direction == "credit" ? r.Amount : 0,
            ReferenceType: "manual",
            ReferenceId: null,
            Description: r.Description,
            OwnerTitle: $"{member.FirstName} {member.LastName}".Trim(),
            AllowNegativeBalance: false), ct);

        if (posted.IsFailure)
            return Result.Failure<AdjustMemberWalletResult>(posted.Error ?? "Cüzdan hareketi kaydedilemedi.");

        return Result.Success(new AdjustMemberWalletResult(posted.Value!.TransactionId, posted.Value.BalanceAfter));
    }
}
