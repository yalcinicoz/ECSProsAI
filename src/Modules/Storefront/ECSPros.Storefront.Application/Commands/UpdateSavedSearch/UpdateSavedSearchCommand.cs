using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpdateSavedSearch;

/// <summary>E11: kayıtlı aramayı düzenle (ad/metin/bildirim) — sahiplik denetimli;
/// metin değişiyorsa mükerrer engeli korunur.</summary>
public record UpdateSavedSearchCommand(
    Guid SavedSearchId,
    Guid MemberId,
    string Query,
    string? Name = null,
    bool NotifyEnabled = false) : IRequest<Result>;

public class UpdateSavedSearchCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpdateSavedSearchCommand, Result>
{
    public async Task<Result> Handle(UpdateSavedSearchCommand request, CancellationToken ct)
    {
        var sorgu = (request.Query ?? string.Empty).Trim();
        if (sorgu.Length < 2)
            return Result.Failure("Arama metni en az 2 karakter olmalıdır.");
        if (sorgu.Length > 200)
            return Result.Failure("Arama metni en fazla 200 karakter olabilir.");

        var kayit = await db.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == request.SavedSearchId && s.MemberId == request.MemberId, ct);
        if (kayit is null)
            return Result.Failure("Kayıtlı arama bulunamadı.");

        var mukerrer = await db.SavedSearches.AnyAsync(s =>
            s.Id != kayit.Id
            && s.FirmPlatformId == kayit.FirmPlatformId
            && s.MemberId == request.MemberId
            && s.Query == sorgu, ct);
        if (mukerrer)
            return Result.Failure("Bu arama zaten kayıtlı.");

        kayit.Query = sorgu;
        kayit.Name = request.Name?.Trim();
        kayit.NotifyEnabled = request.NotifyEnabled;
        kayit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
