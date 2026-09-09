using ECSPros.Storefront.Application.Queries.GetCardMessages;

namespace ECSPros.Api.Grid;

/// <summary>Kart mesajları Excel kolonları.</summary>
public static class CardMessageExportColumns
{
    private static string KapsamEtiketi(string t) => t switch
    {
        "all" => "Tüm ürünler", "category" => "Kategori", "products" => "Ürün listesi", _ => t,
    };

    public static readonly IReadOnlyList<GridExportColumn<CardMessageExportRow>> All = new GridExportColumn<CardMessageExportRow>[]
    {
        new("message", "Mesaj", r => r.Mesaj, Locked: true),
        new("slot", "Alan", r => r.Slot),
        new("icon", "İkon", r => r.Icon),
        new("color", "Renk", r => r.Color),
        new("scopeType", "Kapsam", r => KapsamEtiketi(r.ScopeType)),
        new("kapsamAdet", "Kapsam Adedi", r => r.KapsamAdet),
        new("startDate", "Başlangıç", r => r.StartDate),
        new("endDate", "Bitiş", r => r.EndDate),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
    };
}
