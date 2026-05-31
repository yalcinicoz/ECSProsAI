using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateFilterPreset;

public record UpdateFilterPresetCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string? Description,
    Dictionary<string, object> FilterDef,
    bool IsActive,
    int SortOrder) : IRequest<Result<bool>>;

public class UpdateFilterPresetCommandHandler(ICatalogDbContext db)
    : IRequestHandler<UpdateFilterPresetCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateFilterPresetCommand request, CancellationToken ct)
    {
        var preset = await db.FilterPresets.FirstOrDefaultAsync(fp => fp.Id == request.Id, ct);
        if (preset is null) return Result.Failure<bool>("Filtre şablonu bulunamadı.");

        preset.NameI18n    = request.NameI18n;
        preset.Description = request.Description;
        preset.FilterDef   = request.FilterDef;
        preset.IsActive    = request.IsActive;
        preset.SortOrder   = request.SortOrder;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
