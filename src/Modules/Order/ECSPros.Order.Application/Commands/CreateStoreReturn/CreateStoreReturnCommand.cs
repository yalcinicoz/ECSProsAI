using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateStoreReturn;

/// <summary>
/// E8: mağazadan (Hesabım → İadelerim) iade talebi. Admin CreateReturn'den farkları:
/// üye kapsamlıdır (sipariş sahipliği denetlenir), yalnız teslim edilmiş siparişlerin
/// kalemleri kabul edilir, kalemler farklı siparişlerden gelebilir (sipariş başına bir
/// Return açılır), kargo iade kodu üretilir ve beklenen iade tutarı kalem tutarından
/// hesaplanır. Neden seçimi ana neden Lookup Id'si + metin snapshot'ı olarak saklanır
/// (alt nedenlerin Lookup'ta kimliği yok — tasarım listesi ExtraData'da).
/// </summary>
public record StoreReturnReasonGroup(string Main, List<string> Subs);

public record StoreReturnItemRequest(
    Guid OrderItemId,
    Guid MainReasonId,
    List<StoreReturnReasonGroup> Reasons,
    string? OtherText);

public record CreateStoreReturnCommand(
    Guid MemberId,
    List<StoreReturnItemRequest> Items,
    List<string>? ImageUrls) : IRequest<Result<List<StoreReturnCreatedDto>>>;

public record StoreReturnCreatedDto(
    Guid ReturnId,
    string ReturnNumber,
    string OrderNumber,
    string CargoReturnCode,
    decimal ExpectedRefundAmount);
