using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.ResendInvoice;

/// <summary>Fatura için yeni "send" işi açar (bekleyen iş yoksa). Kanal yöntemi integrator_api olmalı, fatura iptal olmamalı.</summary>
public record ResendInvoiceCommand(Guid InvoiceId, Guid UserId) : IRequest<Result<Guid>>;

public class ResendInvoiceCommandHandler(IOrderDbContext db) : IRequestHandler<ResendInvoiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ResendInvoiceCommand request, CancellationToken ct)
    {
        var inv = await db.Invoices.Include(i => i.Dispatches).FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (inv is null) return Result.Failure<Guid>("Fatura bulunamadı.");
        if (inv.Status == "cancelled") return Result.Failure<Guid>("İptal edilmiş fatura gönderilmez.");
        if (inv.NumberSource != InvoiceNumberSources.Internal) return Result.Failure<Guid>("Dış numaralı fatura bizden gönderilmez.");
        var contractId = inv.IntegrationContractId ?? await db.InvoiceSeries.AsNoTracking()
            .Where(s => s.Id == inv.InvoiceSeriesId).Select(s => s.IntegrationContractId).FirstOrDefaultAsync(ct);
        if (contractId is null) return Result.Failure<Guid>("Faturanın serisine bağlı entegratör sözleşmesi yok.");
        if (inv.Dispatches.Any(d => d.Action == InvoiceDispatchActions.Send && d.Status == InvoiceDispatchStatuses.Pending))
            return Result.Failure<Guid>("Bekleyen bir gönderim işi zaten var.");
        if (inv.IntegratorStatus is "sent" or "accepted") return Result.Failure<Guid>("Fatura entegratöre zaten gönderilmiş.");

        var d = new InvoiceDispatch
        {
            InvoiceId = inv.Id, Action = InvoiceDispatchActions.Send, Status = InvoiceDispatchStatuses.Pending,
            IntegrationContractId = contractId, NextAttemptAt = DateTime.UtcNow, CreatedBy = request.UserId
        };
        inv.IntegrationContractId ??= contractId;
        inv.SendMethod = InvoiceSendMethods.IntegratorApi;
        inv.IntegratorStatus = "queued";
        db.InvoiceDispatches.Add(d);
        await db.SaveChangesAsync(ct);
        return Result.Success(d.Id);
    }
}
