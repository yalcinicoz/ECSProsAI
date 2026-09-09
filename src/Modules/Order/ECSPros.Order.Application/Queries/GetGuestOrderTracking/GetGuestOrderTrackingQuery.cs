using ECSPros.Shared.Contracts;
using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetGuestOrderTracking;

/// <summary>
/// C2 (2026-09-07, mobil/web): üyeliksiz sipariş takibi — sipariş numarası + alıcı TELEFONU ile
/// (siparişte e-posta kolonu yok; misafir kimliği ShippingRecipientPhone'dadır). Telefon yalnız rakamlarla,
/// son 10 hane üzerinden karşılaştırılır (0/+90 farkları elenir). Yanıt sınırlı: kişisel adres/fatura verisi
/// dönmez; kalemler, ödeme/sipariş durumu ve kargo hareketleri döner. Anonim uç oran sınırına tabidir.
/// </summary>
public record GetGuestOrderTrackingQuery(Guid FirmPlatformId, string OrderNumber, string Phone)
    : IRequest<Result<GuestOrderTrackingDto>>;

public partial record GuestOrderTrackingDto(
    Guid OrderId,
    string OrderNumber,
    string Status,
    string PaymentStatus,
    string? PaymentMethod,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    decimal GrandTotal,
    string CurrencyCode,
    string RecipientNameMasked,
    string? RequestedCargoName,
    List<GuestOrderTrackingItemDto> Items,
    List<GuestOrderShipmentDto> Shipments);

// M4 (2026-09-09, mobil): misafir takibinde de vitrin etiketleri + akış şeridi. Aksiyon bayrağı YOK —
// misafir iptal/iade/yorum yapamaz (bu uçlar üyelik ister), bilinçli olarak eklenmedi.
public partial record GuestOrderTrackingDto
{
    public string StatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, Status);
    public string StatusColor => DurumEtiketleri.Renk(DurumEtiketleri.Vitrin.SiparisDurumu, Status);
    public string StatusVariant => DurumEtiketleri.Varyant(DurumEtiketleri.Vitrin.SiparisDurumu, Status);
    public string PaymentStatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.OdemeDurumu, PaymentStatus);
    public string PaymentMethodLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.OdemeYontemi, PaymentMethod);
    public List<AkisAdimi> Timeline => DurumEtiketleri.SiparisAkisi(Status);
}

public record GuestOrderTrackingItemDto(
    Guid VariantId,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal Total,
    string Status,
    string? ProductCode = null,
    string? ImageUrl = null);

public record GuestOrderShipmentDto(
    Guid Id,
    string? CarrierName,
    string? TrackingNumber,
    string? TrackingUrl,
    string Status,
    DateOnly? EstimatedDeliveryDate,
    DateTime? DeliveredAt,
    List<GuestOrderShipmentEventDto> Events);

public record GuestOrderShipmentEventDto(string Code, string Description, string? Location, DateTime Date);

public class GetGuestOrderTrackingQueryHandler(IOrderDbContext db, ECSPros.Shared.Contracts.IProductService productService)
    : IRequestHandler<GetGuestOrderTrackingQuery, Result<GuestOrderTrackingDto>>
{
    private static string Rakamlar(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
    private static string Son10(string s) => s.Length > 10 ? s[^10..] : s;

    public async Task<Result<GuestOrderTrackingDto>> Handle(GetGuestOrderTrackingQuery request, CancellationToken ct)
    {
        var no = request.OrderNumber.Trim();
        var tel = Son10(Rakamlar(request.Phone));
        if (no.Length == 0 || tel.Length < 7)
            return Result.Failure<GuestOrderTrackingDto>("Sipariş numarası ve telefon gerekli.");

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Shipments).ThenInclude(s => s.Events)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.FirmPlatformId == request.FirmPlatformId && o.OrderNumber == no, ct);

        // Bulunamayan ve telefonu uyuşmayan için AYNI mesaj — numara taraması bilgi sızdırmasın.
        if (order is null || Son10(Rakamlar(order.ShippingRecipientPhone)) != tel)
            return Result.Failure<GuestOrderTrackingDto>("Bu bilgilerle eşleşen sipariş bulunamadı.");

        var gosterim = new Dictionary<Guid, ECSPros.Shared.Contracts.VariantDisplayInfo>();
        try { gosterim = await productService.GetVariantDisplayAsync(order.Items.Select(i => i.VariantId).Distinct().ToList(), ct); }
        catch { /* zenginleştirme isteğe bağlı */ }

        static string Maskele(string ad) => string.Join(" ", ad.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Length <= 1 ? p : p[0] + new string('*', Math.Min(3, p.Length - 1))));

        var dto = new GuestOrderTrackingDto(
            order.Id, order.OrderNumber, order.Status, order.PaymentStatus, order.PaymentMethod,
            order.CreatedAt, order.ConfirmedAt, order.GrandTotal, order.CurrencyCode,
            Maskele(order.ShippingRecipientName), order.RequestedCargoName,
            order.Items.Select(i =>
            {
                gosterim.TryGetValue(i.VariantId, out var g);
                return new GuestOrderTrackingItemDto(i.VariantId, i.ProductName, i.VariantInfo, i.Quantity, i.Total, i.Status,
                    g?.ProductCode, g?.ImageUrl);
            }).ToList(),
            order.Shipments.Where(s => !s.IsDeleted).OrderBy(s => s.CreatedAt).Select(s => new GuestOrderShipmentDto(
                s.Id, s.CarrierName, s.TrackingNumber, s.TrackingUrl, s.Status, s.EstimatedDeliveryDate, s.DeliveredAt,
                s.Events.OrderByDescending(e => e.EventDate).Take(20)
                    .Select(e => new GuestOrderShipmentEventDto(e.EventCode, e.EventDescription, e.EventLocation, e.EventDate)).ToList()))
                .ToList());
        return Result.Success(dto);
    }
}
