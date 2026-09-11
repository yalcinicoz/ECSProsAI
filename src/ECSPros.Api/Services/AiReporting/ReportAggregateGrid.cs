using System.Linq.Expressions;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Api.Services.AiReporting;

public static class ReportAggregateGrid
{
    public static IQueryable<ReportAggregateRow> Apply(ReportAggregateQuery query, ReportAggregatePlan plan, ReportGridState grid)
    {
        if (grid.Page is < 1 or > 1_000_000 || grid.PageSize < 1 || grid.PageSize > grid.MaxPageSize || grid.Dir is not (null or "asc" or "desc")
            || grid.Search is { Length: > 256 } || grid.Search?.Any(char.IsControl) == true
            || grid.Filters is { Length: > 16 } || grid.Sort is not null && !query.Columns.Contains(grid.Sort)
            || grid.Filters?.Any(f => f is null || !query.Columns.Contains(f.Field) || f.Value is null || f.Value.Length > 256 || f.Value.Any(char.IsControl)) == true
            || grid.Filters is not null && grid.Filters.Select(f => f.Field).Distinct().Count() != grid.Filters.Length)
            throw new ArgumentException("Dinamik rapor tablo ayarları geçerli değil.");
        var schema = new GridSchema<ReportAggregateRow>();
        var dimensionCount = plan.Dimensions!.Length;
        if (dimensionCount == 0 && !string.IsNullOrWhiteSpace(grid.Search))
            throw new ArgumentException("Bu raporda aranabilir metin kolonu yok; ölçü kolonunun filtresini kullanın.");
        for (var i = 0; i < query.Columns.Count; i++)
        {
            if (i < dimensionCount) schema.Text(query.Columns[i], Dimension(i)).Sort(query.Columns[i], Dimension(i));
            else schema.Number(query.Columns[i], Measure(i - dimensionCount)).Sort(query.Columns[i], Measure(i - dimensionCount));
        }
        var request = new GridRequest { Sort = grid.Sort ?? plan.Sort ?? query.Columns[0], Dir = grid.Dir ?? plan.Direction,
            Filters = (grid.Filters ?? []).Select(f => new GridFilter(f.Field, f.Op, f.Value)).ToList() };
        var rows = schema.ApplyFilters(query.Rows, request);
        if (!string.IsNullOrWhiteSpace(grid.Search))
        {
            var row = Expression.Parameter(typeof(ReportAggregateRow), "result");
            Expression body = Expression.Constant(false);
            var term = grid.Search.Trim().ToLowerInvariant();
            Expression<Func<string>> captured = () => term;
            for (var i = 0; i < dimensionCount; i++)
            {
                var field = Expression.Property(row, $"D{i}");
                body = Expression.OrElse(body, Expression.AndAlso(Expression.NotEqual(field, Expression.Constant(null, typeof(string))),
                    Expression.Call(Expression.Call(field, nameof(string.ToLower), Type.EmptyTypes), nameof(string.Contains), Type.EmptyTypes, captured.Body)));
            }
            rows = rows.Where(Expression.Lambda<Func<ReportAggregateRow, bool>>(body, row));
        }
        var sorted = (IOrderedQueryable<ReportAggregateRow>)schema.ApplySort(rows, request);
        for (var i = 0; i < dimensionCount; i++) sorted = sorted.ThenBy(Dimension(i));
        return sorted;
    }

    private static Expression<Func<ReportAggregateRow, string?>> Dimension(int i) => i switch
    { 0 => r => r.D0, 1 => r => r.D1, 2 => r => r.D2, 3 => r => r.D3, _ => throw new ArgumentException("Kolon sınırı.") };
    private static Expression<Func<ReportAggregateRow, decimal>> Measure(int i) => i switch
    { 0 => r => r.M0, 1 => r => r.M1, 2 => r => r.M2, 3 => r => r.M3, _ => throw new ArgumentException("Ölçü sınırı.") };
    public static object?[] Values(ReportAggregateRow row, int dimensions, int measures) =>
        new object?[] { row.D0, row.D1, row.D2, row.D3 }.Take(dimensions)
            .Concat(new object?[] { row.M0, row.M1, row.M2, row.M3 }.Take(measures)).ToArray();
}
