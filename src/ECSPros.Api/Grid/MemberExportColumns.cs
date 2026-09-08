using ECSPros.Crm.Application.Queries.GetMembers;

namespace ECSPros.Api.Grid;

/// <summary>Üyeler Excel kolonları (anahtarlar panel DataGrid kolon anahtarlarıyla aynı; ad + e-posta kilitli).</summary>
public static class MemberExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<MemberExportRow>> All = new GridExportColumn<MemberExportRow>[]
    {
        new("name", "Ad Soyad", r => $"{r.FirstName} {r.LastName}".Trim(), Locked: true),
        new("email", "E-posta", r => r.Email, Locked: true),
        new("phone", "Telefon", r => r.Phone),
        new("isRegistered", "Üyelik", r => r.IsRegistered ? "Kayıtlı" : "Misafir"),
        new("isActive", "Durum", r => r.IsActive ? "Aktif" : "Pasif"),
        new("isEmailVerified", "E-posta Doğrulandı", r => r.IsEmailVerified ? "Evet" : "Hayır"),
        new("isPhoneVerified", "Telefon Doğrulandı", r => r.IsPhoneVerified ? "Evet" : "Hayır"),
        new("gender", "Cinsiyet", r => MemberGrid.GenderLabel(r.Gender)),
        new("birthDate", "Doğum Tarihi", r => r.BirthDate?.ToDateTime(TimeOnly.MinValue)),
        new("companyName", "Şirket", r => r.CompanyName),
        new("taxOffice", "Vergi Dairesi", r => r.TaxOffice),
        new("taxNumber", "Vergi No", r => r.TaxNumber),
        new("createdAt", "Kayıt", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("lastLoginAt", "Son Giriş", r => r.LastLoginAt is { } d ? GridExportWriter.ToIstanbul(d) : null),
        new("legacyMemberId", "Eski Üye No", r => r.LegacyMemberId),
    };
}
