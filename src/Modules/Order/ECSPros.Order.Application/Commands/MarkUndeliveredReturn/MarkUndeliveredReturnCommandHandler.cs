using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.MarkUndeliveredReturn;

public class MarkUndeliveredReturnCommandHandler : IRequestHandler<MarkUndeliveredReturnCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;
    private readonly IPublisher _publisher;
    private readonly IChannelCapabilityResolver _kanal;

    public MarkUndeliveredReturnCommandHandler(IOrderDbContext context, IPublisher publisher, IChannelCapabilityResolver kanal)
    {
        _context = context;
        _publisher = publisher;
        _kanal = kanal;
    }

    public async Task<Result<Guid>> Handle(MarkUndeliveredReturnCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<Guid>("Teslimatsız İade için neden zorunludur.");

        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .Include(o => o.Invoices)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);

        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        // ── Ön koşul (R3): kargoya verilmiş VEYA (işlemde && iptal edilmemiş fatura var && gönderi yok) ──
        var aktifFaturalar = order.Invoices.Where(i => i.Status != "cancelled").ToList();
        var gonderiler = order.Shipments.ToList();
        var kargoyaVerildi = order.Status == "shipped";
        if (!kargoyaVerildi)
        {
            if (order.Status != "processing")
                return Result.Failure<Guid>($"'{order.Status}' durumundaki sipariş için Teslimatsız İade uygulanamaz; yalnız kargoya verilmiş ya da faturası kesilmiş siparişte olur.");
            if (aktifFaturalar.Count == 0)
                return Result.Failure<Guid>("Faturası kesilmemiş sipariş için iade yoktur — siparişi İptal Et ile iptal edin.");
            if (gonderiler.Any(s => s.Status != ReturnConstants.ShipmentReturnedToSender))
                return Result.Failure<Guid>("Siparişin gönderisi var; önce kargoya veriş tamamlanmalı (durum 'Kargoda' olmalı).");
        }

        // Aynı sipariş için açık teslimatsız iade varsa ikinciyi açma (idempotent).
        var mevcut = await _context.Returns.AsNoTracking()
            .AnyAsync(r => r.OrderId == order.Id && r.ReturnType == ReturnConstants.TypeUndelivered, ct);
        if (mevcut)
            return Result.Failure<Guid>("Bu sipariş için zaten bir Teslimatsız İade kaydı var.");

        try
        {
            order.MarkUndeliveredReturn(request.UpdatedBy, request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        var now = DateTime.UtcNow;

        // ── Gönderi: kargoya verilmişse returned_to_sender ──
        foreach (var s in gonderiler.Where(s => s.Status == "shipped"))
        {
            s.Status = ReturnConstants.ShipmentReturnedToSender;
            s.ReturnedAt = now;
            s.UpdatedAt = now;
            s.UpdatedBy = request.UpdatedBy;
        }

        // ── Fatura: K3 — mal müşteriye hiç geçmedi → iptal (entegratöre iptal kuyruklanır) ──
        foreach (var f in aktifFaturalar)
            FaturaIptal.Uygula(_context, f, request.UpdatedBy);

        // ── İade kaydı: tüm kalemler, onaylı, tip undelivered ──
        var vadeFarkiPaylari = TaksitKurali.KalemPaylari(order.InstallmentFee, order.Items.Select(i => (i.Id, i.Total)));
        var iade = new Return
        {
            ReturnNumber = $"RET-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            OrderId = order.Id,
            MemberId = order.MemberId ?? Guid.Empty,   // misafir siparişi: üye yok
            ReturnType = ReturnConstants.TypeUndelivered,
            CustomerNotes = request.Notes,
            Status = ReturnConstants.StatusApproved,
            RefundMethod = ReturnConstants.RefundMethodFor(order.PaymentMethod),
            RefundStatus = ReturnConstants.RefundPending,
            CreatedBy = request.UpdatedBy
        };
        foreach (var kalem in order.Items)
        {
            var (birim, toplam) = IadeOdemeDegerlendirme.KalemTutari(kalem, kalem.Quantity, vadeFarkiPaylari.GetValueOrDefault(kalem.Id));
            iade.Items.Add(new ReturnItem
            {
                OrderItemId = kalem.Id,
                VariantId = kalem.VariantId,
                Quantity = kalem.Quantity,
                ReturnReasonId = ReturnConstants.UndeliveredReasonId,
                UnitRefundAmount = birim,
                TotalRefundAmount = toplam,
                Status = "pending",
                // Kargoya verilmemişse stok yalnız rezerveydi: rezervasyon şimdi serbest bırakılır (olay),
                // "teslim al" adımı bu kalemler için stok girişi YAPMAZ (çift sayım).
                StockAlreadyIn = !kargoyaVerildi,
                CreatedBy = request.UpdatedBy
            });
        }

        // ── Geri ödeme uygunluğu (§2.5). K5: teslimatsız iadede TAHSİL EDİLENİN TAMAMI (kargo dahil) ──
        var sonuc = await IadeOdemeDegerlendirme.DegerlendirAsync(_context, _kanal, order, null, ct);
        iade.RefundAmount = sonuc.Uygun ? sonuc.UstSinir : 0m;
        IadeOdemeDegerlendirme.Uygula(iade, sonuc);

        _context.Returns.Add(iade);

        await using (var tx = await _context.BeginTransactionAsync(ct))
        {
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // Olaylar kayıttan SONRA (Inventory kendi context'iyle rezervasyonu serbest bırakır — wasShipped=false).
        foreach (var domainEvent in order.DomainEvents)
            await _publisher.Publish(domainEvent, ct);
        order.ClearDomainEvents();

        return Result.Success(iade.Id);
    }
}
