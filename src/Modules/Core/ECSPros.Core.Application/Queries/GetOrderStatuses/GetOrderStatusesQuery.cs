using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Queries.GetOrderStatuses;

public record GetOrderStatusesQuery(bool ActiveOnly = true) : IRequest<Result<List<OrderStatusDto>>>;

public record OrderStatusDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Color,
    bool IsActive,
    int SortOrder);
