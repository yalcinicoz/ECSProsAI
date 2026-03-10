using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.UpdateCategory;

public record UpdateCategoryCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    string FillType,
    bool IsActive,
    int SortOrder,
    Guid UpdatedBy) : IRequest<Result<bool>>;
