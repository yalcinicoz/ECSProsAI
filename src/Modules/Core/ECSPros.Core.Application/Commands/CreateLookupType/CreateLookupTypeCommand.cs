using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Commands.CreateLookupType;

public record CreateLookupTypeCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string? Description) : IRequest<Result<Guid>>;
