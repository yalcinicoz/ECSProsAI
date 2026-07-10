using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SubscribeNewsletter;

/// <summary>F4: bülten aboneliği — idempotent (kayıtlıysa başarı döner, soft-silinen/
/// pasif kayıt yeniden aktive edilir). Misafir de kaydolabilir.</summary>
public record SubscribeNewsletterCommand(
    Guid FirmPlatformId,
    string Email,
    Guid? MemberId = null) : IRequest<Result>;

public class SubscribeNewsletterCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SubscribeNewsletterCommand, Result>
{
    public async Task<Result> Handle(SubscribeNewsletterCommand request, CancellationToken ct)
    {
        var eposta = (request.Email ?? "").Trim().ToLowerInvariant();
        if (eposta.Length < 6 || eposta.Length > 200 || !eposta.Contains('@') || !eposta.Contains('.'))
            return Result.Failure("Geçerli bir e-posta adresi yazın.");

        var mevcut = await db.NewsletterSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.FirmPlatformId == request.FirmPlatformId && n.Email == eposta, ct);
        if (mevcut is not null)
        {
            if (mevcut.IsDeleted || !mevcut.IsActive)
            {
                mevcut.IsDeleted = false;
                mevcut.DeletedAt = null;
                mevcut.IsActive = true;
                mevcut.MemberId ??= request.MemberId;
                mevcut.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return Result.Success();
        }

        db.NewsletterSubscriptions.Add(new NewsletterSubscription
        {
            FirmPlatformId = request.FirmPlatformId,
            Email = eposta,
            MemberId = request.MemberId
        });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
