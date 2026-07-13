using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.SetInvoiceIntegratorUrl;

// P1d: entegratör PDF adresi panelden girilir — H1 storefront "Faturayı Görüntüle"
// butonunun veri kaynağı (URL müşteriye inmez, FaturaPdfProxy üzerinden sunulur).
public record SetInvoiceIntegratorUrlCommand(
    Guid InvoiceId,
    string? IntegratorInvoiceUrl,
    Guid UpdatedBy) : IRequest<Result<bool>>;

public class SetInvoiceIntegratorUrlCommandHandler(IOrderDbContext db)
    : IRequestHandler<SetInvoiceIntegratorUrlCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetInvoiceIntegratorUrlCommand request, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null)
            return Result.Failure<bool>("Fatura bulunamadı.");

        var url = string.IsNullOrWhiteSpace(request.IntegratorInvoiceUrl)
            ? null : request.IntegratorInvoiceUrl.Trim();

        if (url is not null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return Result.Failure<bool>("Geçerli bir https adresi girilmelidir.");
        }

        invoice.IntegratorInvoiceUrl = url;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = request.UpdatedBy;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
