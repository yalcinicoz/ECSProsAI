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
}
