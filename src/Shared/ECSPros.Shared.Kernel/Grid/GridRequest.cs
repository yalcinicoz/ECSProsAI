namespace ECSPros.Shared.Kernel.Grid;

/// <summary>
/// Panel DataGrid standardı (docs/datagrid-standardi-plani.md, F0 — 2026-09-08).
/// Liste uçlarının ortak istek modeli: sayfa, arama, sıralama ve tipli kolon filtreleri.
/// URL kodlaması: <c>?page=1&amp;pageSize=50&amp;search=…&amp;sort=createdAt&amp;dir=desc&amp;f.status=in:pending,confirmed&amp;f.total=gt:1000</c>
/// Filtre/sıralama alanları handler'ın <see cref="GridSchema{T}"/> beyaz listesinden geçer; listede olmayan alan/operatör
/// <see cref="GridException"/> (→ 400) üretir. Dinamik LINQ / keyfi property adı YOKTUR.
/// </summary>
public sealed record GridFilter(string Field, string Op, string Value);

public sealed class GridRequest
{
    public const int MaxPageSize = 250;
    public const string OpAuto = "auto";   // parser op vermediğinde: tipin varsayılan operatörü (text→contains, enum→in, date→between, sayı/bool/guid→eq)

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Search { get; init; }
    public string? Sort { get; init; }
    /// <summary>asc | desc (varsayılan: şemanın varsayılan yönü)</summary>
    public string? Dir { get; init; }
    public List<GridFilter> Filters { get; init; } = new();

    public bool? Desc => Dir is null ? null : string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
    public bool HasFilter(string field) => Filters.Any(f => string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Search) && Filters.Count == 0;

    /// <summary>Sayfa ≥ 1, sayfa boyu 1..250 (merkezi clamp). Varsayılan sayfa boyu uca göre verilir.</summary>
    public GridRequest Normalize(int defaultPageSize = 50) => new()
    {
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize <= 0 ? defaultPageSize : PageSize, 1, MaxPageSize),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Sort = string.IsNullOrWhiteSpace(Sort) ? null : Sort.Trim(),
        Dir = string.IsNullOrWhiteSpace(Dir) ? null : Dir.Trim().ToLowerInvariant(),
        Filters = Filters.Where(f => !string.IsNullOrWhiteSpace(f.Field)).ToList(),
    };

    /// <summary>Verilen alanlar hariç kopya — sekme sayaçları (durum filtresi dışındaki filtrelerle sayım) için.</summary>
    public GridRequest Without(params string[] fields) => new()
    {
        Page = Page, PageSize = PageSize, Search = Search, Sort = Sort, Dir = Dir,
        Filters = Filters.Where(f => !fields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList(),
    };
}

/// <summary>
/// Excel export gövdesi (POST): aynı filtre modeli + kolon listesi. Sayfa/sayfa boyu YOK — export sayfalama uygulamaz.
/// <c>Named</c>: ucun mevcut adlandırılmış parametreleri (örn. statuses, from, to, paymentCollected) — eski filtre modeli korunur.
/// </summary>
public sealed class GridExportRequest
{
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public string? Dir { get; init; }
    public List<GridFilter>? Filters { get; init; }
    /// <summary>Boş → tüm export kolonları; dolu → yalnız bu anahtarlar (kilitli kolonlar her zaman eklenir).</summary>
    public List<string>? Columns { get; init; }
    public Dictionary<string, string>? Named { get; init; }

    public GridRequest ToGridRequest() => new GridRequest
    {
        Page = 1, PageSize = GridRequest.MaxPageSize, Search = Search, Sort = Sort, Dir = Dir,
        Filters = Filters ?? new(),
    }.Normalize();

    public string? NamedValue(string key) =>
        Named is not null && Named.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
}

/// <summary>Geçersiz grid isteği (bilinmeyen alan/operatör/değer). ArgumentException türevi → GlobalExceptionMiddleware 400 döner.</summary>
public sealed class GridException(string message) : ArgumentException(message);

/// <summary>Genel export kaynağı (handler → controller): sayfalamasız sıralı sorgu + toplam (tavan kontrolü). Sorgu çağıranın scope'unda tüketilir.</summary>
public record GridExportSource<TRow>(int Count, IQueryable<TRow> Rows);
