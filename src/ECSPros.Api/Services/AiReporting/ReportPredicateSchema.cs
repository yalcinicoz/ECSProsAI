using System.Globalization;
using System.Linq.Expressions;

namespace ECSPros.Api.Services.AiReporting;

internal sealed class ReportPredicateBudget
{
    private int nodes;
    public void Visit(int depth)
    {
        if (++nodes > 64 || depth > 6) throw new ArgumentException("Rapor koşulu sınırı aşıldı.");
    }
}

/// <summary>
/// Reusable query compiler. Schema, permissions and mandatory row guards are SERVER inputs.
/// Applies only WHERE/EXISTS; never executes, paginates, joins or accepts model expressions.
/// </summary>
public sealed class ReportPredicateSchema<T>(string permission, Expression<Func<T, bool>> rowGuard)
{
    private readonly Dictionary<string, Func<ReportPredicate, ParameterExpression, Expression>> fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> fieldPermissions = new(StringComparer.Ordinal);
    public ReportPredicateSchema<T> RequirePermission(string id, string required)
    {
        if (!fields.ContainsKey(id) || string.IsNullOrWhiteSpace(required)) throw new ArgumentException("Alan yetkisi geçersiz.");
        fieldPermissions[id] = required;
        return this;
    }
    private readonly Dictionary<string, Func<ReportPredicate, ParameterExpression, IReadOnlySet<string>, ReportPredicateBudget, int, Expression>> relations = new(StringComparer.Ordinal);

