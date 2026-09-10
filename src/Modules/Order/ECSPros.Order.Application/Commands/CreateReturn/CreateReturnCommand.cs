using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateReturn;

public record ReturnItemRequest(
    Guid OrderItemId,
    Guid VariantId,
    int Quantity,
    Guid ReturnReasonId,
    string? CustomerNotes);

/// <summary>Panelden müşteri iadesi (İade planı §2.4, 2026-09-10): yalnız TESLİM EDİLMİŞ siparişte açılır (E6/K8 —
/// kargodaki siparişin iadesi ancak Teslimatsız İade olabilir); tip sabit <c>customer</c>, istemciden alınmaz;
/// kalem tutarı sunucuda hesaplanır; geri ödeme uygunluğu oluşturma anında yazılır.</summary>
public record CreateReturnCommand(
    Guid OrderId,
    Guid MemberId,
    string? CustomerNotes,
    string RefundMethod,
    List<ReturnItemRequest> Items) : IRequest<Result<Guid>>;
