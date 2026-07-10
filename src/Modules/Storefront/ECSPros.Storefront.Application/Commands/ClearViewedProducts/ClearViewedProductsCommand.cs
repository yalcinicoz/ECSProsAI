using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.ClearViewedProducts;

/// <summary>E12: "Listeyi Temizle" — üyenin platformdaki tüm gezme kayıtları soft silinir.</summary>
public record ClearViewedProductsCommand(Guid FirmPlatformId, Guid MemberId) : IRequest<Result>;

public class ClearViewedProductsCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<ClearViewedProductsCommand, Result>
{
    public async Task<Result> Handle(ClearViewedProductsCommand request, CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;
        var kayitlar = await db.ViewedProducts
            .Where(v => v.FirmPlatformId == request.FirmPlatformId && v.MemberId == request.MemberId)
            .ToListAsync(ct);
        foreach (var kayit in kayitlar)
        {
            kayit.IsDeleted = true;
            kayit.DeletedAt = simdi;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
