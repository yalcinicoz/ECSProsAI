namespace ECSPros.Shared.Kernel.Authorization;

/// <summary>Bir yetkinin kullanıcıya nereden geldiği (tasarım §C.1 sırası).</summary>
public enum YetkiKaynakTipi
{
    /// <summary>Yetki grubundan (rol) geliyor — gruplar yalnız VERİR.</summary>
    Grup = 0,
    /// <summary>Kullanıcıya özel verilmiş istisna.</summary>
    KullaniciVer = 1,
    /// <summary>Kullanıcıda kaldırılmış istisna — hesapta EN SON uygulanır, en güçlüdür.</summary>
    KullaniciKaldir = 2,
}

/// <summary>
/// Hesaba giren tek bir kaynak satırı.
/// <paramref name="Kanallar"/>: kanal kapsamlı yetkilerde kanal kümesi; kapsamsızlarda yok sayılır.
/// </summary>
public readonly record struct YetkiKaynagi(
    string Key,
    bool KanalKapsamli,
    IReadOnlyCollection<Guid>? Kanallar,
    YetkiKaynakTipi Tip);

/// <summary>
/// Bir kullanıcının EFEKTİF yetkileri: hangi yetki, hangi kanallarda.
/// Kanal kapsamsız yetkilerde kanal kümesi <c>null</c>'dır (her yerde geçerli).
/// </summary>
public sealed class EfektifYetkiler
{
    public static readonly EfektifYetkiler Bos = new(false, new Dictionary<string, HashSet<Guid>?>());

    private readonly Dictionary<string, HashSet<Guid>?> _yetkiler;

    public EfektifYetkiler(bool superAdmin, Dictionary<string, HashSet<Guid>?> yetkiler)
    {
        SuperAdmin = superAdmin;
        _yetkiler = yetkiler;
    }

    /// <summary>K5: süper admin tüm kontrolleri geçer (audit devam eder).</summary>
    public bool SuperAdmin { get; }

    public IReadOnlyCollection<string> Keyler => _yetkiler.Keys;

    /// <summary>Yetki var mı? (kanal kapsamlıda: en az bir kanalda var mı)</summary>
    public bool Var(string key) => SuperAdmin || _yetkiler.ContainsKey(key);

    /// <summary>Belirli bir KANALDA yetki var mı? Kapsamsız yetkide kanal yok sayılır.</summary>
    public bool Var(string key, Guid kanalId)
    {
        if (SuperAdmin) return true;
        if (!_yetkiler.TryGetValue(key, out var kanallar)) return false;
        return kanallar is null || kanallar.Contains(kanalId);
    }

    /// <summary>Yetkinin geçerli olduğu kanallar; null = kanal kapsamsız (hepsi) ya da yetki yok.
    /// Ayrımı <see cref="Var(string)"/> ile yapın. Süper adminde her zaman null döner (sınırsız).</summary>
    public IReadOnlySet<Guid>? Kanallar(string key)
    {
        if (SuperAdmin) return null;
        return _yetkiler.TryGetValue(key, out var k) ? k : null;
    }
}

/// <summary>
/// Efektif yetki hesabı — TEK kural (tasarım §C.1). Sıra bağlayıcıdır:
/// <c>(gruplar ∪ kullanıcı-ver) − kullanıcı-kaldır</c>; kaldırma her zaman EN SONDA uygulanır,
/// böylece "kullanıcı özel kararı grup sonucundan güçlüdür" (Ek-1) tek satırda garanti edilir.
///
/// Kanal kapsamlı yetkide sonuç kümesi BOŞSA yetki yok sayılır (default deny).
/// Kanal kapsamsız yetkide kanal kümesi yoktur; "kaldır" satırı yetkiyi tümden düşürür.
/// </summary>
public static class EfektifYetkiHesabi
{
    public static EfektifYetkiler Hesapla(bool superAdmin, IEnumerable<YetkiKaynagi> kaynaklar)
    {
        if (superAdmin) return new EfektifYetkiler(true, new Dictionary<string, HashSet<Guid>?>());

        var verilen = new Dictionary<string, HashSet<Guid>?>(StringComparer.Ordinal);
        var kaldirilan = new Dictionary<string, HashSet<Guid>?>(StringComparer.Ordinal);
        var kapsamli = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var k in kaynaklar)
        {
            if (string.IsNullOrWhiteSpace(k.Key)) continue;
            kapsamli[k.Key] = k.KanalKapsamli;
            var hedef = k.Tip == YetkiKaynakTipi.KullaniciKaldir ? kaldirilan : verilen;

            if (!k.KanalKapsamli)
            {
                hedef[k.Key] = null;   // kapsamsız: yalnız varlık
                continue;
            }

            if (!hedef.TryGetValue(k.Key, out var küme) || küme is null)
                hedef[k.Key] = küme = new HashSet<Guid>();
            if (k.Kanallar is not null)
                foreach (var kanal in k.Kanallar) küme.Add(kanal);
        }

        var sonuc = new Dictionary<string, HashSet<Guid>?>(StringComparer.Ordinal);
        foreach (var (key, verilenKanallar) in verilen)
        {
            var kanalKapsamli = kapsamli.GetValueOrDefault(key);
            if (!kanalKapsamli)
            {
                if (kaldirilan.ContainsKey(key)) continue;   // kullanıcıda kaldırılmış
                sonuc[key] = null;
                continue;
            }

            var küme = verilenKanallar is null ? new HashSet<Guid>() : new HashSet<Guid>(verilenKanallar);
            if (kaldirilan.TryGetValue(key, out var kaldirilanKanallar) && kaldirilanKanallar is not null)
                küme.ExceptWith(kaldirilanKanallar);

            if (küme.Count == 0) continue;   // kanal kalmadıysa yetki yok (default deny)
            sonuc[key] = küme;
        }

        return new EfektifYetkiler(false, sonuc);
    }
}
