using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.MarkUndeliveredReturn;

/// <summary>
/// Teslimatsız İade (İade planı §2.3, 2026-09-10): kargoya verilmiş ya da faturalı-kargosuz siparişi
/// <c>returned</c> yapar, gönderiyi <c>returned_to_sender</c> işaretler, tüm kalemlerle onaylı bir iade
/// kaydı açar (talep/onay adımı yok — şirket başlatır), faturayı iptal eder (K3) ve geri ödeme
/// uygunluğunu <c>IadeOdemeKurali</c> ile yazar. Sipariş bütünü içindir (K1; paket bazlı v2).
/// Döner: açılan iade Id'si.
/// </summary>
public record MarkUndeliveredReturnCommand(
    Guid OrderId,
    Guid UpdatedBy,
    string Reason,
    string? Notes = null) : IRequest<Result<Guid>>;
