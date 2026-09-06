using ECSPros.Order.Application.Commands.CreateInvoiceSeries;
using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.UpdateInvoiceSeries;

/// <summary>Ad, açıklama ve sözleşme güncellenir. Serial ve tip değişmez (numara akışının kimliğidir).</summary>
public record UpdateInvoiceSeriesCommand(
    Guid Id, string? Name, string? Description, Guid? IntegrationContractId, Guid UpdatedBy) : IRequest<Result<bool>>;

public class UpdateInvoiceSeriesCommandHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<UpdateInvoiceSeriesCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateInvoiceSeriesCommand request, CancellationToken ct)
    {
        var series = await db.InvoiceSeries.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (series is null) return Result.Failure<bool>("Fatura serisi bulunamadı.");

        var contractError = await InvoiceSeriesContractRules.ValidateAsync(firmResolver, series.FirmId, request.IntegrationContractId, ct);
        if (contractError is not null) return Result.Failure<bool>(contractError);

        series.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        series.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        series.IntegrationContractId = request.IntegrationContractId;
        series.UpdatedAt = DateTime.UtcNow;
        series.UpdatedBy = request.UpdatedBy;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
