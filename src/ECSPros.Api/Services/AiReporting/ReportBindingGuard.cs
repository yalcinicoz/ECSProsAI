using System.Globalization;
using System.Text;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Detect dictionary collisions; never silently rewrite a model's chosen field.</summary>
public static class ReportBindingGuard
{
    public static string Fold(string value) => string.Concat(value.Trim().ToLowerInvariant().Replace('ı', 'i')
        .Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();

    public static IReadOnlyList<string> ProductGroupValues(DynamicReportPlan plan)
    {
        if (plan.Source != "stock") return [];
        var values = new HashSet<string>();
        var budget = new ReportPredicateBudget();
        void Visit(ReportPredicate? node, int depth)
        {
            if (node is null) return;
            budget.Visit(depth);
            if (node.Field == "productGroup" && node.Operator is "eq" or "in")
                foreach (var value in node.Values ?? [])
                    if (!string.IsNullOrWhiteSpace(value) && value.Length <= 128) values.Add(value);
            foreach (var child in node.Children ?? []) Visit(child, depth + 1);
        }
        Visit(plan.Predicate, 0);
        if (values.Count > 128) throw new ArgumentException("Grup seçeneği sınırı aşıldı.");
        return values.ToArray();
    }

    public static string? Question(IReadOnlyList<string> requested, IReadOnlyList<string> realGroups, IReadOnlyList<ReportField> attributes)
    {
        var existing = realGroups.Select(Fold).ToHashSet();
        foreach (var value in requested)
        {
            var normalized = Fold(value);
            if (existing.Contains(normalized)) continue;
            var matches = attributes.Where(f => f.MatchedValues?.Any(v => Fold(v) == normalized) == true)
                .Select(f => f.Label).Distinct().Take(3).ToArray();
            if (matches.Length > 0)
                return $"“{value}” adıyla ürün grubu bulunamadı; bu değer şu özelliklerde var: {string.Join(", ", matches)}. Hangi özellik ve değeri kullanmak istiyorsunuz? Seçilen özellik bu belirsiz grup koşulunun yerine geçecek; diğer kolon ve koşullarınız korunacak.";
        }
        return null; // No evidence of a collision: an empty result remains legitimate.
    }
}
