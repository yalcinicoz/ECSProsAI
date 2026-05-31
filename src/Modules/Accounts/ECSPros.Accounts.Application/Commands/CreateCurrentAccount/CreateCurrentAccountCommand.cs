using ECSPros.Shared.Kernel.Common;
using MediatR;
namespace ECSPros.Accounts.Application.Commands.CreateCurrentAccount;
public record CreateCurrentAccountCommand(
    string Code, string Title, string AccountType, Guid? GroupId,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string Currency, string? Notes) : IRequest<Result<Guid>>;
