namespace ECSPros.Api.Services.Marketplace.Mapping;

// ── Kategori eşleme ──────────────────────────────────────────────────────────

public sealed record MappingRuleDto(
    int Order,
    string AttributeTypeCode,
    Guid ValueId,
    string ValueLabel,
    string TargetExternalId,
    string TargetName,
    string TargetPath);

public sealed record PoolTargetDto(string ExternalId, string Name, string Path);

public sealed record CategoryMappingDto(
    Guid Id,
    string MappingKind,
    string? TargetExternalId,
    string? TargetName,
    string? TargetPath,
    List<MappingRuleDto> Rules,
    List<PoolTargetDto> Pool,
    string Status,
    string? StatusNote);

public sealed record GroupRowDto(
    Guid ProductGroupId,
    string Code,
    string Name,
    int ProductCount,
    CategoryMappingDto? Mapping);

public sealed record MappingOverviewDto(
    List<GroupRowDto> Groups,
    int MappedCount,
    int UnmappedCount,
    int ReviewCount);

public sealed record MpCategoryDto(string ExternalId, string Name, string Path);

public sealed record CategorySuggestionDto(string ExternalId, string Name, string Path, int Score);

public sealed record SaveCategoryMappingRequest(
    string Marketplace,
    Guid ProductGroupId,
    string MappingKind,
    string? TargetExternalId,
    string? TargetName,
    string? TargetPath,
    List<MappingRuleDto>? Rules,
    List<PoolTargetDto>? Pool);

// ── Özellik eşleme ───────────────────────────────────────────────────────────

public sealed record OwnAttributeTypeDto(Guid Id, string Code, string Name);

public sealed record OwnAttributeValueDto(Guid Id, string Label);

public sealed record MpAttributeRowDto(
    string ExternalId,
    string Name,
    bool IsRequired,
    bool AllowCustom,
    bool IsVariantAxis,
    string ValueMode,
    int ValueCount,
    // mevcut eşleme (varsa)
    Guid? MappingId,
    string? Strategy,
    Guid? AttributeTypeId,
    string? FixedValue,
    string? Status,
    string? StatusNote,
    // değer eşleme ilerlemesi (map_values stratejisinde)
    int OwnValueCount,
    int MappedValueCount);

public sealed record AttributesViewDto(
    List<MpAttributeRowDto> Attributes,
    List<OwnAttributeTypeDto> OwnAttributeTypes);

public sealed record MappedTargetDto(string ExternalId, string Name, string Path, List<string> ViaGroups);

public sealed record SaveAttributeMappingRequest(
    string Marketplace,
    string MpCategoryExternalId,
    string MpAttributeExternalId,
    string MpAttributeName,
    string Strategy,
    Guid? AttributeTypeId,
    string? FixedValue);

// ── Değer eşleme ─────────────────────────────────────────────────────────────

public sealed record ValueRowDto(
    Guid AttributeValueId,
    string Label,
    string? TargetExternalId,
    string? TargetValue,
    string Status,
    // öneri (eşsiz satırlar için)
    string? SuggestedExternalId,
    string? SuggestedValue,
    int SuggestedScore);

public sealed record ValuesViewDto(
    Guid? AttributeTypeId,
    string? AttributeTypeName,
    List<ValueRowDto> Rows,
    List<MpValueDto> MpValues);

public sealed record MpValueDto(string? ExternalId, string? Code, string Value);

public sealed record SaveValueMappingItem(
    Guid AttributeValueId,
    string? TargetExternalId,
    string? TargetCode,
    string? TargetValue);

public sealed record SaveValueMappingsRequest(
    string Marketplace,
    string MpCategoryExternalId,
    string MpAttributeExternalId,
    List<SaveValueMappingItem> Items);

// ── Gözden geçir ─────────────────────────────────────────────────────────────

public sealed record ReviewRowDto(
    Guid MappingId,
    string MappingType,        // category | attribute | value
    string Marketplace,
    string Status,             // broken | needs_review
    string Title,              // insan-okur başlık (grup adı / özellik adı / değer)
    string? Note,
    string? MpCategoryExternalId,
    Guid? ProductGroupId);
