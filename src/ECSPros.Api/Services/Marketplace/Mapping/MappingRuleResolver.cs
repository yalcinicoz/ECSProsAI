namespace ECSPros.Api.Services.Marketplace.Mapping;

/// <summary>
/// EM1: kurallı eşlemenin TEK çözücüsü (readiness, gönderim ve ERP aynı sınıfı kullanır — plan §2.3).
/// Sıralı kurallar; bir kuralın TÜM koşulları (VE) ürünün değerlerinde sağlanıyorsa o kural kazanır.
/// Koşulsuz kural hiçbir zaman eşleşmez (yanlışlıkla "her şey" olmasın). Hiçbiri tutmazsa null → çağıran
/// varsayılan hedefe (direct) düşer ya da rule_no_match üretir.
/// </summary>
public static class MappingRuleResolver
{
    public static MappingRuleDto? Resolve(
        IEnumerable<MappingRuleDto> rules,
        Func<string, Guid?> typeIdByCode,
        IReadOnlyDictionary<Guid, List<Guid>>? productValuesByTypeId)
    {
        foreach (var rule in rules.OrderBy(r => r.Order))
        {
            var conds = rule.EffectiveConditions();
            if (conds.Count == 0) continue;
            var all = true;
            foreach (var c in conds)
            {
                var typeId = typeIdByCode(c.AttributeTypeCode);
                if (typeId is null
                    || productValuesByTypeId is null
                    || !productValuesByTypeId.TryGetValue(typeId.Value, out var vals)
                    || !vals.Contains(c.ValueId))
                { all = false; break; }
            }
            if (all) return rule;
        }
        return null;
    }

    /// <summary>Kaydetmeden önce: koşul listesini normalize eder (eski alanlar → Conditions; ilk koşul eski
    /// alanlara da yazılır), boş/eksik koşullu kuralı hata olarak bildirir.</summary>
    public static (List<MappingRuleDto>? Rules, string? Error) Normalize(IReadOnlyList<MappingRuleDto> rules)
    {
        var result = new List<MappingRuleDto>();
        var i = 0;
        foreach (var r in rules.OrderBy(r => r.Order))
        {
            var raw = r.EffectiveConditions();
            if (raw.Any(c => string.IsNullOrWhiteSpace(c.AttributeTypeCode) || c.ValueId == Guid.Empty))
                return (null, $"{i + 1}. kuralda eksik koşul var; tüm koşulları tamamlayın.");
            var conds = raw
                .GroupBy(c => (c.AttributeTypeCode, c.ValueId)).Select(g => g.First()).ToList();
            if (conds.Count == 0) return (null, $"{i + 1}. kuralda en az bir koşul (özellik = değer) gerekli.");
            if (string.IsNullOrWhiteSpace(r.TargetExternalId)) return (null, $"{i + 1}. kuralın hedefi seçilmemiş.");
            result.Add(new MappingRuleDto(i++, conds[0].AttributeTypeCode, conds[0].ValueId, conds[0].ValueLabel,
                r.TargetExternalId, r.TargetName, r.TargetPath, conds));
        }
        return (result, null);
    }
}
