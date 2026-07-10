using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteSavedSearch;

/// <summary>E11: kayıtlı aramayı sil (soft) — sahiplik denetimli; kayıt yoksa da
/// başarı döner (silme idempotent).</summary>
public record DeleteSavedSearchCommand(Guid SavedSearchId, Guid MemberId) : IRequest<Result>;

public class DeleteSavedSearchCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteSavedSearchCommand, Result>
{
    public async Task<Result> Handle(DeleteSavedSearchCommand request, CancellationToken ct)
    {
        var kayit = await db.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == request.SavedSearchId && s.MemberId == request.MemberId, ct);
        if (kayit is null)
            return Result.Success();

        kayit.IsDeleted = true;
        kayit.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
