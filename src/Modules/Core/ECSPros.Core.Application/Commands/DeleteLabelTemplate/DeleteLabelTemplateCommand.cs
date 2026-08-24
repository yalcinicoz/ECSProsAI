using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.DeleteLabelTemplate;

public record DeleteLabelTemplateCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteLabelTemplateCommandHandler(ICoreDbContext db)
    : IRequestHandler<DeleteLabelTemplateCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteLabelTemplateCommand request, CancellationToken ct)
    {
        var t = await db.LabelTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return Result.Failure<bool>("Şablon bulunamadı.");
        t.IsDeleted = true;
        t.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
