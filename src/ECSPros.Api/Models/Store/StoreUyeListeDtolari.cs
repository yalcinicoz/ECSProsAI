using ECSPros.Api.Services.Store;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Models.Store;

// B1 (2026-09-07, mobil): üye listelerinin zenginleştirilmiş satırları — mevcut alanlar KORUNUR (web istemcisi
// aynı uçları kullanır), ürün özeti alanları eklenir. product null = ürün artık satışta değil / bulunamadı.

// B9 (2026-09-08, mobil): satırda kart rozetleri (campaignName/campaignBadges) + tek biçim fiyat (price) da döner.
// Eski alanlar (minPrice/campaignPrice) bir sürüm KORUNUR — sahadaki uygulamalar kırılmasın; mobil price'a geçince kaldırılır.

public record FavoriteItemDto(
    string ProductCode, Guid? ColorValueId,
    string? ProductName, string? ImageUrl, decimal? MinPrice, decimal? CompareAtPrice, decimal? CampaignPrice,
    bool IsAvailable,
    decimal? Price = null, string? CampaignName = null, IReadOnlyList<CampaignBadge>? CampaignBadges = null);

public record ViewedProductItemDto(
    string ProductCode, DateTime ViewedAt,
    string? ProductName, string? ImageUrl, decimal? MinPrice, decimal? CompareAtPrice, decimal? CampaignPrice,
    bool IsAvailable,
    decimal? Price = null, string? CampaignName = null, IReadOnlyList<CampaignBadge>? CampaignBadges = null);

public record MemberReviewItemDto(
    Guid Id, string ProductCode, int Rating, string? Text, string Status, string? RejectReason,
    bool IsDeleted, DateTime CreatedAt, DateTime? DeletedAt, string? Topic, IReadOnlyList<string>? Photos,
    string? ProductName, string? ImageUrl);

public record MemberQuestionItemDto(
    Guid Id, string ProductCode, string Question, string? Answer, string Status, string MemberName,
    DateTime CreatedAt, DateTime? AnsweredAt,
    string? ProductName, string? ImageUrl);

public record MemberCollectionItemDto(
    Guid Id, string Name, string? Description, bool IsPublic, bool IsShareable, string ShareCode, string Status,
    int ViewCount, bool IsQuickSave, DateTime? UpdatedAt, DateTime CreatedAt,
    List<string> ItemCodes,
    List<StoreUrunOzetDto> Items);
