using ECSPros.Cms.Application.Services;
using ECSPros.Cms.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Commands.CreatePage;

public class CreatePageCommandHandler : IRequestHandler<CreatePageCommand, Result<Guid>>
{
    private readonly ICmsDbContext _context;

    public CreatePageCommandHandler(ICmsDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreatePageCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Pages.AnyAsync(
            p => p.Code == request.Code && p.FirmPlatformId == request.FirmPlatformId,
            cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' sayfa kodu bu platform için zaten mevcut.");

        var templateExists = await _context.PageTemplates.AnyAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (!templateExists)
            return Result.Failure<Guid>("Sayfa şablonu bulunamadı.");

        var page = new Page
        {
            FirmPlatformId = request.FirmPlatformId,
            TemplateId = request.TemplateId,
            Code = request.Code,
            NameI18n = request.NameI18n,
            SlugI18n = request.SlugI18n,
            PageType = request.PageType,
            TargetGender = request.TargetGender,
            TargetCategoryId = request.TargetCategoryId,
            PublishAt = request.PublishAt,
            UnpublishAt = request.UnpublishAt,
            IsActive = true
        };

        _context.Pages.Add(page);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(page.Id);
    }
}
