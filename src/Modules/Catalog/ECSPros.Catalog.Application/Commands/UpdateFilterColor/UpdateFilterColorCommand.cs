using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateFilterColor;

public record UpdateFilterColorCommand(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? HexCode,
    int SortOrder,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateFilterColorCommandHandler : IRequestHandler<UpdateFilterColorCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateFilterColorCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateFilterColorCommand request, CancellationToken cancellationToken)
    {
        var color = await _db.FilterColors.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (color is null)
            return Result.Failure<bool>("Filter color not found.");

        var codeConflict = await _db.FilterColors
            .AnyAsync(c => c.Code == request.Code && c.Id != request.Id, cancellationToken);
        if (codeConflict)
            return Result.Failure<bool>($"Filter color with code '{request.Code}' already exists.");

        color.Code = request.Code;
        color.NameI18n = request.NameI18n;
        color.HexCode = request.HexCode;
        color.SortOrder = request.SortOrder;
        color.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
