using ECSPros.Cms.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Commands.UpdateSectionContent;

// P2b: rich_text bölüm içeriği (Settings.html) panelden düzenlenir. UpdatedAt'in
// ilerlemesi sözleşme sürüm tarihini (ContentUpdatedAt) otomatik günceller.
public record UpdateSectionContentCommand(
    Guid SectionId,
    string Html,
    Guid UpdatedBy) : IRequest<Result<bool>>;

public class UpdateSectionContentCommandHandler(ICmsDbContext db)
    : IRequestHandler<UpdateSectionContentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateSectionContentCommand request, CancellationToken ct)
    {
        var section = await db.PageSections
            .Include(s => s.SectionType)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, ct);

        if (section is null)
            return Result.Failure<bool>("Bölüm bulunamadı.");
        if (section.SectionType.Code != "rich_text")
            return Result.Failure<bool>("Yalnız rich_text bölümlerinin HTML içeriği düzenlenebilir.");

        section.Settings["html"] = request.Html;
        // Dictionary içi değişikliği EF'in jsonb diff'i her zaman yakalamaz — sözleşme
        // sürüm tarihi buna bağlı olduğundan alan bilinçli olarak yeniden atanır
        section.Settings = new Dictionary<string, object>(section.Settings);
        section.UpdatedAt = DateTime.UtcNow;
        section.UpdatedBy = request.UpdatedBy;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
