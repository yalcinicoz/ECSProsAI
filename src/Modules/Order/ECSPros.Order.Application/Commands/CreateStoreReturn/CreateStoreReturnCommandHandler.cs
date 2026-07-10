using System.Security.Cryptography;
using System.Text.Json;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateStoreReturn;

public class CreateStoreReturnCommandHandler : IRequestHandler<CreateStoreReturnCommand, Result<List<StoreReturnCreatedDto>>>
{
    private readonly IOrderDbContext _context;

    public CreateStoreReturnCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    // Türkçe karakterler \uXXXX yerine okunur yazılsın (snapshot admin/panelde de görüntülenir)
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<Result<List<StoreReturnCreatedDto>>> Handle(CreateStoreReturnCommand request, CancellationToken ct)
    {
        if (request.Items.Count == 0)
            return Result.Failure<List<StoreReturnCreatedDto>>("İade talebi için en az bir ürün seçmelisiniz.");

        var kalemIdleri = request.Items.Select(i => i.OrderItemId).Distinct().ToList();
        if (kalemIdleri.Count != request.Items.Count)
            return Result.Failure<List<StoreReturnCreatedDto>>("Aynı kalem birden fazla kez seçilemez.");

        var siparisler = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.MemberId == request.MemberId && o.Items.Any(i => kalemIdleri.Contains(i.Id)))
            .ToListAsync(ct);

        var kalemMap = siparisler
            .SelectMany(o => o.Items.Select(i => (Siparis: o, Kalem: i)))
            .Where(x => kalemIdleri.Contains(x.Kalem.Id))
            .ToDictionary(x => x.Kalem.Id);

        if (kalemMap.Count != kalemIdleri.Count)
            return Result.Failure<List<StoreReturnCreatedDto>>("Seçilen kalemlerden bazıları siparişlerinizde bulunamadı.");

        if (kalemMap.Values.Any(x => x.Siparis.Status != "delivered"))
            return Result.Failure<List<StoreReturnCreatedDto>>("İade yalnızca teslim edilmiş siparişler için oluşturulabilir.");

        // Mükerrer iade engeli: reddedilmemiş bir iadede zaten yer alan kalem yeniden iade edilemez.
        var mevcutIadeliler = await _context.Returns
            .Where(r => r.MemberId == request.MemberId && r.Status != "rejected")
            .SelectMany(r => r.Items)
            .Where(i => kalemIdleri.Contains(i.OrderItemId))
            .Select(i => i.OrderItemId)
            .ToListAsync(ct);
        if (mevcutIadeliler.Count > 0)
            return Result.Failure<List<StoreReturnCreatedDto>>("Seçilen ürünlerden bazıları için zaten bir iade talebiniz var.");

        var simdi = DateTime.UtcNow;
        var sonuclar = new List<StoreReturnCreatedDto>();

        foreach (var grup in request.Items.GroupBy(i => kalemMap[i.OrderItemId].Siparis.Id))
        {
            var siparis = kalemMap[grup.First().OrderItemId].Siparis;
            var iade = new Return
            {
                ReturnNumber = $"RET-{simdi:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                OrderId = siparis.Id,
                MemberId = request.MemberId,
                ReturnType = "return",
                Status = "requested",
                RefundMethod = "original_payment",
                RefundStatus = "pending",
                CargoReturnCode = KargoIadeKoduUret(),
                ImageUrls = request.ImageUrls ?? new List<string>()
            };

            foreach (var istek in grup)
            {
                var kalem = kalemMap[istek.OrderItemId].Kalem;
                iade.Items.Add(new ReturnItem
                {
                    OrderItemId = kalem.Id,
                    VariantId = kalem.VariantId,
                    Quantity = kalem.Quantity, // tasarımda adet seçici yok — kalemin tamamı iade edilir
                    ReturnReasonId = istek.MainReasonId,
                    // Neden seçimlerinin metin snapshot'ı — "Önceki İade Nedeni" paneli buradan okur.
                    CustomerNotes = JsonSerializer.Serialize(new
                    {
                        reasons = istek.Reasons.Select(r => new { main = r.Main, subs = r.Subs }),
                        other = istek.OtherText
                    }, SnapshotJsonOptions),
                    UnitRefundAmount = kalem.Quantity == 0 ? 0 : kalem.Total / kalem.Quantity,
                    TotalRefundAmount = kalem.Total,
                    Status = "pending"
                });
            }

            iade.RefundAmount = iade.Items.Sum(i => i.TotalRefundAmount); // beklenen tutar
            _context.Returns.Add(iade);
            sonuclar.Add(new StoreReturnCreatedDto(
                iade.Id, iade.ReturnNumber, siparis.OrderNumber, iade.CargoReturnCode!, iade.RefundAmount));
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success(sonuclar);
    }

    /// <summary>Karıştırılabilir karakterler (0/O, 1/I) olmadan 6 haneli kod — IAD-XXXXXX.</summary>
    private static string KargoIadeKoduUret()
    {
        const string harfler = "ABCDEFGHJKLMNPRSTUVYZ23456789";
        Span<char> kod = stackalloc char[6];
        for (var i = 0; i < kod.Length; i++)
            kod[i] = harfler[RandomNumberGenerator.GetInt32(harfler.Length)];
        return $"IAD-{new string(kod)}";
    }
}
