using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteFilterPreset;

public record DeleteFilterPresetCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteFilterPresetCommandHandler(ICatalogDbContext db)
    : IRequestHandler<DeleteFilterPresetCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteFilterPresetCommand request, CancellationToken ct)
    {
        var preset = await db.FilterPresets.FirstOrDefaultAsync(fp => fp.Id == request.Id, ct);
        if (preset is null) return Result.Failure<bool>("Filtre şablonu bulunamadı.");

        // Kategorilerde kullanılıyor mu?
        var usedCount = await db.Categories.CountAsync(c => c.FilterPresetId == request.Id, ct);
        if (usedCount > 0)
            return Result.Failure<bool>($"Bu filtre {usedCount} kategoride kullanılıyor. Önce kategorilerden kaldırın.");

        preset.IsDeleted = true;
        preset.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
