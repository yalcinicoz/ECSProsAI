using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.AddProductVideoByUrl;

/// <summary>
/// H5 (K15): URL ile ürün videosu ekleme — kullanıcının video sunucusundaki ya da dış
/// kaynaktaki https adres. Dosya yükleme (FTP/batch) akışının yanında ikinci yol; kayıt
/// doğrudan Active doğar (yüklenecek dosya yok), FileName boş kalır.
/// </summary>
public record AddProductVideoByUrlCommand(
    Guid ProductId,
    Guid ImageSetId,
    string VideoUrl,
    string? ThumbnailUrl = null,
    int SortOrder = 0) : IRequest<Result<Guid>>;

public class AddProductVideoByUrlCommandHandler(ICatalogDbContext db)
    : IRequestHandler<AddProductVideoByUrlCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddProductVideoByUrlCommand request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.VideoUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return Result.Failure<Guid>("Geçerli bir video adresi girin (http/https).");

        var urunVar = await db.Products.AnyAsync(p => p.Id == request.ProductId, ct);
        if (!urunVar) return Result.Failure<Guid>("Ürün bulunamadı.");

        var setVar = await db.ImageSets.AnyAsync(s => s.Id == request.ImageSetId, ct);
        if (!setVar) return Result.Failure<Guid>("Görsel seti bulunamadı.");

        var video = new ProductVideo
        {
            ProductId = request.ProductId,
            ImageSetId = request.ImageSetId,
            FileName = string.Empty,
            VideoUrl = request.VideoUrl.Trim(),
            ThumbnailUrl = string.IsNullOrWhiteSpace(request.ThumbnailUrl) ? null : request.ThumbnailUrl.Trim(),
            SortOrder = request.SortOrder,
            Status = ProductImageStatus.Active,
            BatchId = Guid.NewGuid()
        };
        db.ProductVideos.Add(video);
        await db.SaveChangesAsync(ct);
        return Result.Success(video.Id);
    }
}
