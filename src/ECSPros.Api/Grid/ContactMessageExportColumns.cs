using ECSPros.Storefront.Application.Queries.GetContactMessages;

namespace ECSPros.Api.Grid;

/// <summary>İletişim mesajları Excel kolonları — ad/e-posta/telefon kişisel veri, alan yetkisine bağlı.</summary>
public static class ContactMessageExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ContactMessageExportRow>> All = new GridExportColumn<ContactMessageExportRow>[]
    {
        new("name", "Ad", r => r.Name, Locked: true),
        new("email", "E-posta", r => r.Email, AlanYetkisi: "email"),
        new("phone", "Telefon", r => r.Phone, AlanYetkisi: "phone"),
        new("subject", "Konu", r => r.Subject),
        new("message", "Mesaj", r => r.Message),
        new("status", "Durum", r => ContactMessageGrid.StatusLabel(r.Status)),
        new("createdAt", "Tarih", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
