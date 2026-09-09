using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetReturnDetail;

public record GetReturnDetailQuery(Guid ReturnId) : IRequest<Result<ReturnDetailDto>>;

public partial record ReturnDetailDto(
    Guid Id,
    string ReturnNumber,
    Guid OrderId,
    Guid MemberId,
    string ReturnType,
    string? CustomerNotes,
    string Status,
    string? ReturnTrackingNumber,
    DateTime? ReturnCargoSentAt,
    DateTime? ReturnCargoReceivedAt,
    string? InspectionNotes,
    DateTime? InspectionCompletedAt,
    string RefundMethod,
    string RefundStatus,
    decimal RefundAmount,
    DateTime CreatedAt,
    List<ReturnItemDto> Items,
    List<ReturnRefundDto> Refunds,
    string? CargoReturnCode = null,     // E8: kargo iade kodu
    List<string>? ImageUrls = null);    // E8: talep görselleri

// M4 (2026-09-09, mobil): iade detayının vitrin etiketi + 4 adımlı iade akışı (reddedilende boş).
public partial record ReturnDetailDto
{
    public string StatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.IadeDurumu, Status);
    public string StatusColor => DurumEtiketleri.Renk(DurumEtiketleri.Vitrin.IadeDurumu, Status);
    public string StatusVariant => DurumEtiketleri.Varyant(DurumEtiketleri.Vitrin.IadeDurumu, Status);
    public List<AkisAdimi> Timeline => DurumEtiketleri.IadeAkisi(Status);
}

public record ReturnItemDto(
    Guid Id,
    Guid OrderItemId,
    Guid VariantId,
    int Quantity,
    Guid ReturnReasonId,
    string? CustomerNotes,
    decimal UnitRefundAmount,
    decimal TotalRefundAmount,
    string Status,
    string? InspectionResult,
    string? InspectionNotes);

public record ReturnRefundDto(
    Guid Id,
    string RefundMethod,
    decimal Amount,
    string Status,
    DateTime? ProcessedAt);
