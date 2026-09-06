namespace ECSPros.Api.Services.Marketplace.Mapping;

// ── Kategori eşleme ──────────────────────────────────────────────────────────

/// <summary>EM1 (2026-09-06): kural = 1..n koşul (VE) → hedef. Eski kayıtlar tek koşulu AttributeTypeCode/ValueId
/// alanlarında taşır; <see cref="EffectiveConditions"/> her iki biçimi tek listeye indirger. Kayıtta hem
/// Conditions hem (ilk koşul olarak) eski alanlar yazılır — geriye uyumlu okuma.</summary>
public sealed record MappingRuleDto(
    int Order,
    string? AttributeTypeCode,
    Guid? ValueId,
    string? ValueLabel,
    string TargetExternalId,
    string TargetName,
    string TargetPath,
    List<MappingConditionDto>? Conditions = null)
{
    public IReadOnlyList<MappingConditionDto> EffectiveConditions()
    {
        if (Conditions is { Count: > 0 }) return Conditions;
        if (!string.IsNullOrWhiteSpace(AttributeTypeCode) && ValueId is { } v && v != Guid.Empty)
            return [new MappingConditionDto(AttributeTypeCode, v, ValueLabel ?? "")];
        return [];
    }
}

public sealed record MappingConditionDto(string AttributeTypeCode, Guid ValueId, string ValueLabel);

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

// ── RF4 toplu öneri/eşleme (2026-09-01, plan: docs/pazaryeri-referans-ve-esleme-plani.md) ──

/// <summary>Eşsiz bir grup + onun için önerilen ilk 3 kategori (kampanya tablosunun satırı).</summary>
public sealed record GroupSuggestionRowDto(
    Guid ProductGroupId, string Code, string Name, int ProductCount,
    List<CategorySuggestionDto> Suggestions);

public sealed record BulkCategoryMappingItem(Guid ProductGroupId, string TargetExternalId);

public sealed record BulkCategoryMappingRequest(string Marketplace, List<BulkCategoryMappingItem> Items);

public sealed record BulkCategoryMappingResult(int Saved, int Failed, List<string> Errors);

// ── EM0: ERP hedefleri + sözlük ──────────────────────────────────────────────
public sealed record ErpTargetDto(string Key, string ServiceCode, string Name, bool HasContract, int GroupCount);
public sealed record ErpReferenceItemDto(
    Guid Id, string TargetSystem, string Kind, string Code, string Name, string? ParentCode,
    bool IsActive, string Source, DateTime LastSeenAt, bool IsMapped);
