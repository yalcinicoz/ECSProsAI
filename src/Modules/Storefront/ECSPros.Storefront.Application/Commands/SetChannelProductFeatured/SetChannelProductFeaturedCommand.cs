using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SetChannelProductFeatured;

/// <summary>
/// B11 (K8): kanal ürününe tarih aralıklı "öne çıkar" bayrağı atar/kaldırır.
/// FeaturedFrom null gönderilirse bayrak kaldırılır. ChannelProduct satırı yoksa
/// oluşturulur (upsert) — platformda satır olmayan ürün de öne çıkarılabilsin.
/// </summary>
public record SetChannelProductFeaturedCommand(
    Guid FirmPlatformId,
    Guid ProductId,
    DateTime? FeaturedFrom,
    DateTime? FeaturedUntil) : IRequest<Result<Guid>>;

public class SetChannelProductFeaturedCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SetChannelProductFeaturedCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SetChannelProductFeaturedCommand request, CancellationToken ct)
    {
        if (request.FeaturedFrom.HasValue && request.FeaturedUntil.HasValue
            && request.FeaturedUntil.Value < request.FeaturedFrom.Value)
            return Result.Failure<Guid>("Bitiş tarihi başlangıçtan önce olamaz.");

        var kayit = await db.ChannelProducts.FirstOrDefaultAsync(
            p => p.FirmPlatformId == request.FirmPlatformId && p.ProductId == request.ProductId, ct);

        if (kayit is null)
        {
            kayit = new ChannelProduct
            {
                FirmPlatformId = request.FirmPlatformId,
                ProductId = request.ProductId
            };
            db.ChannelProducts.Add(kayit);
        }

        kayit.FeaturedFrom = request.FeaturedFrom?.ToUniversalTime();
        kayit.FeaturedUntil = request.FeaturedUntil?.ToUniversalTime();
        kayit.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(kayit.Id);
    }
}
