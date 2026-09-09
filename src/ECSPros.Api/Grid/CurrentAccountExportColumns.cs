using ECSPros.Accounts.Application.Queries.GetCurrentAccounts;

namespace ECSPros.Api.Grid;

/// <summary>Cari hesaplar Excel kolonları — telefon/e-posta kişisel veri, alan yetkisine bağlı.</summary>
public static class CurrentAccountExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<CurrentAccountExportRow>> All = new GridExportColumn<CurrentAccountExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("title", "Unvan", r => r.Title),
        new("accountType", "Tip", r => CurrentAccountGrid.TypeLabel(r.AccountType)),
        new("ownerType", "Sahiplik", r => CurrentAccountGrid.OwnerLabel(r.OwnerType)),
        new("group", "Grup", r => r.GroupName),
        new("taxNumber", "VKN/TCKN", r => r.TaxNumber),
        new("contactName", "Yetkili", r => r.ContactName),
        new("phone", "Telefon", r => r.Phone, AlanYetkisi: "phone"),
        new("email", "E-posta", r => r.Email, AlanYetkisi: "email"),
        new("city", "Şehir", r => r.City),
        new("country", "Ülke", r => r.Country),
        new("creditLimit", "Kredi Limiti", r => r.CreditLimit),
        new("currency", "Para Birimi", r => r.Currency),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
