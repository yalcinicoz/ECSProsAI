using System.Linq.Expressions;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>
/// One server-owned declaration supplies both the AI dictionary and executable expressions.
/// Business fields are registered once; report questions/combinations are not registered here.
/// </summary>
public sealed class ReportEntityDefinition<T>(string permission)
{
    public string Permission => permission;
    private sealed record FieldRegistration(ReportField Metadata,
        Action<ReportPredicateSchema<T>>? Predicate, Action<ReportAggregateSchema<T>>? Aggregate, Action<ReportDetailSchema<T>>? Detail);
    private readonly Dictionary<string, FieldRegistration> fields = new(StringComparer.Ordinal);
    private bool sealedDefinition;

    public ReportEntityDefinition<T> Text(string id, string label, Expression<Func<T, string?>> selector,
        bool groupable = true, bool filterable = true)
    {
        if (!groupable && !filterable) throw new ArgumentException("Alan en az bir işlem desteklemeli.");
        return Register(new(id, label, groupable ? "dimension" : "filter", permission, label,
                filterable ? Array.AsReadOnly(new[] { "eq", "ne", "in", "contains", "isNull", "isNotNull" }) : Array.Empty<string>()) { DataType = "text" },
            filterable ? schema => schema.Field(id, selector) : null,
            groupable ? schema => schema.Dimension(id, selector) : null, schema => schema.Field(id, selector));
    }

