using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetMemberInvoicePdfSource;

/// <summary>
/// H1: Üyenin kendi faturasının entegratör PDF adresini döner — sahiplik (fatura →
/// sipariş → MemberId) burada doğrulanır; URL yalnız sunucu tarafında kullanılır,
/// hiçbir DTO ile müşteriye sızmaz (proxy endpoint'i bu sorguyla çözer).
/// </summary>
public record GetMemberInvoicePdfSourceQuery(
    Guid InvoiceId,
    Guid OrderId,
    Guid MemberId) : IRequest<Result<string>>;
