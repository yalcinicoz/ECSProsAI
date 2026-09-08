using ECSPros.Iam.Application.Queries.GetUsers;

namespace ECSPros.Api.Grid;

/// <summary>Kullanıcılar (personel) Excel kolonları — anahtarlar panel DataGrid kolon anahtarlarıyla aynı; kullanıcı adı kilitli.</summary>
public static class UserExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<UserExportRow>> All = new GridExportColumn<UserExportRow>[]
    {
        new("username", "Kullanıcı Adı", r => r.Username, Locked: true),
        new("name", "Ad Soyad", r => $"{r.FirstName} {r.LastName}".Trim()),
        new("email", "E-posta", r => r.Email),
        new("phone", "Telefon", r => r.Phone),
        new("department", "Departman", r => r.Department),
        new("jobTitle", "Ünvan", r => r.JobTitle),
        new("roles", "Roller", r => r.Roles),
        new("lastLoginAt", "Son Giriş", r => r.LastLoginAt is { } d ? GridExportWriter.ToIstanbul(d) : null),
        new("isActive", "Durum", r => r.IsActive ? "Aktif" : "Pasif"),
        new("createdAt", "Kayıt", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
