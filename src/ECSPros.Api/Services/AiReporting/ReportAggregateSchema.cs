using System.Linq.Expressions;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Reusable grouped projection. Operates on the whole authorized input, never executes or pages.</summary>
public sealed class ReportAggregateSchema<T>(string permission, Expression<Func<T, bool>> guard)
{
    private sealed record Measure(LambdaExpression? Selector, string? RequiredDimension, string Operation);
    private readonly Dictionary<string, Expression<Func<T, string?>>> dimensions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Measure> measures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> fieldPermissions = new(StringComparer.Ordinal);
    public ReportAggregateSchema<T> RequirePermission(string id, string required)
    {
        if ((!dimensions.ContainsKey(id) && !measures.ContainsKey(id)) || string.IsNullOrWhiteSpace(required)) throw new ArgumentException("Alan yetkisi geçersiz.");
        fieldPermissions[id] = required;
        return this;
    }

    public ReportAggregateSchema<T> Dimension(string id, Expression<Func<T, string?>> selector)
    {
        if (string.IsNullOrWhiteSpace(id) || measures.ContainsKey(id) || !dimensions.TryAdd(id, selector))
            throw new ArgumentException("Rapor alanı tekil olmalı.");
        return this;
    }
    public ReportAggregateSchema<T> Sum(string id, Expression<Func<T, decimal>> selector, string? requiredDimension = null)
        => AddMeasure(id, new(selector, requiredDimension, nameof(Enumerable.Sum)));
    public ReportAggregateSchema<T> Average(string id, Expression<Func<T, decimal>> selector, string? requiredDimension = null)
        => AddMeasure(id, new(selector, requiredDimension, nameof(Enumerable.Average)));
    public ReportAggregateSchema<T> Minimum(string id, Expression<Func<T, decimal>> selector, string? requiredDimension = null)
        => AddMeasure(id, new(selector, requiredDimension, nameof(Enumerable.Min)));
    public ReportAggregateSchema<T> Maximum(string id, Expression<Func<T, decimal>> selector, string? requiredDimension = null)
        => AddMeasure(id, new(selector, requiredDimension, nameof(Enumerable.Max)));
    public ReportAggregateSchema<T> Count(string id) => AddMeasure(id, new(null, null, nameof(Enumerable.LongCount)));
    public ReportAggregateSchema<T> DistinctCount<TValue>(string id, Expression<Func<T, TValue>> selector)
    {
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(string) && type != typeof(Guid) && type != typeof(int) && type != typeof(long))
            throw new ArgumentException("Tekil sayım yalnız metin veya kimlik alanını destekler.");
        return AddMeasure(id, new(selector, null, "distinctCount"));
    }
    private ReportAggregateSchema<T> AddMeasure(string id, Measure measure)
    {
        if (string.IsNullOrWhiteSpace(id) || dimensions.ContainsKey(id) || !measures.TryAdd(id, measure))
            throw new ArgumentException("Rapor alanı tekil olmalı.");
        return this;
    }

    public ReportAggregateQuery Build(IQueryable<T> source, ReportAggregatePlan plan, IReadOnlySet<string> permissions)
    {
        if (!permissions.Contains(ReportDictionary.UsePermission) || !permissions.Contains(permission))
            throw new UnauthorizedAccessException();
        if (plan is null || plan.Dimensions is null || plan.Measures is null
            || plan.Dimensions.Length > 4 || plan.Measures.Length is < 1 or > 4
            || plan.Dimensions.Any(d => d is null || !dimensions.ContainsKey(d))
            || plan.Measures.Any(m => m is null || !measures.ContainsKey(m))
            || plan.Dimensions.Distinct().Count() != plan.Dimensions.Length
            || plan.Measures.Distinct().Count() != plan.Measures.Length
            || plan.Direction is not ("asc" or "desc")) throw new ArgumentException("Rapor seçim planı geçerli değil.");
        // Snapshot client collections. Later edits must not change query/column correspondence.
        plan.ValidateResultLimit();
        var selectedDimensions = plan.Dimensions.ToArray();
        var selectedMeasures = plan.Measures.ToArray();
        var columns = selectedDimensions.Concat(selectedMeasures).ToArray();
        if (columns.Any(id => fieldPermissions.TryGetValue(id, out var required) && !permissions.Contains(required))) throw new UnauthorizedAccessException();
        if (plan.Sort is not null && !columns.Contains(plan.Sort)) throw new ArgumentException("Rapor sıralama alanı seçili değil.");
        if (selectedMeasures.Any(m => measures[m].RequiredDimension is { } required && !selectedDimensions.Contains(required)))
            throw new ArgumentException("Bu ölçü için zorunlu gruplama alanı eksik (örneğin para birimi).");
        var item = Expression.Parameter(typeof(T), "item");
        var keyBindings = Enumerable.Range(0, 4).Select(i => Expression.Bind(typeof(ReportAggregateKey).GetProperty($"D{i}")!,
            i < selectedDimensions.Length ? Replace(dimensions[selectedDimensions[i]].Body, dimensions[selectedDimensions[i]].Parameters[0], item)
                : Expression.Constant(null, typeof(string))));
        var key = Expression.Lambda<Func<T, ReportAggregateKey>>(Expression.MemberInit(Expression.New(typeof(ReportAggregateKey)), keyBindings), item);
        var group = Expression.Parameter(typeof(IGrouping<ReportAggregateKey, T>), "group");
        var bindings = new List<MemberBinding>();
        for (var i = 0; i < 4; i++)
        {
            bindings.Add(Expression.Bind(typeof(ReportAggregateRow).GetProperty($"D{i}")!, Expression.Property(Expression.Property(group, "Key"), $"D{i}")));
            Expression value = Expression.Constant(0m);
            if (i < selectedMeasures.Length)
            {
                var measure = measures[selectedMeasures[i]];
                value = measure.Operation == "distinctCount" ? BuildDistinctCount(group, measure.Selector!) : measure.Selector is null
                    ? Expression.Convert(Expression.Call(typeof(Enumerable), nameof(Enumerable.LongCount), new[] { typeof(T) }, group), typeof(decimal))
                    : Expression.Call(typeof(Enumerable), measure.Operation, new[] { typeof(T) }, group, measure.Selector);
            }
            bindings.Add(Expression.Bind(typeof(ReportAggregateRow).GetProperty($"M{i}")!, value));
        }
        var projection = Expression.Lambda<Func<IGrouping<ReportAggregateKey, T>, ReportAggregateRow>>(
            Expression.MemberInit(Expression.New(typeof(ReportAggregateRow)), bindings), group);
        var rows = source.Where(guard).GroupBy(key).Select(projection);
        var sortIndex = plan.Sort is null ? 0 : Array.IndexOf(columns, plan.Sort);
        var slot = sortIndex < selectedDimensions.Length ? $"D{sortIndex}" : $"M{sortIndex - selectedDimensions.Length}";
        var sorted = Sort(rows, slot, plan.Direction == "desc", false);
        // Full group key provides deterministic ties, including when sorting by a measure.
        for (var i = 0; i < selectedDimensions.Length; i++) sorted = Sort(sorted, $"D{i}", false, true);
        // Top-N membership is fixed by the report ranking, not by later table sorting/filtering.
        return new(Array.AsReadOnly(columns), plan.Top is { } limit ? sorted.Take(limit) : sorted);
    }

    private static Expression BuildDistinctCount(Expression group, LambdaExpression selector)
    {
        var type = selector.ReturnType;
        Expression values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), new[] { typeof(T), type }, group, selector);
        // SQL COUNT(DISTINCT ...) excludes NULL; keep in-memory evaluation identical.
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
        {
            var value = Expression.Parameter(type, "value");
            values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Where), new[] { type }, values,
                Expression.Lambda(Expression.NotEqual(value, Expression.Constant(null, type)), value));
        }
        values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Distinct), new[] { type }, values);
        return Expression.Convert(Expression.Call(typeof(Enumerable), nameof(Enumerable.LongCount), new[] { type }, values), typeof(decimal));
    }

    private static IQueryable<ReportAggregateRow> Sort(IQueryable<ReportAggregateRow> rows, string slot, bool desc, bool then)
    {
        var row = Expression.Parameter(typeof(ReportAggregateRow), "result");
        var member = Expression.Property(row, slot);
        var method = then ? (desc ? "ThenByDescending" : "ThenBy") : (desc ? "OrderByDescending" : "OrderBy");
        return rows.Provider.CreateQuery<ReportAggregateRow>(Expression.Call(typeof(Queryable), method,
            new[] { typeof(ReportAggregateRow), member.Type }, rows.Expression, Expression.Quote(Expression.Lambda(member, row))));
    }
    private static Expression Replace(Expression body, ParameterExpression from, Expression to) => new Substitute(from, to).Visit(body)!;
    private sealed class Substitute(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
