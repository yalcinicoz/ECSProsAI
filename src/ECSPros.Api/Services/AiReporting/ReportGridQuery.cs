using System.Globalization;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.AiReporting;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportGridState(int Page = 1, int PageSize = 25, string? Search = null,
    string? Sort = null, string? Dir = null, ReportGridFilter[]? Filters = null)
{
    // Not a JSON property: only the export endpoint can grant the larger retrieval budget.
    internal bool ExportMode { get; init; }
    internal int MaxPageSize => ExportMode ? ReportExcelExport.MaxRows : 250;
    public static ReportGridState ForExport(ReportGridState grid) => grid with
    { Page = 1, PageSize = ReportExcelExport.MaxRows, ExportMode = true };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportGridFilter(string Field, string Op, string Value);

/// <summary>Filter the entire authorized aggregate, then count/sum/page in ONE database snapshot.</summary>
public static class ReportGridQuery
{
    public static NpgsqlCommand Create(string json, IReadOnlySet<string> permissions,
        IReadOnlyList<ReportField> attributes, ReportGridState grid)
    {
        var validation = ReportDefinitionValidator.Parse(json, permissions, attributes: attributes);
        if (!validation.IsValid) throw new ArgumentException(validation.Error);
        var recipe = validation.Definition!;
        var dimensions = recipe.Dimensions!;
        var metrics = recipe.Metrics!;
        var columns = dimensions.Concat(metrics).ToArray();
        if (grid.Page is < 1 or > 1_000_000 || grid.PageSize < 1 || grid.PageSize > grid.MaxPageSize
            || grid.Dir is not (null or "asc" or "desc") || (grid.Filters?.Length ?? 0) > 16)
            throw new ArgumentException("Tablo sayfası, sıralama veya filtre sınırı geçerli değil.");
        ValidateText(grid.Search ?? "");
        if (grid.Sort is not null && !columns.Contains(grid.Sort))
            throw new ArgumentException("Bu raporda sıralama alanı bulunmuyor.");

        var command = StockReportQuery.Create(json, permissions, attributes, applyResultLimit: false);
        try
        {
            string Parameter(object value, NpgsqlDbType type)
            {
                var name = "g" + command.Parameters.Count;
                command.Parameters.AddWithValue(name, type, value);
                return "@" + name;
            }
            var predicates = new List<string>();
            var seen = new HashSet<string>();
            foreach (var filter in grid.Filters ?? Array.Empty<ReportGridFilter>())
            {
                if (filter is null || filter.Field is null || !seen.Add(filter.Field)) throw new ArgumentException("Tablo filtresi geçerli değil.");
                var index = Array.IndexOf(columns, filter.Field);
                if (index < 0) throw new ArgumentException("Bu raporda filtre alanı bulunmuyor.");
                ValidateText(filter.Value);
                var col = "r.c" + index;
                if (index >= dimensions.Length)
                {
                    decimal Number(string value) => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var number) ? number : throw new ArgumentException("Sayısal filtre geçerli değil.");
                    if (filter.Op == "between")
                    {
                        var pair = filter.Value.Split(';');
                        if (pair.Length != 2) throw new ArgumentException("Sayısal aralık geçerli değil.");
                        var from = Number(pair[0]); var to = Number(pair[1]);
                        if (from > to) throw new ArgumentException("Aralık başlangıcı bitişten büyük olamaz.");
                        predicates.Add($"{col} BETWEEN {Parameter(from, NpgsqlDbType.Numeric)} AND {Parameter(to, NpgsqlDbType.Numeric)}");
                    }
                    else
                    {
                        var op = filter.Op switch { "eq" => "=", "gt" => ">", "gte" => ">=", "lt" => "<", "lte" => "<=", _ => throw new ArgumentException("Sayısal operatör desteklenmiyor.") };
                        predicates.Add($"{col} {op} {Parameter(Number(filter.Value), NpgsqlDbType.Numeric)}");
                    }
                }
                else
                {
                    var value = Parameter(Normalize(filter.Value), NpgsqlDbType.Text);
                    var text = TextColumn(col);
                    predicates.Add(filter.Op switch
                    {
                        "eq" => $"{text} = {value}",
                        "contains" => $"strpos({text}, {value}) > 0",
                        "startswith" => $"starts_with({text}, {value})",
                        _ => throw new ArgumentException("Metin operatörü desteklenmiyor.")
                    });
                }
            }
            if (!string.IsNullOrWhiteSpace(grid.Search))
            {
                var value = Parameter(Normalize(grid.Search), NpgsqlDbType.Text);
                predicates.Add("(" + string.Join(" OR ", columns.Select((_, i) => $"strpos({TextColumn("r.c" + i)}, {value}) > 0")) + ")");
            }
            var order = new List<string>();
            if (grid.Sort is not null) order.Add($"p.c{Array.IndexOf(columns, grid.Sort)} {(grid.Dir == "desc" ? "DESC" : "ASC")} NULLS LAST");
            // Group keys provide deterministic ties; an ungrouped aggregate has only one row.
            order.AddRange(dimensions.Select((id, i) => (id, i)).Where(x => x.id != grid.Sort).Select(x => $"p.c{x.i} ASC NULLS LAST"));
            if (order.Count == 0) order.Add("p.c0 ASC NULLS LAST");
            var metricTotals = metrics.Select((_, i) => $"COALESCE(SUM(c{dimensions.Length + i}),0) AS m{i}");
            var totalColumns = metrics.Select((_, i) => $"totals.m{i}");
            command.CommandText = "WITH report AS (" + command.CommandText + "), filtered AS MATERIALIZED (SELECT * FROM report r"
                + (predicates.Count > 0 ? " WHERE " + string.Join(" AND ", predicates) : "")
                + "), totals AS (SELECT COUNT(*) AS total, " + string.Join(", ", metricTotals) + " FROM filtered)"
                + " SELECT totals.total, " + string.Join(", ", totalColumns) + ", page.* FROM totals LEFT JOIN LATERAL ("
                + "SELECT TRUE AS has_row, p.* FROM filtered p ORDER BY " + string.Join(", ", order)
                + " LIMIT @gridSize OFFSET @gridOffset) page ON TRUE ORDER BY " + string.Join(", ", order).Replace("p.c", "page.c");
            command.Parameters.AddWithValue("gridSize", grid.PageSize);
            command.Parameters.AddWithValue("gridOffset", (long)(grid.Page - 1) * grid.PageSize);
            return command;
        }
        catch { command.Dispose(); throw; }
    }

    private static string Normalize(string value) => value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
    private static string TextColumn(string col) => $"lower(translate(COALESCE({col}::text, 'Belirtilmemiş'), 'Iİ', 'ıi'))";
    private static void ValidateText(string? value)
    {
        if (value is null || value.Length > 256 || value.Any(char.IsControl)) throw new ArgumentException("Tablo araması veya filtre değeri geçerli değil.");
    }
}
