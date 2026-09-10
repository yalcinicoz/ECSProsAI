using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateReturn;

public class CreateReturnCommandHandler : IRequestHandler<CreateReturnCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;
    private readonly IChannelCapabilityResolver _kanal;

    public CreateReturnCommandHandler(IOrderDbContext context, IChannelCapabilityResolver kanal)
    {
        _context = context;
        _kanal = kanal;
    }

    public async Task<Result<Guid>> Handle(CreateReturnCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        // İade planı R1/R5 (E6 kapandı): müşteri iadesi yalnız teslim edilmiş siparişte; kargodaki sipariş
        // müşteriye ulaşmadıysa operasyon Teslimatsız İade kullanır.
        if (order.Status != "delivered")
            return Result.Failure<Guid>("Müşteri iadesi yalnızca teslim edilmiş siparişler için açılabilir; kargodaki sipariş için Teslimatsız İade kullanın.");

        if (!request.Items.Any())
            return Result.Failure<Guid>("İade en az bir kalem içermelidir.");

        var kalemler = order.Items.ToDictionary(i => i.Id);
        foreach (var item in request.Items)
        {
            if (!kalemler.TryGetValue(item.OrderItemId, out var kalem))
                return Result.Failure<Guid>("İade kalemi siparişte bulunamadı.");
            if (item.Quantity <= 0 || item.Quantity > kalem.Quantity)
                return Result.Failure<Guid>($"'{kalem.ProductName}' için iade adedi 1-{kalem.Quantity} arasında olmalıdır.");
        }

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var returnNumber = $"RET-{now:yyyyMMdd}-{suffix}";

        // Vade farkı payı (2026-09-10): kalem tutarı oranında (TaksitKurali.KalemPaylari — tek kural).
        var vadeFarkiPaylari = TaksitKurali.KalemPaylari(order.InstallmentFee, order.Items.Select(i => (i.Id, i.Total)));

        var @return = new Return
        {
            ReturnNumber = returnNumber,
            OrderId = request.OrderId,
            MemberId = request.MemberId,
            ReturnType = ReturnConstants.TypeCustomer,
            CustomerNotes = request.CustomerNotes,
            Status = ReturnConstants.StatusRequested,
            RefundMethod = string.IsNullOrWhiteSpace(request.RefundMethod) ? ReturnConstants.RefundMethodFor(order.PaymentMethod) : request.RefundMethod,
            RefundStatus = ReturnConstants.RefundPending,
            RefundAmount = 0
        };

        foreach (var item in request.Items)
        {
            var kalem = kalemler[item.OrderItemId];
            var (birim, toplam) = IadeOdemeDegerlendirme.KalemTutari(kalem, item.Quantity, vadeFarkiPaylari.GetValueOrDefault(kalem.Id));
            @return.Items.Add(new ReturnItem
            {
                OrderItemId = item.OrderItemId,
                VariantId = kalem.VariantId,
                Quantity = item.Quantity,
                ReturnReasonId = item.ReturnReasonId,
                CustomerNotes = item.CustomerNotes,
                UnitRefundAmount = birim,
                TotalRefundAmount = toplam,
                Status = "pending"
            });
        }

        // K5: müşteri iadesinde yalnız ürün kalemleri (kargo ücreti iade edilmez); üst sınır tahsil edilen.
        @return.RefundAmount = @return.Items.Sum(i => i.TotalRefundAmount);
        var sonuc = await IadeOdemeDegerlendirme.DegerlendirAsync(_context, _kanal, order, null, cancellationToken);
        IadeOdemeDegerlendirme.Uygula(@return, sonuc);

        _context.Returns.Add(@return);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(@return.Id);
    }
}
