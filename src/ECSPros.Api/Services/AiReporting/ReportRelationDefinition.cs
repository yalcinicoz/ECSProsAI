using System.Linq.Expressions;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>A registered one-to-many relationship can filter with EXISTS, never multiply parent measures.</summary>
public sealed class ReportRelationDefinition<TParent, TChild, TKey>(string id, string label,
    ReportEntityDefinition<TChild> child, Expression<Func<TParent, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childKey)
{
    public IReadOnlyList<ReportField> Describe(IReadOnlySet<string> permissions)
    {
        var fields = child.Describe(permissions);
        return fields.Count == 0 ? Array.Empty<ReportField>() : Array.AsReadOnly(new[]
        {
            new ReportField(id, label, "relation", child.Permission,
                "Birden çoğa ilişki; yalnız yetkili eşleşen kayıt var/yok koşulu. Ana kayıt miktarını çoğaltmaz.", Array.Empty<string>())
        }.Concat(fields).ToArray());
    }

    public void Bind(ReportPredicateSchema<TParent> parent, IQueryable<TChild> query,
        Expression<Func<TChild, bool>> mandatoryScope, IReadOnlySet<string> permissions)
    {
        if (child.Describe(permissions).Count == 0) return;
        parent.Relation(id, query, parentKey, childKey, child.Predicates(mandatoryScope));
    }
}
