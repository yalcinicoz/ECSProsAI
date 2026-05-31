using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateFilterPreset;

public record CreateFilterPresetCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string? Description,
    Dictionary<string, object> FilterDef,
    int SortOrder = 0) : IRequest<Result<Guid>>;

public class CreateFilterPresetCommandHandler(ICatalogDbContext db)
    : IRequestHandler<CreateFilterPresetCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFilterPresetCommand request, CancellationToken ct)
    {
        if (await db.FilterPresets.AnyAsync(fp => fp.Code == request.Code, ct))
            return Result.Failure<Guid>($"'{request.Code}' kodu zaten mevcut.");

        var preset = new FilterPreset
        {
            Id          = Guid.NewGuid(),
            Code        = request.Code,
            NameI18n    = request.NameI18n,
            Description = request.Description,
            FilterDef   = request.FilterDef,
            SortOrder   = request.SortOrder,
            IsActive    = true,
        };

        db.FilterPresets.Add(preset);
        await db.SaveChangesAsync(ct);
        return Result.Success(preset.Id);
    }
}
