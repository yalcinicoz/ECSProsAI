using ECSPros.Storefront.Application.Queries.GetNewsletterSubscriptions;

namespace ECSPros.Api.Grid;

/// <summary>Bülten aboneleri Excel kolonları — e-posta kişisel veri, alan yetkisine bağlı.</summary>
public static class NewsletterExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<NewsletterExportRow>> All = new GridExportColumn<NewsletterExportRow>[]
    {
        new("email", "E-posta", r => r.Email, Locked: true, AlanYetkisi: "email"),
        new("isMember", "Üye", r => r.IsMember ? "Evet" : "Hayır"),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Kayıt", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