    public ReportEntityDefinition<T> Value<TValue>(string id, string label, Expression<Func<T, TValue>> selector)
    {
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(Guid) && type != typeof(DateTime) && type != typeof(decimal) && type != typeof(int) && type != typeof(long))
            throw new ArgumentException("Alan türü desteklenmiyor.");
        var operators = type == typeof(Guid) ? new[] { "eq", "ne", "in" } : new[] { "eq", "ne", "in", "gt", "gte", "lt", "lte", "between" };
        if (Nullable.GetUnderlyingType(typeof(TValue)) is not null) operators = operators.Concat(new[] { "isNull", "isNotNull" }).ToArray();
        return Register(new(id, label, "filter", permission, label, Array.AsReadOnly(operators)) { DataType = type == typeof(DateTime) ? "date" : type == typeof(Guid) ? "guid" : "number" }, schema => schema.Field(id, selector), null,
            schema => schema.Field(id, selector));
    }

    public ReportEntityDefinition<T> Count(string id, string label) =>
        Register(new(id, label, "metric", permission, label, []), null, schema => schema.Count(id));

    public ReportEntityDefinition<T> SetText(string id, string label, Expression<Func<T, string?>> display,
        Expression<Func<T, string?>> normalizedJson)
        => Register(new(id, label, "dimension", permission, "Çoklu seçenek kümesi; eq/in tek seçeneğin üyeliğini sınar.", new[] { "eq", "in" }) { DataType = "text" },
            schema => schema.SetField(id, normalizedJson), schema => schema.Dimension(id, display), schema => schema.Field(id, display));

    public ReportEntityDefinition<T> DistinctCount<TValue>(string id, string label, Expression<Func<T, TValue>> selector)
    {
        // Validate at declaration time, rather than failing on the first user report.
        new ReportAggregateSchema<T>(permission, _ => true).DistinctCount(id, selector);
        return Register(new(id, label, "metric", permission, label + "; tekil değer sayısı, null hariç", []),
            null, schema => schema.DistinctCount(id, selector));
    }

    public ReportEntityDefinition<T> Measure(string id, string label, Expression<Func<T, decimal>> selector,
        string operation, string? requiredDimension = null, bool filterable = false)
    {
        Action<ReportAggregateSchema<T>> aggregate = operation switch
        {
            "sum" => schema => schema.Sum(id, selector, requiredDimension),
            "average" => schema => schema.Average(id, selector, requiredDimension),
            "minimum" => schema => schema.Minimum(id, selector, requiredDimension),
            "maximum" => schema => schema.Maximum(id, selector, requiredDimension),
            _ => throw new ArgumentException("Ölçü işlemi desteklenmiyor.")
        };
        // A row filter on an aggregate-only average/min/max ID would misrepresent its meaning.
        if (filterable && operation != "sum") throw new ArgumentException("Bu ölçü satır filtresi olamaz.");
        var description = label + (requiredDimension is null ? "" : $"; zorunlu kırılım: {requiredDimension}");
        return Register(new(id, label, "metric", permission, description,
                filterable ? Array.AsReadOnly(new[] { "eq", "ne", "in", "gt", "gte", "lt", "lte", "between" }) : Array.Empty<string>()) { DataType = "number" },
            filterable ? schema => schema.Field(id, selector) : null, aggregate,
            filterable ? schema => schema.Field(id, selector, requiredDimension) : null);
    }

    private ReportEntityDefinition<T> Register(ReportField field, Action<ReportPredicateSchema<T>>? predicate,
        Action<ReportAggregateSchema<T>>? aggregate, Action<ReportDetailSchema<T>>? detail = null)
    {
        if (sealedDefinition) throw new InvalidOperationException("Veri sözlüğü sabitlendi.");
        if (string.IsNullOrWhiteSpace(field.Id) || !fields.TryAdd(field.Id, new(field, predicate, aggregate, detail)))
            throw new ArgumentException("Veri sözlüğü alan kimliği geçerli veya tekil değil.");
        return this;
    }

    public ReportEntityDefinition<T> RequirePermission(string id, string required)
    {
        if (sealedDefinition) throw new InvalidOperationException("Veri sözlüğü sabitlendi.");
        if (!fields.TryGetValue(id, out var field) || string.IsNullOrWhiteSpace(required)) throw new ArgumentException("Alan yetkisi geçersiz.");
        fields[id] = field with { Metadata = field.Metadata with { Permission = required } };
        return this;
    }
    public ReportEntityDefinition<T> Seal() { sealedDefinition = true; return this; }
    private void RequireSealed()
    { if (!sealedDefinition) throw new InvalidOperationException("Veri sözlüğü kullanılmadan önce sabitlenmeli."); }

    public IReadOnlyList<ReportField> Describe(IReadOnlySet<string> permissions)
    {
        RequireSealed();
        return permissions.Contains(ReportDictionary.UsePermission) && permissions.Contains(permission)
            ? Array.AsReadOnly(fields.Values.Where(f => permissions.Contains(f.Metadata.Permission)).Select(f => f.Metadata).ToArray()) : Array.Empty<ReportField>();
    }

    public ReportPredicateSchema<T> Predicates(Expression<Func<T, bool>> mandatoryScope)
    {
        RequireSealed();
        var schema = new ReportPredicateSchema<T>(permission, mandatoryScope);
        foreach (var field in fields.Values.Where(f => f.Predicate is not null))
        { field.Predicate!(schema); schema.RequirePermission(field.Metadata.Id, field.Metadata.Permission); }
        return schema;
    }

    public ReportAggregateSchema<T> Aggregates(Expression<Func<T, bool>> mandatoryScope)
    {
        RequireSealed();
        var schema = new ReportAggregateSchema<T>(permission, mandatoryScope);
        foreach (var field in fields.Values.Where(f => f.Aggregate is not null))
        { field.Aggregate!(schema); schema.RequirePermission(field.Metadata.Id, field.Metadata.Permission); }
        return schema;
    }

    public IReadOnlyList<ReportField> DescribeDetails(IReadOnlySet<string> permissions)
    {
        var authorized = Describe(permissions);
        return Array.AsReadOnly(authorized.Where(f => fields[f.Id].Detail is not null).ToArray());
    }

    public ReportDetailSchema<T> Details(Expression<Func<T, bool>> mandatoryScope, Expression<Func<T, Guid>> uniqueKey)
    {
        RequireSealed();
        var schema = new ReportDetailSchema<T>(permission, mandatoryScope, uniqueKey);
        foreach (var field in fields.Values.Where(f => f.Detail is not null))
        { field.Detail!(schema); schema.RequirePermission(field.Metadata.Id, field.Metadata.Permission); }
        return schema;
    }
}
