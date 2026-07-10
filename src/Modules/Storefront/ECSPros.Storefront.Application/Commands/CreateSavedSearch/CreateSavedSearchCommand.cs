using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CreateSavedSearch;

/// <summary>E11: arama kaydet — aynı arama metni platform başına bir kez (mükerrer
/// hata döner; soft-delete edilmiş eski kayıt varsa yeni değerlerle geri açılır).</summary>
public record CreateSavedSearchCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string Query,
    string? Name = null,
    Dictionary<string, string>? Filters = null,
    bool NotifyEnabled = false) : IRequest<Result<Guid>>;

public class CreateSavedSearchCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateSavedSearchCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSavedSearchCommand request, CancellationToken ct)
    {
        var sorgu = (request.Query ?? string.Empty).Trim();
        if (sorgu.Length < 2)
            return Result.Failure<Guid>("Arama metni en az 2 karakter olmalıdır.");
        if (sorgu.Length > 200)
            return Result.Failure<Guid>("Arama metni en fazla 200 karakter olabilir.");

        var mevcut = await db.SavedSearches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId
                                      && s.MemberId == request.MemberId
                                      && s.Query == sorgu, ct);
        if (mevcut is not null)
        {
            if (!mevcut.IsDeleted)
                return Result.Failure<Guid>("Bu arama zaten kayıtlı.");

            mevcut.IsDeleted = false;
            mevcut.DeletedAt = null;
            mevcut.Name = request.Name?.Trim();
            mevcut.Filters = request.Filters;
            mevcut.NotifyEnabled = request.NotifyEnabled;
            mevcut.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result.Success(mevcut.Id);
        }

        var kayit = new SavedSearch
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            Query = sorgu,
            Name = request.Name?.Trim(),
            Filters = request.Filters,
            NotifyEnabled = request.NotifyEnabled
        };
        db.SavedSearches.Add(kayit);
        await db.SaveChangesAsync(ct);
        return Result.Success(kayit.Id);
    }
}
