using ECSPros.Cms.Application.Services;
using ECSPros.Cms.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Commands.ManageSectionItems;

// P2b: SSS (faq) soru/cevap öğeleri panelden yönetilir.
// TitleI18n = soru, DescriptionI18n = cevap (GetStoreFaq sözleşmesi).

public record CreateSectionItemCommand(
    Guid SectionId,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string> DescriptionI18n,
    int SortOrder,
    Guid CreatedBy) : IRequest<Result<Guid>>;

public record UpdateSectionItemCommand(
    Guid ItemId,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string> DescriptionI18n,
    int SortOrder,
    bool IsActive,
    Guid UpdatedBy) : IRequest<Result<bool>>;

public record DeleteSectionItemCommand(Guid ItemId, Guid DeletedBy) : IRequest<Result<bool>>;

public class CreateSectionItemCommandHandler(ICmsDbContext db)
    : IRequestHandler<CreateSectionItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSectionItemCommand request, CancellationToken ct)
    {
        var section = await db.PageSections
            .Include(s => s.SectionType)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, ct);
        if (section is null)
            return Result.Failure<Guid>("Bölüm bulunamadı.");

        var item = new PageSectionItem
        {
            SectionId = request.SectionId,
            ItemType = section.SectionType.Code,
            TitleI18n = request.TitleI18n,
            DescriptionI18n = request.DescriptionI18n,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedBy = request.CreatedBy
        };

        // Tracked parent koleksiyonuna değil DbSet'e eklenir (EF Modified tuzağı)
        db.PageSectionItems.Add(item);
        section.UpdatedAt = DateTime.UtcNow; // sürüm tarihi ilerlesin
        await db.SaveChangesAsync(ct);
        return Result.Success(item.Id);
    }
}

public class UpdateSectionItemCommandHandler(ICmsDbContext db)
    : IRequestHandler<UpdateSectionItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateSectionItemCommand request, CancellationToken ct)
    {
        var item = await db.PageSectionItems.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct);
        if (item is null)
            return Result.Failure<bool>("Öğe bulunamadı.");

        item.TitleI18n = request.TitleI18n;
        item.DescriptionI18n = request.DescriptionI18n;
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = request.UpdatedBy;

        await TouchSection(db, item.SectionId, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    internal static async Task TouchSection(ICmsDbContext db, Guid sectionId, CancellationToken ct)
    {
        var section = await db.PageSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is not null) section.UpdatedAt = DateTime.UtcNow;
    }
}

public class DeleteSectionItemCommandHandler(ICmsDbContext db)
    : IRequestHandler<DeleteSectionItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteSectionItemCommand request, CancellationToken ct)
    {
        var item = await db.PageSectionItems.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct);
        if (item is null)
            return Result.Failure<bool>("Öğe bulunamadı.");

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = request.DeletedBy;

        await UpdateSectionItemCommandHandler.TouchSection(db, item.SectionId, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
