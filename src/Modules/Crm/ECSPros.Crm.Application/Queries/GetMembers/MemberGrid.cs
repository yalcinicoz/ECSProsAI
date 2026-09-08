using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMembers;

/// <summary>
/// Üyeler DataGrid şeması (plan F4): beyaz listeli sıralama/filtre + mevcut adlandırılmış filtreler tek yerden
/// (liste ve export AYNI filtre modeli). Consents jsonb ve ilişkiler şemada yok.
/// </summary>
public static class MemberGrid
{
    public static readonly string[] Genders = { "male", "female", "unisex" };

    public static readonly GridSchema<Member> Schema = new GridSchema<Member>()
        .Text("firstName", m => m.FirstName)
        .Text("lastName", m => m.LastName)
        .Text("email", m => m.Email)
        .Text("phone", m => m.Phone)
        .Text("companyName", m => m.CompanyName)
        .Text("taxNumber", m => m.TaxNumber)
        .Enum("gender", m => m.Gender, Genders, nullToken: "none")
        .Bool("isActive", m => m.IsActive)
        .Bool("isRegistered", m => m.IsRegistered)
        .Bool("isEmailVerified", m => m.IsEmailVerified)
        .Bool("isPhoneVerified", m => m.IsPhoneVerified)
        .Date("createdAt", m => m.CreatedAt)
        .Date("lastLoginAt", m => m.LastLoginAt)
        .Guid("memberGroupId", m => m.MemberGroupId)
        .Guid("cityId", m => m.CityId)
        .Number("legacyMemberId", m => m.LegacyMemberId)
        .Sort("firstName", m => m.FirstName)
        .Sort("lastName", m => m.LastName)
        .Sort("email", m => m.Email)
        .Sort("phone", m => m.Phone)
        .Sort("createdAt", m => m.CreatedAt)
        .Sort("lastLoginAt", m => m.LastLoginAt)
        .Sort("isActive", m => m.IsActive)
        .Sort("isRegistered", m => m.IsRegistered)
        .DefaultSort(m => m.CreatedAt, desc: true)
        .TieBreaker(m => m.Id);

    /// <summary>Mevcut adlandırılmış filtre (activeOnly) + global arama (e-posta, telefon, ad, soyad; ek: şirket adı).</summary>
    public static IQueryable<Member> ApplyNamed(IQueryable<Member> query, MemberListFilters f)
    {
        if (f.ActiveOnly) query = query.Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            query = query.Where(m =>
                (m.Email != null && m.Email.ToLower().Contains(s)) ||
                (m.Phone != null && m.Phone.Contains(s)) ||
                m.FirstName.ToLower().Contains(s) ||
                m.LastName.ToLower().Contains(s) ||
                (m.CompanyName != null && m.CompanyName.ToLower().Contains(s)));
        }
        return query;
    }

    public static IQueryable<Member> ApplyAll(IQueryable<Member> query, MemberListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string GenderLabel(string? g) => g switch
    {
        "male" => "Erkek", "female" => "Kadın", "unisex" => "Belirtilmemiş", null => "", _ => g,
    };
}

/// <summary>Üye listesinin adlandırılmış filtreleri (GET parametreleri / export gövdesi "named").</summary>
public record MemberListFilters(string? Search = null, bool ActiveOnly = true);

/// <summary>Excel satırı — export kolon tanımı (Api) bu alanlardan seçer.</summary>
public record MemberExportRow(
    string FirstName, string LastName, string? Email, string? Phone, bool IsRegistered, bool IsActive,
    bool IsEmailVerified, bool IsPhoneVerified, string? Gender, DateOnly? BirthDate, string? CompanyName,
    string? TaxOffice, string? TaxNumber, DateTime CreatedAt, DateTime? LastLoginAt, int? LegacyMemberId);

public record ExportMembersQuery(MemberListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<MemberExportRow>>>;

public class ExportMembersQueryHandler(ICrmDbContext db) : IRequestHandler<ExportMembersQuery, Result<GridExportSource<MemberExportRow>>>
{
    public async Task<Result<GridExportSource<MemberExportRow>>> Handle(ExportMembersQuery r, CancellationToken ct)
    {
        var q = MemberGrid.ApplyAll(db.Members.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<MemberExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = MemberGrid.Schema.ApplySort(q, r.Grid).Select(m => new MemberExportRow(
            m.FirstName, m.LastName, m.Email, m.Phone, m.IsRegistered, m.IsActive,
            m.IsEmailVerified, m.IsPhoneVerified, m.Gender, m.BirthDate, m.CompanyName,
            m.TaxOffice, m.TaxNumber, m.CreatedAt, m.LastLoginAt, m.LegacyMemberId));
        return Result.Success(new GridExportSource<MemberExportRow>(count, rows));
    }
}
