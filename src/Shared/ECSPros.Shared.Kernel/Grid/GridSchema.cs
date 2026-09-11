using System.Globalization;
using System.Linq.Expressions;

namespace ECSPros.Shared.Kernel.Grid;

public enum GridFieldType { Text, Enum, Date, Number, Bool, Guid }

/// <summary>
/// Bir liste ucunun sıralanabilir/filtrelenebilir alanlarının BEYAZ LİSTESİ (K1). Handler alan adı → entity ifadesi
/// eşlemesini verir; <see cref="ApplyFilters"/> ve <see cref="ApplySort"/> yalnız bu eşlemeyi kullanır.
/// Operatörler: text contains|eq|startsWith · enum in|eq · date between|gte|gt|lte|lt · number eq|gt|gte|lt|lte|between · bool eq · guid eq.
/// Tarih değerleri: ISO-8601 (UTC'ye çevrilir) ya da yalnız gün "yyyy-MM-dd" (İstanbul günü; between'de bitiş günü DAHİL). DateOnly kolonlar da desteklenir (gün karşılaştırması).
/// Metin karşılaştırmaları mevcut arama davranışıyla aynı: <c>ToLower().Contains</c> (LOWER … LIKE).
/// </summary>
public sealed class GridSchema<T>
{
    private sealed record Field(GridFieldType Type, LambdaExpression? Selector, Expression<Func<T, bool>>? Predicate,
        IReadOnlySet<string>? Allowed, string? NullToken);

    private static readonly TimeZoneInfo TrTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private readonly Dictionary<string, LambdaExpression> _sorts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Field> _filters = new(StringComparer.OrdinalIgnoreCase);
    private LambdaExpression? _defaultSort;
    private bool _defaultDesc = true;
    private LambdaExpression? _tieBreaker;
    private Expression<Func<T, Guid>>? _kanalSecici;
    private Expression<Func<T, Guid?>>? _kanalSeciciNullable;

    public IReadOnlyCollection<string> SortableFields => _sorts.Keys;
    public IReadOnlyCollection<string> FilterableFields => _filters.Keys;
    /// <summary>Alan tipi (testler/istemci meta için); bilinmeyen alan → null.</summary>
    public GridFieldType? FieldTypeOf(string key) => _filters.TryGetValue(key, out var f) ? f.Type : null;
    /// <summary>Y3: liste kanal kapsamına tabi mi (şemada kanal kolonu bildirildi mi)?</summary>
    public bool KanalKapsamli => _kanalSecici is not null || _kanalSeciciNullable is not null;

    /// <summary>Enum alanının izinli değerleri (tanımlı değilse null).</summary>
    public IReadOnlyCollection<string>? AllowedValuesOf(string key) => _filters.TryGetValue(key, out var f) ? f.Allowed?.ToList() : null;

    // ── tanım ──
    public GridSchema<T> Sort<TKey>(string key, Expression<Func<T, TKey>> selector) { _sorts[key] = selector; return this; }
    public GridSchema<T> DefaultSort<TKey>(Expression<Func<T, TKey>> selector, bool desc = true) { _defaultSort = selector; _defaultDesc = desc; return this; }
    /// <summary>Eşit anahtarlarda kararlı sayfalama için ikinci sıralama (genelde Id).</summary>
    public GridSchema<T> TieBreaker<TKey>(Expression<Func<T, TKey>> selector) { _tieBreaker = selector; return this; }

    public GridSchema<T> Text(string key, Expression<Func<T, string?>> selector)
    { _filters[key] = new(GridFieldType.Text, selector, null, null, null); return this; }

    /// <summary>İlişkili veri sorgusuyla çağıranın uyguladığı alan; doğrudan uygulanırsa sessizce atlanmaz.</summary>
    public GridSchema<T> ExternalText(string key)
    { _filters[key] = new(GridFieldType.Text, null, null, null, null); return this; }

