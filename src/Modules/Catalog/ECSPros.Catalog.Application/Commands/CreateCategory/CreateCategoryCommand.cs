using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    string FillType = "manual",
    int SortOrder = 0) : IRequest<Result<Guid>>;