    public ReportPredicateSchema<T> Field<TValue>(string id, Expression<Func<T, TValue>> selector)
    {
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(string) && type != typeof(decimal) && type != typeof(int)
            && type != typeof(long) && type != typeof(Guid) && type != typeof(DateTime))
            throw new ArgumentException("Rapor alan türü desteklenmiyor.");
        if (string.IsNullOrWhiteSpace(id) || !fields.TryAdd(id, (node, parameter) =>
            CompareNullable(node, Replace(selector.Body, selector.Parameters[0], parameter), type)))
            throw new ArgumentException("Rapor alan kimliği geçerli veya tekil değil.");
        return this;
    }

    public ReportPredicateSchema<T> SetField(string id, Expression<Func<T, string?>> selector)
    {
        if (!fields.TryAdd(id, (node, parameter) =>
        {
            if (node.Operator is not ("eq" or "in") || node.Values is not { Length: >= 1 and <= 20 }
                || node.Operator == "eq" && node.Values.Length != 1
                || node.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 256 || v.Any(char.IsControl)))
                throw new ArgumentException("Özellik filtresi geçerli değil.");
            var member = Replace(selector.Body, selector.Parameters[0], parameter);
            var options = new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var checks = node.Values.Select(v => {
                var literal = System.Text.Json.JsonSerializer.Serialize(v.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")), options);
                return (Expression)Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, Parameter(literal, typeof(string)));
            }).Aggregate(Expression.OrElse);
            return Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, typeof(string))), checks);
        })) throw new ArgumentException("Yinelenen özellik alanı.");
        return this;
    }

    private static Expression CompareNullable(ReportPredicate node, Expression member, Type type)
    {
        if (Nullable.GetUnderlyingType(member.Type) is null) return Compare(node, member, type);
        var hasValue = Expression.Property(member, "HasValue");
        if (node.Operator is "isNull" or "isNotNull")
        {
            if (node.Values is { Length: > 0 }) throw new ArgumentException("Boşluk filtresi geçerli değil.");
            return node.Operator == "isNull" ? Expression.Not(hasValue) : hasValue;
        }
        return Expression.AndAlso(hasValue, Compare(node, Expression.Property(member, "Value"), type));
    }

    public ReportPredicateSchema<T> Relation<TChild, TKey>(string id, IQueryable<TChild> target,
        Expression<Func<T, TKey>> parentKey, Expression<Func<TChild, TKey>> childKey,
        ReportPredicateSchema<TChild> childSchema)
    {
        if (string.IsNullOrWhiteSpace(id) || !relations.TryAdd(id, (node, parent, permissions, budget, depth) =>
        {
            var child = Expression.Parameter(typeof(TChild), "related");
            var nested = childSchema.Build(node.Children![0], child, permissions, budget, depth + 1);
            var guarded = Expression.AndAlso(Replace(childSchema.RowGuard.Body, childSchema.RowGuard.Parameters[0], child), nested);
            var keys = Expression.Equal(Replace(parentKey.Body, parentKey.Parameters[0], parent),
                Replace(childKey.Body, childKey.Parameters[0], child));
            var predicate = Expression.Lambda<Func<TChild, bool>>(Expression.AndAlso(keys, guarded), child);
            var any = Expression.Call(typeof(Queryable), nameof(Queryable.Any), new[] { typeof(TChild) },
                target.Expression, Expression.Quote(predicate));
            return node.Kind == "notExists" ? Expression.Not(any) : any;
        })) throw new ArgumentException("Rapor ilişki kimliği geçerli veya tekil değil.");
        return this;
    }

    private Expression<Func<T, bool>> RowGuard => rowGuard;

    public IQueryable<T> Apply(IQueryable<T> source, ReportPredicate predicate, IReadOnlySet<string> permissions)
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        var body = Build(predicate, parameter, permissions, new(), 0);
        // User NOT/OR cannot negate or escape the separately applied mandatory guard.
        return source.Where(rowGuard).Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    private Expression Build(ReportPredicate node, ParameterExpression parameter,
        IReadOnlySet<string> permissions, ReportPredicateBudget budget, int depth)
    {
        if (!permissions.Contains(ReportDictionary.UsePermission) || !permissions.Contains(permission))
            throw new UnauthorizedAccessException();
        budget.Visit(depth);
        if (node is null) throw new ArgumentException("Rapor koşulu boş.");
        if (node.Kind == "compare")
        {
            if (node.Relation is not null || node.Children is not null || node.Field is null || !fields.TryGetValue(node.Field, out var field))
                throw new ArgumentException("Rapor alanı desteklenmiyor.");
            if (fieldPermissions.TryGetValue(node.Field, out var required) && !permissions.Contains(required)) throw new UnauthorizedAccessException();
            return field(node, parameter);
        }
        if (node.Field is not null || node.Operator is not null || node.Values is not null || node.Children is null)
            throw new ArgumentException("Rapor koşulu biçimi geçerli değil.");
        if (node.Kind is "exists" or "notExists")
        {
            if (node.Children.Length != 1 || node.Relation is null || !relations.TryGetValue(node.Relation, out var relation))
                throw new ArgumentException("Rapor ilişkisi desteklenmiyor.");
            return relation(node, parameter, permissions, budget, depth);
        }
        if (node.Relation is not null || node.Kind is not ("all" or "any" or "not")
            || node.Children.Length < 1 || node.Children.Length > 16 || (node.Kind == "not" && node.Children.Length != 1))
            throw new ArgumentException("Rapor koşul grubu geçerli değil.");
        var children = node.Children.Select(n => Build(n, parameter, permissions, budget, depth + 1)).ToArray();
        return node.Kind == "not" ? Expression.Not(children[0])
            : children.Aggregate((left, right) => node.Kind == "all" ? Expression.AndAlso(left, right) : Expression.OrElse(left, right));
    }

    private static Expression Compare(ReportPredicate node, Expression member, Type type)
    {
        if (node.Operator is "isNull" or "isNotNull")
        {
            if (type != typeof(string) || node.Values is { Length: > 0 }) throw new ArgumentException("Boşluk filtresi geçerli değil.");
            var empty = Expression.Constant(null, type);
            return node.Operator == "isNull" ? Expression.Equal(member, empty) : Expression.NotEqual(member, empty);
        }
        if (node.Values is null || node.Values.Length is < 1 or > 20
            || node.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 256 || v.Any(char.IsControl))
            || node.Values.Distinct(StringComparer.Ordinal).Count() != node.Values.Length)
            throw new ArgumentException("Rapor filtre değeri geçerli değil.");
        var op = node.Operator;
        if (op is not ("eq" or "ne" or "in" or "gt" or "gte" or "lt" or "lte" or "between" or "contains")
            || (op == "between" ? node.Values.Length != 2 : op != "in" && node.Values.Length != 1)
            || (op == "contains" && type != typeof(string))
            || (op is "gt" or "gte" or "lt" or "lte" or "between") && (type == typeof(string) || type == typeof(Guid)))
            throw new ArgumentException("Rapor filtre işlemi desteklenmiyor.");
        var values = node.Values.Select(v => ParseValue(v, type)).ToArray();
        if (op == "between" && ((IComparable)values[0]).CompareTo(values[1]) >= 0)
            throw new ArgumentException("Rapor aralığının başlangıcı bitişten küçük olmalı.");
        var right = Parameter(values[0], type);
        return op switch
        {
            "eq" => Expression.Equal(member, right), "ne" => Expression.NotEqual(member, right),
            "gt" => Expression.GreaterThan(member, right), "gte" => Expression.GreaterThanOrEqual(member, right),
            "lt" => Expression.LessThan(member, right), "lte" => Expression.LessThanOrEqual(member, right),
            // Half-open for dates and numeric intervals, unambiguous across consecutive periods.
            "between" => Expression.AndAlso(Expression.GreaterThanOrEqual(member, right), Expression.LessThan(member, Parameter(values[1], type))),
            "in" => values.Select(v => Expression.Equal(member, Parameter(v, type))).Aggregate<Expression>(Expression.OrElse),
            "contains" => Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, right)),
            _ => throw new ArgumentException("Rapor işlemi desteklenmiyor.")
        };
    }

    private static object ParseValue(string value, Type type)
    {
        if (type == typeof(string)) return value;
        if (type == typeof(Guid) && Guid.TryParseExact(value, "D", out var guid) && guid != Guid.Empty) return guid;
        if (type == typeof(decimal) && decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) return number;
        if (type == typeof(int) && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)) return integer;
        if (type == typeof(long) && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var big)) return big;
        if (type == typeof(DateTime) && (value.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(value, @"[+-]\d{2}:\d{2}$"))
            && value.Contains('T') && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date.UtcDateTime;
        throw new ArgumentException("Rapor filtre değeri alan türüne uygun değil.");
    }

    // Captured values become EF parameters; literals are never SQL fragments.
    private static Expression Parameter(object value, Type type)
    {
        Expression<Func<object>> captured = () => value;
        return Expression.Convert(captured.Body, type);
    }
    private static Expression Replace(Expression body, ParameterExpression from, Expression to) => new Substitute(from, to).Visit(body)!;
    private sealed class Substitute(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
