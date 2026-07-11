using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeletePageBlock;

/// <summary>
/// G6: taslak bloğu öğeleriyle birlikte soft-delete eder. Yayınlanmış snapshot'lara
/// dokunmaz — silinen blok bir SONRAKİ Yayınla'da canlıdan düşer (taslak/yayın ayrımı).
/// </summary>
public record DeletePageBlockCommand(Guid Id, Guid FirmPlatformId) : IRequest<Result>;

public class DeletePageBlockCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeletePageBlockCommand, Result>
{
    public async Task<Result> Handle(DeletePageBlockCommand request, CancellationToken ct)
    {
        var blok = await db.PageBlocks
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == request.Id && b.FirmPlatformId == request.FirmPlatformId, ct);
        if (blok is null)
            return Result.Failure("Blok bulunamadı.");

        var now = DateTime.UtcNow;
        blok.IsDeleted = true;
        blok.DeletedAt = now;
        foreach (var oge in blok.Items)
        {
            oge.IsDeleted = true;
            oge.DeletedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
