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
    string? CargoReturnCode = null); // E8: kargo iade kodu
