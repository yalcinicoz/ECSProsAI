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
    Guid CreatedBy) : IRequest<Result<Guid>>;
