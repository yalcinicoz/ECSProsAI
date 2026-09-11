using System.Linq.Expressions;
using System.Text.Json.Serialization;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Api.Services.AiReporting;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportDetailPlan
{
    public string[]? Columns { get; init; }
    public string? Sort { get; init; }
    public string Direction { get; init; } = "asc";
    public int? Top { get; init; }
}

public sealed record ReportDetailQuery<T>(IReadOnlyList<string> Columns, IQueryable<T> Filtered, IQueryable<object?[]> Page);

/// <summary>Server-owned field projection. Does not connect, execute or expose unselected entity columns.</summary>
public sealed class ReportDetailSchema<T>(string permission, Expression<Func<T, bool>> guard, Expression<Func<T, Guid>> uniqueKey)
{
    private sealed record FieldDefinition(LambdaExpression Selector, Action<GridSchema<T>> Register, string? RequiredColumn);
    private readonly Dictionary<string, FieldDefinition> fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> fieldPermissions = new(StringComparer.Ordinal);
    public ReportDetailSchema<T> RequirePermission(string id, string required)
    {
        if (!fields.ContainsKey(id) || string.IsNullOrWhiteSpace(required)) throw new ArgumentException("Alan yetkisi geçersiz.");
        fieldPermissions[id] = required;
        return this;
    }

    public ReportDetailSchema<T> Field<TValue>(string id, Expression<Func<T, TValue>> selector, string? requiredColumn = null)
    {
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(string) && type != typeof(Guid) && type != typeof(DateTime)
            && type != typeof(decimal) && type != typeof(int) && type != typeof(long))
            throw new ArgumentException("Detay alan türü desteklenmiyor.");
        void Register(GridSchema<T> schema)
        {
            schema.Sort(id, selector);
            if (type == typeof(string)) schema.Text(id, (Expression<Func<T, string?>>)(object)selector);
            else if (type == typeof(Guid)) schema.Guid(id, selector);
            else if (type == typeof(DateTime)) schema.Date(id, selector);
            else schema.Number(id, selector);
        }
        if (string.IsNullOrWhiteSpace(id) || !fields.TryAdd(id, new(selector, Register, requiredColumn)))
            throw new ArgumentException("Detay alan kimliği tekil olmalı.");
        return this;
    }

    public ReportDetailQuery<T> Build(IQueryable<T> source, ReportDetailPlan plan, ReportGridState grid, IReadOnlySet<string> permissions)
    {
        if (!permissions.Contains(ReportDictionary.UsePermission) || !permissions.Contains(permission)) throw new UnauthorizedAccessException();
        if (plan is null || plan.Columns is not { Length: >= 1 and <= 16 }
            || plan.Columns.Any(c => c is null || !fields.ContainsKey(c)) || plan.Columns.Distinct().Count() != plan.Columns.Length
            || plan.Direction is not ("asc" or "desc") || plan.Sort is not null && !plan.Columns.Contains(plan.Sort)
            || plan.Top is < 1 or > 1000 || plan.Top is not null && plan.Sort is null)
            throw new ArgumentException("Detay kolon/sıralama/sınır planı geçerli değil.");
        var columns = plan.Columns.ToArray();
        if (columns.Any(id => fieldPermissions.TryGetValue(id, out var required) && !permissions.Contains(required))) throw new UnauthorizedAccessException();
        if (columns.Any(c => fields[c].RequiredColumn is { } required && !columns.Contains(required)))
            throw new ArgumentException("Detay alanının zorunlu eşlik eden kolonu eksik (örneğin para birimi).");
        if (grid.Page is < 1 or > 1_000_000 || grid.PageSize < 1 || grid.PageSize > grid.MaxPageSize || grid.Dir is not (null or "asc" or "desc")
            || grid.Search is { Length: > 256 } || grid.Search?.Any(char.IsControl) == true || grid.Filters is { Length: > 16 }
            || grid.Sort is not null && !columns.Contains(grid.Sort)
            || grid.Filters?.Any(f => f is null || !columns.Contains(f.Field) || f.Value is null || f.Value.Length > 256 || f.Value.Any(char.IsControl)) == true
            || grid.Filters is not null && grid.Filters.Select(f => f.Field).Distinct().Count() != grid.Filters.Length)
            throw new ArgumentException("Detay tablo ayarları geçerli değil.");
        var schema = new GridSchema<T>().DefaultSort(uniqueKey, false).TieBreaker(uniqueKey);
        foreach (var column in columns) fields[column].Register(schema);
        var request = new GridRequest { Sort = grid.Sort ?? plan.Sort ?? columns[0], Dir = grid.Dir ?? plan.Direction,
            Filters = (grid.Filters ?? []).Select(f => new GridFilter(f.Field, f.Op, f.Value)).ToList() };
        var rows = source.Where(guard);
        if (plan.Top is { } limit)
            rows = schema.ApplySort(rows, new GridRequest { Sort = plan.Sort, Dir = plan.Direction }).Take(limit);
        rows = schema.ApplyFilters(rows, request);
        var item = Expression.Parameter(typeof(T), "item");
        Expression Body(string id) => new Substitute(fields[id].Selector.Parameters[0], item).Visit(fields[id].Selector.Body)!;
        if (!string.IsNullOrWhiteSpace(grid.Search))
        {
            var textColumns = columns.Where(c => fields[c].Selector.ReturnType == typeof(string)).ToArray();
            if (textColumns.Length == 0) throw new ArgumentException("Seçili detay kolonlarında aranabilir metin yok.");
            var term = grid.Search.Trim().ToLowerInvariant();
            Expression<Func<string>> captured = () => term;
            Expression match = Expression.Constant(false);
            foreach (var column in textColumns)
            {
                var value = Body(column);
                match = Expression.OrElse(match, Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, typeof(string))),
                    Expression.Call(Expression.Call(value, nameof(string.ToLower), Type.EmptyTypes), nameof(string.Contains), Type.EmptyTypes, captured.Body)));
            }
            rows = rows.Where(Expression.Lambda<Func<T, bool>>(match, item));
        }
        var projection = Expression.Lambda<Func<T, object?[]>>(Expression.NewArrayInit(typeof(object),
            columns.Select(c => Expression.Convert(Body(c), typeof(object)))), item);
        var page = schema.ApplySort(rows, request).Skip(checked((grid.Page - 1) * grid.PageSize)).Take(grid.PageSize).Select(projection);
        return new(Array.AsReadOnly(columns), rows, page);
    }

    private sealed class Substitute(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    { protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node); }
}
