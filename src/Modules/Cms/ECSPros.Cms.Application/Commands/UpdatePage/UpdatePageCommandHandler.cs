using ECSPros.Cms.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Commands.UpdatePage;

public class UpdatePageCommandHandler : IRequestHandler<UpdatePageCommand, Result<bool>>
{
    private readonly ICmsDbContext _context;

    public UpdatePageCommandHandler(ICmsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
    {
        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (page is null)
            return Result.Failure<bool>("Sayfa bulunamadı.");

        page.NameI18n = request.NameI18n;
        page.SlugI18n = request.SlugI18n;
        page.MetaTitleI18n = request.MetaTitleI18n ?? new Dictionary<string, string>();
        page.MetaDescriptionI18n = request.MetaDescriptionI18n ?? new Dictionary<string, string>();
        page.IsActive = request.IsActive;
        page.PublishAt = request.PublishAt;
        page.UnpublishAt = request.UnpublishAt;
        page.TargetGender = request.TargetGender;
        page.UpdatedAt = DateTime.UtcNow;
        page.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
