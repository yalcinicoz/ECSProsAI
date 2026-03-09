using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Queries.GetPaymentMethods;

public record GetPaymentMethodsQuery(bool ActiveOnly = true) : IRequest<Result<List<PaymentMethodDto>>>;

public record PaymentMethodDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsOnline,
    bool RequiresConfirmation,
    bool IsActive,
    int SortOrder);
