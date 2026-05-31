using ECSPros.Catalog.Application.Commands.ArchiveProductImage;
using ECSPros.Catalog.Application.Commands.ArchiveProductVideo;
using ECSPros.Catalog.Application.Commands.ConfirmImageBatch;
using ECSPros.Catalog.Application.Commands.ConfirmVideoBatch;
using ECSPros.Catalog.Application.Commands.CreateImageSet;
using ECSPros.Catalog.Application.Commands.DeleteProductImageSetMapping;
using ECSPros.Catalog.Application.Commands.PrepareImageBatch;
using ECSPros.Catalog.Application.Commands.PrepareVideoBatch;
using ECSPros.Catalog.Application.Commands.RestoreImageBatch;
using ECSPros.Catalog.Application.Commands.RestoreVideoBatch;
using ECSPros.Catalog.Application.Commands.UpdateImageSet;
using ECSPros.Catalog.Application.Commands.UpdateProductImageMetadata;
using ECSPros.Catalog.Application.Commands.UpdateProductVideoMetadata;
using ECSPros.Catalog.Application.Commands.UpsertProductImageSetMapping;
using ECSPros.Catalog.Application.Queries.GetImageSets;
using ECSPros.Catalog.Application.Queries.GetProductImageArchive;
using ECSPros.Catalog.Application.Queries.GetProductImageCoverageReport;
using ECSPros.Catalog.Application.Queries.GetProductImages;
using ECSPros.Catalog.Application.Queries.GetProductImageSetMappings;
using ECSPros.Catalog.Application.Queries.GetProductVideoArchive;
using ECSPros.Catalog.Application.Queries.GetProductVideos;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public class ProductImageController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IImageUploadService _imageUploadService;
    private readonly IVideoUploadService _videoUploadService;

    public ProductImageController(IMediator mediator, IImageUploadService imageUploadService, IVideoUploadService videoUploadService)
    {
        _mediator = mediator;
        _imageUploadService = imageUploadService;
        _videoUploadService = videoUploadService;
    }

    // ─── Image Sets ────────────────────────────────────────────────────────────

    [HttpGet("image-sets")]
    public async Task<IActionResult> GetImageSets([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetImageSetsQuery(activeOnly), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("image-sets")]
    public async Task<IActionResult> CreateImageSet([FromBody] CreateImageSetCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPut("image-sets/{id:guid}")]
    public async Task<IActionResult> UpdateImageSet(Guid id, [FromBody] UpdateImageSetRequest request, CancellationToken ct = default)
    {
        var command = new UpdateImageSetCommand(id, request.Name, request.IsDefault, request.FallbackSetId, request.SortPriority, request.IsActive);
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Product Images ────────────────────────────────────────────────────────

    [HttpGet("products/{productId:guid}/images")]
    public async Task<IActionResult> GetProductImages(
        Guid productId,
        [FromQuery] Guid? imageSetId,
        [FromQuery] Guid? variantId,
        [FromQuery] bool applyFallback = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductImagesQuery(productId, imageSetId, variantId, applyFallback), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("products/{productId:guid}/images/prepare")]
    public async Task<IActionResult> PrepareImageBatch(Guid productId, [FromBody] PrepareImageBatchRequest request, CancellationToken ct = default)
    {
        var command = new PrepareImageBatchCommand(productId, request.VariantId, request.ImageSetId, request.FileExtensions, request.ReplaceSet);
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("upload/{batchId:guid}")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> UploadToFtp(Guid batchId, CancellationToken ct = default)
    {
        // Pending batch'teki imageId→fileName mapping'ini çek
        var pendingImages = await GetPendingBatchImages(batchId, ct);
        if (pendingImages is null)
            return BadRequest(new { success = false, error = "Batch bulunamadı veya pending değil." });

        var files = Request.Form.Files;
        var results = new List<object>();

        foreach (var file in files)
        {
            // Her dosyanın field name'i imageId olmalı
            if (!Guid.TryParse(file.Name, out var imageId))
            {
                results.Add(new { imageId = file.Name, fileName = (string?)null, success = false, error = "Geçersiz imageId format" });
                continue;
            }

            if (!pendingImages.TryGetValue(imageId, out var fileName))
            {
                results.Add(new { imageId, fileName = (string?)null, success = false, error = "ImageId batch'te bulunamadı" });
                continue;
            }

            using var stream = file.OpenReadStream();
            var uploadSuccess = await _imageUploadService.UploadAsync(stream, fileName, ct);
            results.Add(new { imageId, fileName, success = uploadSuccess });
        }

        return Ok(new { success = true, data = new { results } });
    }

    [HttpPost("products/{productId:guid}/images/confirm")]
    public async Task<IActionResult> ConfirmImageBatch(Guid productId, [FromBody] ConfirmImageBatchCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command with { ProductId = productId }, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPatch("products/{productId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> UpdateProductImageMetadata(
        Guid productId,
        Guid imageId,
        [FromBody] UpdateImageMetadataRequest request,
        CancellationToken ct = default)
    {
        var command = new UpdateProductImageMetadataCommand(imageId, request.SortOrder, request.IsProductCover, request.IsVariantCover);
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpDelete("products/{productId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> ArchiveProductImage(Guid productId, Guid imageId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ArchiveProductImageCommand(imageId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Archive ───────────────────────────────────────────────────────────────

    [HttpGet("products/{productId:guid}/images/archive")]
    public async Task<IActionResult> GetProductImageArchive(
        Guid productId,
        [FromQuery] Guid? imageSetId,
        [FromQuery] Guid? batchId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductImageArchiveQuery(productId, imageSetId, batchId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("products/{productId:guid}/images/archive/{batchId:guid}/restore")]
    public async Task<IActionResult> RestoreImageBatch(Guid productId, Guid batchId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RestoreImageBatchCommand(productId, batchId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Image Set Mappings ────────────────────────────────────────────────────

    [HttpGet("products/{productId:guid}/image-set-mappings")]
    public async Task<IActionResult> GetProductImageSetMappings(Guid productId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductImageSetMappingsQuery(productId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPut("products/{productId:guid}/image-set-mappings")]
    public async Task<IActionResult> UpsertProductImageSetMapping(Guid productId, [FromBody] UpsertMappingRequest request, CancellationToken ct = default)
    {
        var command = new UpsertProductImageSetMappingCommand(productId, request.ForSetId, request.UseSetId);
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpDelete("products/{productId:guid}/image-set-mappings/{forSetId:guid}")]
    public async Task<IActionResult> DeleteProductImageSetMapping(Guid productId, Guid forSetId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteProductImageSetMappingCommand(productId, forSetId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Coverage Report ───────────────────────────────────────────────────────

    [HttpGet("products/image-coverage-report")]
    public async Task<IActionResult> GetProductImageCoverageReport([FromQuery] Guid? imageSetId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductImageCoverageReportQuery(imageSetId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Product Videos ────────────────────────────────────────────────────────

    [HttpGet("products/{productId:guid}/videos")]
    public async Task<IActionResult> GetProductVideos(
        Guid productId,
        [FromQuery] Guid? imageSetId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductVideosQuery(productId, imageSetId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("products/{productId:guid}/videos/prepare")]
    public async Task<IActionResult> PrepareVideoBatch(Guid productId, [FromBody] PrepareVideoBatchRequest request, CancellationToken ct = default)
    {
        var command = new PrepareVideoBatchCommand(productId, request.ImageSetId, request.FileExtensions, request.ReplaceSet);
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("upload/videos/{batchId:guid}")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> UploadVideoToFtp(Guid batchId, CancellationToken ct = default)
    {
        var files = Request.Form.Files;
        var db = HttpContext.RequestServices.GetRequiredService<ECSPros.Catalog.Application.Services.ICatalogDbContext>();
        var pendingVideos = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                db.ProductVideos.Where(x => x.BatchId == batchId && x.Status == ECSPros.Catalog.Domain.Entities.ProductImageStatus.Pending),
                ct);

        if (!pendingVideos.Any())
            return BadRequest(new { success = false, error = "Batch bulunamadı veya pending değil." });

        var mapping = pendingVideos.ToDictionary(x => x.Id, x => x.FileName);
        var results = new List<object>();

        foreach (var file in files)
        {
            if (!Guid.TryParse(file.Name, out var videoId))
            {
                results.Add(new { videoId = file.Name, fileName = (string?)null, success = false, error = "Geçersiz videoId format" });
                continue;
            }

            if (!mapping.TryGetValue(videoId, out var fileName))
            {
                results.Add(new { videoId, fileName = (string?)null, success = false, error = "VideoId batch'te bulunamadı" });
                continue;
            }

            using var stream = file.OpenReadStream();
            var uploadSuccess = await _videoUploadService.UploadAsync(stream, fileName, ct);
            results.Add(new { videoId, fileName, success = uploadSuccess });
        }

        return Ok(new { success = true, data = new { results } });
    }

    [HttpPost("products/{productId:guid}/videos/confirm")]
    public async Task<IActionResult> ConfirmVideoBatch(Guid productId, [FromBody] ConfirmVideoBatchCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command with { ProductId = productId }, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPatch("products/{productId:guid}/videos/{videoId:guid}")]
    public async Task<IActionResult> UpdateProductVideoMetadata(
        Guid productId,
        Guid videoId,
        [FromBody] UpdateVideoMetadataRequest request,
        CancellationToken ct = default)
    {
        var command = new UpdateProductVideoMetadataCommand(videoId, request.SortOrder, request.ThumbnailFileName);
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpDelete("products/{productId:guid}/videos/{videoId:guid}")]
    public async Task<IActionResult> ArchiveProductVideo(Guid productId, Guid videoId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ArchiveProductVideoCommand(videoId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("products/{productId:guid}/videos/archive")]
    public async Task<IActionResult> GetProductVideoArchive(
        Guid productId,
        [FromQuery] Guid? imageSetId,
        [FromQuery] Guid? batchId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductVideoArchiveQuery(productId, imageSetId, batchId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("products/{productId:guid}/videos/archive/{batchId:guid}/restore")]
    public async Task<IActionResult> RestoreVideoBatch(Guid productId, Guid batchId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RestoreVideoBatchCommand(productId, batchId), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Local File Serving ────────────────────────────────────────────────────

    /// <summary>
    /// LocalDiskImageUploadService ile yüklenen görselleri doğrudan API üzerinden sunar.
    /// FTP/CDN yapılandırılmış ortamlarda bu endpoint'e istek gelmez (frontend publicBaseUrl'i kullanır).
    /// </summary>
    [HttpGet("images/file/{fileName}")]
    [AllowAnonymous]
    public IActionResult ServeLocalImage(string fileName)
    {
        // Path traversal koruması
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest();

        var basePath = Path.Combine(AppContext.BaseDirectory, "uploads", "images", "products");
        var filePath = Path.Combine(basePath, fileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();
        var contentType = ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png"           => "image/png",
            "webp"          => "image/webp",
            "gif"           => "image/gif",
            "mp4"           => "video/mp4",
            "webm"          => "video/webm",
            "mov"           => "video/quicktime",
            _               => "application/octet-stream",
        };

        return PhysicalFile(filePath, contentType);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, string>?> GetPendingBatchImages(Guid batchId, CancellationToken ct)
    {
        // IImageUploadService üzerinden değil, doğrudan MediatR aracılığıyla erişmek yerine
        // ICatalogDbContext'e ihtiyaç var. Controller'da DI ile çözmek için IServiceProvider kullanıyoruz.
        var db = HttpContext.RequestServices.GetRequiredService<ECSPros.Catalog.Application.Services.ICatalogDbContext>();
        var images = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                db.ProductImages.Where(x => x.BatchId == batchId && x.Status == ECSPros.Catalog.Domain.Entities.ProductImageStatus.Pending),
                ct);

        if (!images.Any())
            return null;

        return images.ToDictionary(x => x.Id, x => x.FileName);
    }
}

// ─── Request DTOs ──────────────────────────────────────────────────────────────

public record UpdateImageSetRequest(string Name, bool IsDefault, Guid? FallbackSetId, int SortPriority, bool IsActive);
public record PrepareImageBatchRequest(Guid? VariantId, Guid ImageSetId, List<string> FileExtensions, bool ReplaceSet);
public record UpdateImageMetadataRequest(int SortOrder, bool IsProductCover, bool IsVariantCover);
public record UpsertMappingRequest(Guid ForSetId, Guid UseSetId);
public record PrepareVideoBatchRequest(Guid ImageSetId, List<string> FileExtensions, bool ReplaceSet);
public record UpdateVideoMetadataRequest(int SortOrder, string? ThumbnailFileName);
