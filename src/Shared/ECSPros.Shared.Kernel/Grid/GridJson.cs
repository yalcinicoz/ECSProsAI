namespace ECSPros.Shared.Kernel.Grid;

/// <summary>
/// jsonb sözlük alanlarında (NameI18n gibi) sunucu taraflı filtre/sıralama için EF DbFunction: SQL'de
/// <c>jsonb_extract_path_text(col, 'tr')</c>. Her DbContext <c>modelBuilder.HasDbFunction(GridJson.TextMethod).HasName("jsonb_extract_path_text")</c>
/// ile kaydeder; şema tanımında <c>.Text("name", p =&gt; GridJson.Text(p.NameI18n, "tr"))</c>. Bellek içinde de çalışır (testler).
/// </summary>
public static class GridJson
{
    public static string? Text(Dictionary<string, string>? dict, string key)
        => dict is not null && dict.TryGetValue(key, out var v) ? v : null;

    public static readonly System.Reflection.MethodInfo TextMethod =
        typeof(GridJson).GetMethod(nameof(Text), new[] { typeof(Dictionary<string, string>), typeof(string) })!;

    /// <summary>FAZ 15.4h (2026-09-10): <c>Dictionary&lt;string, object&gt;</c> jsonb kolonları (Order.CustomerNotes) için aynı
    /// fonksiyon — değer metin değilse (nesne/sayı) SQL tarafı jsonb_extract_path_text yine metin döner, bellek tarafı ToString.</summary>
    public static string? TextObj(Dictionary<string, object>? dict, string key)
        => dict is not null && dict.TryGetValue(key, out var v) && v is not null ? v.ToString() : null;
    public static readonly System.Reflection.MethodInfo TextObjMethod =
        typeof(GridJson).GetMethod(nameof(TextObj), new[] { typeof(Dictionary<string, object>), typeof(string) })!;

}
