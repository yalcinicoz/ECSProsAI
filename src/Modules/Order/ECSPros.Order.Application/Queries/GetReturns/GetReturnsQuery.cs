using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetReturns;

public record GetReturnsQuery(
    Guid? OrderId = null,
    Guid? MemberId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<ReturnListDto>>>;
    // Search/Grid (2026-09-08, DataGrid F4): global arama + beyaz listeli f.* filtreleri + sort/dir (ReturnGrid.Schema)

public record ReturnListDto(
    Guid Id,
    string ReturnNumber,
    Guid OrderId,
    Guid MemberId,
    string ReturnType,
    string Status,
    string RefundMethod,
    string RefundStatus,
    decimal RefundAmount,
    DateTime CreatedAt,
    string? CargoReturnCode = null,
    string? OrderNumber = null,
    string? RefundNotApplicableReason = null)
{
    /// <summary>Vitrin iade tipi etiketi (İadelerim: teslimatsız iade satırı "Teslim Edilemedi").</summary>
    public string ReturnTypeLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.IadeTipi, ReturnType);
    public bool RefundApplicable => RefundStatus != "not_applicable";
    // M4 (2026-09-09, mobil): "İadelerim" listesinin vitrin etiketi.
    public string StatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.IadeDurumu, Status);
    public string StatusColor => DurumEtiketleri.Renk(DurumEtiketleri.Vitrin.IadeDurumu, Status);
    public string StatusVariant => DurumEtiketleri.Varyant(DurumEtiketleri.Vitrin.IadeDurumu, Status);
}
