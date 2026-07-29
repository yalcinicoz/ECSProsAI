using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateInvoice;

public record CreateInvoiceCommand(
    Guid OrderId,
    Guid InvoiceSeriesId,
    string InvoiceType,
    DateTime InvoiceDate,
    string RecipientName,
    string RecipientAddress,
    string? RecipientTaxOffice,
    string? RecipientTaxNumber,
    string? RecipientCompanyName,
    Guid CreatedBy,
    // Paket başına fatura normal akıştır (karar 2026-07-19); null = sipariş geneli
    // fatura (istisna/eski akış)
    Guid? PackageId = null) : IRequest<Result<Guid>>;
