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
    private readonly ECSPros.Shared.Contracts.Channels.IChannelCapabilityResolver _kanal;

    public CompleteRefundCommandHandler(IOrderDbContext context, ISender sender,
        ECSPros.Shared.Contracts.Channels.IChannelCapabilityResolver kanal)
    {
        _context = context;
        _sender = sender;
        _kanal = kanal;
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

        // İade planı §2.5 (b) — İKİNCİ SAVUNMA HATTI: iade kaydı "geri ödeme yok" diyorsa ya da kural şu an
        // uygun bulmuyorsa (pazaryeri / tahsilat yok / tamamı iade edilmiş) ödeme YAPILMAZ; tutar tahsil edilen
        // kalanı (üst sınır) aşamaz — panel elle tutar girse bile (E9).
        if (@return.RefundStatus == ReturnConstants.RefundNotApplicable)
            return Result.Failure<bool>("Bu iadede müşteriye para iadesi yoktur: "
                + ECSPros.Shared.Contracts.DurumEtiketleri.Etiket(ECSPros.Shared.Contracts.DurumEtiketleri.Panel.GeriOdemeYokNedeni, @return.RefundNotApplicableReason) + ".");

        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == @return.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure<bool>("İadenin siparişi bulunamadı.");
        var uygunluk = await IadeOdemeDegerlendirme.DegerlendirAsync(_context, _kanal, order, @return.Id, cancellationToken);
        if (!uygunluk.Uygun)
            return Result.Failure<bool>("Müşteriye para iadesi yapılamaz: "
                + ECSPros.Shared.Contracts.DurumEtiketleri.Etiket(ECSPros.Shared.Contracts.DurumEtiketleri.Panel.GeriOdemeYokNedeni, uygunluk.Neden) + ".");
        if (request.Amount > uygunluk.UstSinir + 0.005m)
            return Result.Failure<bool>($"Geri ödeme tutarı tahsil edilen tutarı aşamaz (üst sınır {uygunluk.UstSinir:N2}).");

        var now = DateTime.UtcNow;

        // 15.4g: havale ile geri ödemede IBAN zorunlu (eski "Müşteriye Ödemeler" kalıbı); komutla gelen bilgi iade kaydına da yazılır.
        var iban = (request.Iban ?? @return.RefundIban)?.Replace(" ", "").ToUpperInvariant();
        var hesapSahibi = request.AccountHolder ?? @return.RefundAccountHolder;
        if (request.RefundMethod == ReturnConstants.RefundMethodBankTransfer)
        {
            if (string.IsNullOrWhiteSpace(iban) || iban.Length < 15)
                return Result.Failure<bool>("Havale ile geri ödeme için müşterinin IBAN'ı gerekli.");
            @return.RefundIban = iban; @return.RefundAccountHolder = hesapSahibi;
        }
        var details = request.Details ?? new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(iban)) { details["iban"] = iban; if (!string.IsNullOrWhiteSpace(hesapSahibi)) details["accountHolder"] = hesapSahibi!; }

        var refund = new ReturnRefund
        {
            ReturnId = request.ReturnId,
            RefundMethod = request.RefundMethod,
            Amount = request.Amount,
            Status = "completed",
            Details = details.Count == 0 ? null : details,
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

        @return.Status = ReturnConstants.StatusRefunded;
        @return.RefundStatus = ReturnConstants.RefundCompleted;
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
