using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Queries.GetCategories;

public record GetCategoriesQuery(Guid? ParentId = null, bool ActiveOnly = true) : IRequest<Result<List<CategoryDto>>>;

public record CategoryDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    bool IsActive,
    int SortOrder);
