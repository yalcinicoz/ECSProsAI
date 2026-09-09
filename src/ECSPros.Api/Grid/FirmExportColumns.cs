using ECSPros.Core.Application.Queries.GetFirms;

namespace ECSPros.Api.Grid;

/// <summary>Firmalar Excel kolonları — telefon/e-posta/adres alan yetkisine bağlı.</summary>
public static class FirmExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<FirmExportRow>> All = new GridExportColumn<FirmExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("taxNumber", "VKN", r => r.TaxNumber),
        new("taxOffice", "Vergi Dairesi", r => r.TaxOffice),
        new("phone", "Telefon", r => r.Phone, AlanYetkisi: "phone"),
        new("email", "E-posta", r => r.Email, AlanYetkisi: "email"),
        new("address", "Adres", r => r.Address, AlanYetkisi: "address"),
        new("platformCount", "Platform", r => r.PlatformCount),
        new("isMain", "Ana Firma", r => r.IsMain ? "Evet" : "Hayır"),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
