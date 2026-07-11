namespace ECSPros.Storefront.Application.Commands.PublishPageSnapshot;

/// <summary>
/// G4: yayınlanmış snapshot'ın JSON şeması — Yayınla bu modeli üretir,
/// canlı okuma (store API + Razor) aynı modele deserialize eder. Snapshot ürün
/// DETAYI taşımaz: ürün blokları Config içindeki kaynak konfigürasyonuyla gelir,
/// canlı taraf G3 resolver'la doldurur (spec). Yalnız yayın anında aktif blok/öğeler
/// yazılır; tarih penceresi (StartAt/EndAt) istek anında değerlendirilir — pencere
/// yayından sonra açılabilir. Rule G-M2 kural motorunun girdisidir (null=herkese).
/// </summary>
public record PageSnapshotDto(
    int Version,
    DateTime PublishedAt,
    List<SnapshotBlockDto> Blocks);

public record SnapshotBlockDto(
    Guid Id,
    string Placement,
    string BlockType,
    string? Template,
    Dictionary<string, string> Title,
    Dictionary<string, string>? Subtitle,
    int SortOrder,
    int Priority,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Rule,
    string? Config,
    List<SnapshotItemDto> Items);

public record SnapshotItemDto(
    Guid Id,
    Dictionary<string, string> Title,
    Dictionary<string, string>? Subtitle,
    string? ImageUrl,
    string? MobileImageUrl,
    string? VideoUrl,
    string? LinkUrl,
    bool OpenInNewTab,
    Dictionary<string, string>? ButtonText,
    string? BadgeLabel,
    int SortOrder,
    int Priority,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Rule,
    string? Config);