    /// <summary><paramref name="allowed"/> boşsa her değer kabul edilir; <paramref name="nullToken"/> (örn. "none") → NULL eşleşmesi.</summary>
    public GridSchema<T> Enum(string key, Expression<Func<T, string?>> selector, IEnumerable<string>? allowed = null, string? nullToken = null)
    {
        _filters[key] = new(GridFieldType.Enum, selector, null,
            allowed is null ? null : new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase), nullToken);
        return this;
    }

    public GridSchema<T> Date<TValue>(string key, Expression<Func<T, TValue>> selector)
    { _filters[key] = new(GridFieldType.Date, selector, null, null, null); return this; }

    public GridSchema<T> Number<TValue>(string key, Expression<Func<T, TValue>> selector)
    { _filters[key] = new(GridFieldType.Number, selector, null, null, null); return this; }

    public GridSchema<T> Bool(string key, Expression<Func<T, bool>> predicate)
    { _filters[key] = new(GridFieldType.Bool, null, predicate, null, null); return this; }

    public GridSchema<T> Guid<TValue>(string key, Expression<Func<T, TValue>> selector)
    { _filters[key] = new(GridFieldType.Guid, selector, null, null, null); return this; }

    /// <summary>
    /// Y3 (2026-09-09, K2): listenin KANAL kolonu. Bildirildiğinde, kullanıcının erişemediği
    /// kanalların satırları listeye/sayıma/exporta HİÇ girmez (yetki kontrolü ayrı; bu veri kısıtıdır).
    /// Bildirilmeyen şema "kanaldan bağımsız liste" sayılır (ürün, üye, depo… gibi).
    /// </summary>
    public GridSchema<T> Kanal(Expression<Func<T, Guid>> selector) { _kanalSecici = selector; return this; }

    /// <summary>Kanalı boş olabilen kayıtlar için (null kanal = kanala bağlı olmayan kayıt;
    /// kapsam kısıtlıyken bu satırlar da GİZLENİR — aksi hâlde sızıntı olur).</summary>
    public GridSchema<T> Kanal(Expression<Func<T, Guid?>> selector) { _kanalSeciciNullable = selector; return this; }

    /// <summary>
    /// Kanal kapsamını uygular. <paramref name="izinliKanallar"/> null → kısıt yok
    /// (süper admin ya da kanal kapsamsız yetki). Boş liste → HİÇBİR satır (default deny).
    /// </summary>
    public IQueryable<T> ApplyKanalKapsami(IQueryable<T> query, IReadOnlyCollection<Guid>? izinliKanallar)
    {
        if (izinliKanallar is null) return query;
        if (!KanalKapsamli)
            throw new InvalidOperationException(
                $"Kanal kapsamı istendi ama {typeof(T).Name} şemasında Kanal(...) bildirilmemiş.");

        var liste = izinliKanallar as IList<Guid> ?? izinliKanallar.ToList();
        if (liste.Count == 0) return query.Where(_ => false);

        if (_kanalSecici is not null)
        {
            var p = _kanalSecici.Parameters[0];
            var body = Expression.Call(
                typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)],
                Expression.Constant(liste), _kanalSecici.Body);
            return query.Where(Expression.Lambda<Func<T, bool>>(body, p));
        }
        else
        {
            var p = _kanalSeciciNullable!.Parameters[0];
            // Nullable kanal: NULL satır kapsam kısıtlıyken gizlenir.
            var deger = Expression.Property(_kanalSeciciNullable.Body, "Value");
            var doluMu = Expression.Property(_kanalSeciciNullable.Body, "HasValue");
            var contains = Expression.Call(
                typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)],
                Expression.Constant(liste), deger);
            return query.Where(Expression.Lambda<Func<T, bool>>(Expression.AndAlso(doluMu, contains), p));
        }
    }

    // ── uygulama ──
    public IQueryable<T> ApplyFilters(IQueryable<T> query, GridRequest? request, params string[] skipFields)
    {
        if (request is null) return query;
        foreach (var f in request.Filters)
        {
            if (skipFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)) continue;
            if (!_filters.TryGetValue(f.Field, out var field))
                throw new GridException($"Geçersiz filtre alanı: {f.Field}");
            var predicate = Build(field, f);
            if (predicate is not null) query = query.Where(predicate);
        }
        return query;
    }

    public IQueryable<T> ApplySort(IQueryable<T> query, GridRequest? request)
    {
        LambdaExpression? lambda = null; bool desc = _defaultDesc;
        if (request?.Sort is { Length: > 0 } key)
        {
            if (!_sorts.TryGetValue(key, out lambda)) throw new GridException($"Geçersiz sıralama alanı: {key}");
            desc = request.Desc ?? false;
        }
        lambda ??= _defaultSort;
        if (lambda is null) return query;
        var ordered = OrderBy(query, lambda, desc, then: false);
        if (_tieBreaker is not null && !ReferenceEquals(_tieBreaker, lambda))
            ordered = OrderBy(ordered, _tieBreaker, desc, then: true);
        return ordered;
    }

    // ── ifade üretimi ──
    private static IQueryable<T> OrderBy(IQueryable<T> q, LambdaExpression lambda, bool desc, bool then)
    {
        var method = then ? (desc ? "ThenByDescending" : "ThenBy") : (desc ? "OrderByDescending" : "OrderBy");
        var call = Expression.Call(typeof(Queryable), method, new[] { typeof(T), lambda.ReturnType }, q.Expression, Expression.Quote(lambda));
        return q.Provider.CreateQuery<T>(call);
    }

    private Expression<Func<T, bool>>? Build(Field field, GridFilter f)
    {
        var op = string.IsNullOrWhiteSpace(f.Op) || f.Op == GridRequest.OpAuto ? DefaultOp(field.Type) : f.Op.Trim().ToLowerInvariant();
        var value = (f.Value ?? "").Trim();
        if (field.Type != GridFieldType.Bool && value.Length == 0) return null; // boş değer = filtre yok

        if (field.Type == GridFieldType.Bool)
        {
            if (op != "eq") throw new GridException($"'{f.Field}' için geçersiz operatör: {op}");
            if (!bool.TryParse(value, out var b)) throw new GridException($"'{f.Field}' için geçersiz değer: {value}");
            var p = field.Predicate!;
            return b ? p : Expression.Lambda<Func<T, bool>>(Expression.Not(p.Body), p.Parameters);
        }

        var selector = field.Selector ?? throw new GridException($"'{f.Field}' ilişkili veri filtresi çağıran tarafından uygulanmalı.");
        var param = selector.Parameters[0];
        var body = selector.Body;
        Expression pred = field.Type switch
        {
            GridFieldType.Text => BuildText(body, op, value, f.Field),
            GridFieldType.Enum => BuildEnum(body, op, value, field, f.Field),
            GridFieldType.Date => BuildDate(body, op, value, f.Field),
            GridFieldType.Number => BuildNumber(body, op, value, f.Field),
            GridFieldType.Guid => BuildGuid(body, op, value, f.Field),
            _ => throw new GridException($"Desteklenmeyen alan tipi: {field.Type}"),
        };
        return Expression.Lambda<Func<T, bool>>(pred, param);
    }

    private static string DefaultOp(GridFieldType t) => t switch
    {
        GridFieldType.Text => "contains",
        GridFieldType.Enum => "in",
        GridFieldType.Date => "between",
        _ => "eq",
    };

    private static readonly System.Reflection.MethodInfo ToLowerM = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly System.Reflection.MethodInfo ContainsM = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
    private static readonly System.Reflection.MethodInfo StartsWithM = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;

    private static Expression BuildText(Expression body, string op, string value, string name)
    {
        var lower = Expression.Call(body, ToLowerM);
        var term = Expression.Constant(value.ToLowerInvariant());
        Expression cmp = op switch
        {
            "contains" => Expression.Call(lower, ContainsM, term),
            "startswith" => Expression.Call(lower, StartsWithM, term),
            "eq" => Expression.Equal(lower, term),
            _ => throw new GridException($"'{name}' için geçersiz operatör: {op}"),
        };
        // null string → false (EF NULL semantiğiyle uyumlu; bellek içi değerlendirmede NRE olmasın)
        return Expression.AndAlso(Expression.NotEqual(body, Expression.Constant(null, typeof(string))), cmp);
    }

    private static Expression BuildEnum(Expression body, string op, string value, Field field, string name)
    {
        if (op is not ("in" or "eq")) throw new GridException($"'{name}' için geçersiz operatör: {op}");
        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (values.Count == 0) throw new GridException($"'{name}' için değer gerekli.");
        if (op == "eq" && values.Count > 1) throw new GridException($"'{name}' eq operatörü tek değer alır.");
        var hasNull = field.NullToken is not null && values.Remove(field.NullToken);
        if (field.Allowed is not null)
        {
            var bad = values.FirstOrDefault(v => !field.Allowed.Contains(v));
            if (bad is not null) throw new GridException($"'{name}' için geçersiz değer: {bad}");
        }
        Expression? pred = null;
        if (values.Count == 1) pred = Expression.Equal(body, Expression.Constant(values[0], typeof(string)));
        else if (values.Count > 1)
            pred = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(string) }, Expression.Constant(values), body);
        if (hasNull)
        {
            var isNull = Expression.Equal(body, Expression.Constant(null, typeof(string)));
            pred = pred is null ? isNull : Expression.OrElse(pred, isNull);
        }
        return pred ?? Expression.Constant(true);
    }

    private static Expression BuildDate(Expression body, string op, string value, string name)
    {
        var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
        var underlying = Nullable.GetUnderlyingType(body.Type) ?? body.Type;
        if (underlying == typeof(DateOnly)) return BuildDateOnly(body, op, value, parts, name);
        if (underlying != typeof(DateTime)) throw new GridException($"'{name}' tarih alanı değil.");
        Expression C(DateTime d) => Typed(Expression.Constant(d, typeof(DateTime)), body.Type);
        switch (op)
        {
            case "between":
            {
                if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) throw new GridException($"'{name}' between iki tarih ister (from,to).");
                var from = ParseDate(parts[0], name, endOfDayExclusive: false);
                var to = ParseDate(parts[1], name, endOfDayExclusive: true);
                if (to < from) throw new GridException($"'{name}' için bitiş, başlangıçtan önce olamaz.");
                return Expression.AndAlso(Expression.GreaterThanOrEqual(body, C(from)), Expression.LessThan(body, C(to)));
            }
            case "gte": return Expression.GreaterThanOrEqual(body, C(ParseDate(value, name, false)));
            case "gt": return Expression.GreaterThan(body, C(ParseDate(value, name, true)));   // gün verildiyse "o günden sonra"
            case "lt": return Expression.LessThan(body, C(ParseDate(value, name, false)));
            case "lte": return Expression.LessThan(body, C(ParseDate(value, name, true)));     // gün verildiyse gün dahil
            default: throw new GridException($"'{name}' için geçersiz operatör: {op}");
        }
    }

    /// <summary>DateOnly/DateOnly? kolonlar (gün hassasiyetli tarihler: fatura tarihi, vade, geçerlilik): between iki uç DAHİL; eq/gte/gt/lt/lte gün karşılaştırması.</summary>
    private static Expression BuildDateOnly(Expression body, string op, string value, string[] parts, string name)
    {
        Expression C(DateOnly d) => Typed(Expression.Constant(d, typeof(DateOnly)), body.Type);
        if (op == "between")
        {
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) throw new GridException($"'{name}' between iki tarih ister (from,to).");
            var from = ParseDay(parts[0], name); var to = ParseDay(parts[1], name);
            if (to < from) throw new GridException($"'{name}' için bitiş, başlangıçtan önce olamaz.");
            return Expression.AndAlso(Expression.GreaterThanOrEqual(body, C(from)), Expression.LessThanOrEqual(body, C(to)));
        }
        var d = ParseDay(value, name);
        return op switch
        {
            "eq" => Expression.Equal(body, C(d)),
            "gte" => Expression.GreaterThanOrEqual(body, C(d)),
            "gt" => Expression.GreaterThan(body, C(d)),
            "lt" => Expression.LessThan(body, C(d)),
            "lte" => Expression.LessThanOrEqual(body, C(d)),
            _ => throw new GridException($"'{name}' için geçersiz operatör: {op}"),
        };
    }

    /// <summary>"yyyy-MM-dd" → gün; ISO tarih-saat → İstanbul günü.</summary>
    private static DateOnly ParseDay(string s, string name)
    {
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TrTz));
        throw new GridException($"'{name}' için geçersiz tarih: {s}");
    }

    /// <summary>ISO-8601 → UTC; yalnız gün ("yyyy-MM-dd") → İstanbul gün başlangıcı (endOfDayExclusive ise ertesi gün başlangıcı).</summary>
    private static DateTime ParseDate(string s, string name, bool endOfDayExclusive)
    {
        if (s.Length == 10 && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            if (endOfDayExclusive) local = local.AddDays(1);
            return TimeZoneInfo.ConvertTimeToUtc(local, TrTz);
        }
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        throw new GridException($"'{name}' için geçersiz tarih: {s}");
    }

    private static Expression BuildNumber(Expression body, string op, string value, string name)
    {
        var underlying = Nullable.GetUnderlyingType(body.Type) ?? body.Type;
        Expression C(string s)
        {
            var norm = s.Replace(',', '.');
            if (!decimal.TryParse(norm, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                throw new GridException($"'{name}' için geçersiz sayı: {s}");
            object typed = underlying == typeof(decimal) ? d : Convert.ChangeType(d, underlying, CultureInfo.InvariantCulture);
            return Typed(Expression.Constant(typed, underlying), body.Type);
        }
        if (op == "between")
        {
            var parts = value.Split(';', 2, StringSplitOptions.TrimEntries); // sayı aralığı ayırıcı ';' (virgül ondalık olabilir)
            if (parts.Length != 2) throw new GridException($"'{name}' between iki sayı ister (min;max).");
            return Expression.AndAlso(Expression.GreaterThanOrEqual(body, C(parts[0])), Expression.LessThanOrEqual(body, C(parts[1])));
        }
        return op switch
        {
            "eq" => Expression.Equal(body, C(value)),
            "gt" => Expression.GreaterThan(body, C(value)),
            "gte" => Expression.GreaterThanOrEqual(body, C(value)),
            "lt" => Expression.LessThan(body, C(value)),
            "lte" => Expression.LessThanOrEqual(body, C(value)),
            _ => throw new GridException($"'{name}' için geçersiz operatör: {op}"),
        };
    }

    private static Expression BuildGuid(Expression body, string op, string value, string name)
    {
        if (op != "eq") throw new GridException($"'{name}' için geçersiz operatör: {op}");
        if (!System.Guid.TryParse(value, out var g)) throw new GridException($"'{name}' için geçersiz kimlik: {value}");
        return Expression.Equal(body, Typed(Expression.Constant(g, typeof(Guid)), body.Type));
    }

    private static Expression Typed(Expression constant, Type target) => constant.Type == target ? constant : Expression.Convert(constant, target);
}
