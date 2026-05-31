using ECSPros.Shared.Kernel.Common;
using MediatR;
namespace ECSPros.Accounts.Application.Commands.CreateAccountGroup;
public record CreateAccountGroupCommand(string Code, string Name, string GroupType, string? Description, int SortOrder) : IRequest<Result<Guid>>;
