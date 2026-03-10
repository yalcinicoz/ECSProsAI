using ECSPros.Promotion.Application.Services.Engine;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Queries.CalculateDiscounts;

public record CalculateDiscountsQuery(
    List<CartLineItem> Items,
    Guid? MemberId = null) : IRequest<Result<List<DiscountLine>>>;
