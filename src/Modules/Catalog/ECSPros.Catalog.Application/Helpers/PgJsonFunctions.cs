namespace ECSPros.Catalog.Application.Helpers;

/// <summary>
/// EF sorgularında JSONB sözlük kolonlarından (ör. NameI18n) tek anahtar okumak için
/// veritabanı fonksiyon eşlemesi. CatalogDbContext.OnModelCreating'de PostgreSQL'in
/// yerleşik <c>jsonb_extract_path_text</c> fonksiyonuna bağlanır; sorgu dışında
/// çağrılırsa gövdedeki client-side karşılığı çalışır.
/// </summary>
public static class PgJsonFunctions
{
    public static string? JsonText(Dictionary<string, string> jsonb, string key)
        => jsonb.TryGetValue(key, out var value) ? value : null;

    /// <summary>H10: jsonb string dizisinde verilen anahtarlardan EN AZ BİRİ var mı —
    /// PostgreSQL <c>jsonb_exists_any</c> (?| operatörünün fonksiyon hâli). Product.Tags
    /// jsonb olduğundan text[] çevirili LINQ (Any/Contains) çevrilemiyor.</summary>
    public static bool JsonExistsAny(List<string> jsonb, string[] keys)
        => jsonb.Any(keys.Contains);
}
