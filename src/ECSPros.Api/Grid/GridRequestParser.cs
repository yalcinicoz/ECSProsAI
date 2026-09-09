using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Api.Grid;

/// <summary>
/// Query string → <see cref="GridRequest"/>. Tanınan anahtarlar: page, pageSize, search, sort, dir, f.&lt;alan&gt;=&lt;op&gt;:&lt;değer&gt;.
/// <c>f.x=değer</c> (op'suz) → op "auto" (şema tipin varsayılanını uygular). <c>fq.*</c> (istemci hızlı-seçim etiketi) yok sayılır.
/// Ucun mevcut adlandırılmış parametreleri (statuses, from, to…) controller'da ayrıca okunmaya devam eder.
/// </summary>
public static class GridRequestParser
{
    /// <param name="kanalKisiti">Y3 (K2): kullanıcının bu listede görebileceği kanallar.
    /// null verilirse kısıt uygulanmaz — kanaldan bağımsız listelerde (ürün, üye, depo…) bilinçli
    /// olarak null geçilir. Parametre ZORUNLUDUR: her liste ucu kapsam kararını açıkça verir.</param>
    public static GridRequest Parse(IQueryCollection q, IReadOnlyCollection<Guid>? kanalKisiti, int defaultPageSize = 50)
    {
        var filters = new List<GridFilter>();
        foreach (var (key, values) in q)
        {
            if (!key.StartsWith("f.", StringComparison.OrdinalIgnoreCase) || key.Length <= 2) continue;
            var field = key[2..];
            foreach (var raw in values)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var idx = raw.IndexOf(':');
                // "in:a,b" / "between:x,y" / "gt:5"; ':' yoksa auto. Tarih ISO değerlerinde ':' saat ayıracı olabilir → op önekini
                // yalnız bilinen operatör adıysa ayır.
                if (idx > 0 && idx <= 12 && KnownOps.Contains(raw[..idx].ToLowerInvariant()))
                    filters.Add(new GridFilter(field, raw[..idx].ToLowerInvariant(), raw[(idx + 1)..]));
                else
                    filters.Add(new GridFilter(field, GridRequest.OpAuto, raw));
            }
        }
        return new GridRequest
        {
            Page = Int(q["page"], 1),
            PageSize = Int(q["pageSize"], defaultPageSize),
            Search = q["search"].ToString(),
            Sort = q["sort"].ToString(),
            Dir = q["dir"].ToString(),
            Filters = filters,
            KanalKisiti = kanalKisiti,
        }.Normalize(defaultPageSize);
    }

    private static readonly HashSet<string> KnownOps = new(StringComparer.Ordinal)
    { "contains", "eq", "startswith", "in", "between", "gt", "gte", "lt", "lte", "auto" };

    private static int Int(string? s, int fallback) => int.TryParse(s, out var v) ? v : fallback;
}
