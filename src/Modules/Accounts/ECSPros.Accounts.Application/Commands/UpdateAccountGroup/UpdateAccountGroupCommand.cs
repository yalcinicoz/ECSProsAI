using ECSPros.Shared.Kernel.Common;
using MediatR;
namespace ECSPros.Accounts.Application.Commands.UpdateAccountGroup;
public record UpdateAccountGroupCommand(Guid Id, string Name, string GroupType, string? Description, int SortOrder, bool IsActive) : IRequest<Result>;
