using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;

namespace ECSPros.Storefront.Application.Commands.CreateCollection;

/// <summary>E6: koleksiyon oluştur — pending doğar (admin onayına dek Faz G bloklarında
/// görünmez; üye kendi sayfasında görür). ShareCode benzersiz kısa kod.</summary>
public record CreateCollectionCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string Name,
    string? Description,
    bool IsPublic,
    bool IsShareable,
    List<string>? ProductCodes = null) : IRequest<Result<Guid>>;

public class CreateCollectionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateCollectionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCollectionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<Guid>("Koleksiyon adı gereklidir.");

        var koleksiyon = new Collection
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsPublic = request.IsPublic,
            IsShareable = request.IsShareable,
            ShareCode = Guid.NewGuid().ToString("N")[..10],
            Status = "pending"
        };
        foreach (var kod in (request.ProductCodes ?? []).Select(k => k.Trim())
                     .Where(k => k.Length > 0).Distinct())
            koleksiyon.Items.Add(new CollectionItem { ProductCode = kod });

        db.Collections.Add(koleksiyon);
        await db.SaveChangesAsync(ct);
        return Result.Success(koleksiyon.Id);
    }
}
