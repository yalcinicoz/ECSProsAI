using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Pos.Application.Queries.GetPosSales;

public record GetPosSalesQuery(
    Guid? SessionId = null,
    Guid? RegisterId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20,
    /// <summary>DataGrid (2026-09-08): verilirse sayfa/sıralama/filtre/arama buradan (PosSaleGrid.Schema); Page/PageSize yok sayılır.</summary>
    GridRequest? Grid = null) : IRequest<Result<PagedResult<PosSaleListDto>>>;

public record PosSaleListDto(
    Guid Id,
    string SaleNumber,
    Guid SessionId,
    Guid RegisterId,
    Guid? MemberId,
    string Status,
    decimal GrandTotal,
    DateTime CreatedAt,
    string? RegisterName = null);
