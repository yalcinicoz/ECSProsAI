using ECSPros.Shared.Kernel.Common;
using MediatR;
namespace ECSPros.Accounts.Application.Commands.UpdateCurrentAccount;
public record UpdateCurrentAccountCommand(
    Guid Id, string Title, string AccountType, Guid? GroupId,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string Currency, string? Notes, bool IsActive) : IRequest<Result>;
