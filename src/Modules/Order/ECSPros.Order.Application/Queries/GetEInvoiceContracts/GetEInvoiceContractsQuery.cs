using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetEInvoiceContracts;

/// <summary>Firma bazlı e-fatura entegratör sözleşmeleri (seri formu seçicisi). Kimlik değerleri dönmez.</summary>
public record GetEInvoiceContractsQuery(Guid? FirmId = null) : IRequest<Result<List<EInvoiceContractDto>>>;

public record EInvoiceContractDto(Guid Id, Guid FirmId, string ServiceCode, string? Name, bool IsActive, string Status, bool TestMode, bool FirmWide);

public class GetEInvoiceContractsQueryHandler(IFirmResolver firmResolver)
    : IRequestHandler<GetEInvoiceContractsQuery, Result<List<EInvoiceContractDto>>>
{
    public async Task<Result<List<EInvoiceContractDto>>> Handle(GetEInvoiceContractsQuery request, CancellationToken ct)
    {
        var rows = await firmResolver.GetEInvoiceContractsAsync(request.FirmId, ct);
        return Result.Success(rows.Select(r => new EInvoiceContractDto(
            r.Id, r.FirmId, r.ServiceCode, r.Name, r.IsActive, r.Status, r.TestMode, r.FirmPlatformId is null)).ToList());
    }
}
