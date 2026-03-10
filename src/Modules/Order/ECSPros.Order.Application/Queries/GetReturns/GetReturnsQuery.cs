using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetReturns;

public record GetReturnsQuery(
    Guid? OrderId = null,
    Guid? MemberId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ReturnListDto>>>;

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
    DateTime CreatedAt);
