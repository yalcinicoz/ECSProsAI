using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Accounts.Application.Queries.GetCurrentAccounts;

/// <summary>
/// Cari hesaplar DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + mevcut adlandırılmış
/// filtreler (accountType/ownerType/groupId/isActive) + global arama (unvan, kod, VKN, e-posta).
///
/// <para>Grup ADI <c>Group</c> navigation'ı üzerinden süzülür/sıralanır (aynı modül).
/// Telefon/e-posta kişisel veridir: Excel kolonlarında alan yetkisine bağlanır.</para>
/// </summary>
public static class CurrentAccountGrid
{
    public static readonly string[] AccountTypes = { "supplier", "customer", "both" };
    public static readonly string[] OwnerTypes = { "external", "member", "firm" };
    public static readonly string[] SupplierKinds = { "normal", "marketplace" };

    public static readonly GridSchema<CurrentAccount> Schema = new GridSchema<CurrentAccount>()
        .Text("code", a => a.Code)
        .Text("title", a => a.Title)
        .Text("taxNumber", a => a.TaxNumber)
        .Text("contactName", a => a.ContactName)
        .Text("phone", a => a.Phone)
        .Text("email", a => a.Email)
        .Text("city", a => a.City)
        .Text("group", a => a.Group != null ? a.Group.Name : null)
        .Enum("accountType", a => a.AccountType, AccountTypes)
        .Enum("ownerType", a => a.OwnerType, OwnerTypes)
        .Enum("supplierKind", a => a.SupplierKind, SupplierKinds)
        .Enum("currency", a => a.Currency)
        .Enum("country", a => a.Country)
        .Bool("isActive", a => a.IsActive)
        .Bool("hasCreditLimit", a => a.CreditLimit > 0)
        .Number("creditLimit", a => a.CreditLimit)
        .Date("createdAt", a => a.CreatedAt)
        .Guid("groupId", a => a.GroupId)
        .Guid("ownerId", a => a.OwnerId)
        .Sort("code", a => a.Code)
        .Sort("title", a => a.Title)
        .Sort("group", a => a.Group != null ? a.Group.Name : null)
        .Sort("accountType", a => a.AccountType)
        .Sort("ownerType", a => a.OwnerType)
        .Sort("taxNumber", a => a.TaxNumber)
        .Sort("contactName", a => a.ContactName)
        .Sort("phone", a => a.Phone)
        .Sort("email", a => a.Email)
        .Sort("city", a => a.City)
        .Sort("creditLimit", a => a.CreditLimit)
        .Sort("currency", a => a.Currency)
        .Sort("isActive", a => a.IsActive)
        .Sort("createdAt", a => a.CreatedAt)
        .DefaultSort(a => a.Title, desc: false)
        .TieBreaker(a => a.Id);

    public static IQueryable<CurrentAccount> ApplyNamed(IQueryable<CurrentAccount> query, CurrentAccountFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.AccountType)) query = query.Where(a => a.AccountType == f.AccountType);
        if (!string.IsNullOrWhiteSpace(f.OwnerType)) query = query.Where(a => a.OwnerType == f.OwnerType);
        if (f.GroupId.HasValue) query = query.Where(a => a.GroupId == f.GroupId);
        if (f.IsActive.HasValue) query = query.Where(a => a.IsActive == f.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(s) || a.Code.ToLower().Contains(s)
                || (a.TaxNumber != null && a.TaxNumber.Contains(s))
                || (a.Email != null && a.Email.ToLower().Contains(s)));
        }
        return query;
    }

    public static IQueryable<CurrentAccount> ApplyAll(IQueryable<CurrentAccount> query, CurrentAccountFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string TypeLabel(string s) => s switch
    {
        "supplier" => "Tedarikçi", "customer" => "Müşteri", "both" => "Tedarikçi + Müşteri", _ => s,
    };

    public static string OwnerLabel(string s) => s switch
    {
        "external" => "Dış", "member" => "Üye", "firm" => "Firma", _ => s,
    };
}

public record CurrentAccountFilters(
    string? AccountType = null, Guid? GroupId = null, bool? IsActive = null,
    string? Search = null, string? OwnerType = null);

public record CurrentAccountExportRow(
    string Code, string Title, string AccountType, string OwnerType, string? GroupName,
    string? TaxNumber, string? ContactName, string? Phone, string? Email, string? City, string? Country,
    decimal CreditLimit, string Currency, bool IsActive, DateTime CreatedAt);

public record ExportCurrentAccountsQuery(CurrentAccountFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<CurrentAccountExportRow>>>;

public class ExportCurrentAccountsQueryHandler(IAccountsDbContext db)
    : IRequestHandler<ExportCurrentAccountsQuery, Result<GridExportSource<CurrentAccountExportRow>>>
{
    public async Task<Result<GridExportSource<CurrentAccountExportRow>>> Handle(
        ExportCurrentAccountsQuery r, CancellationToken ct)
    {
        var q = CurrentAccountGrid.ApplyAll(db.CurrentAccounts.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<CurrentAccountExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = CurrentAccountGrid.Schema.ApplySort(q, r.Grid).Select(a => new CurrentAccountExportRow(
            a.Code, a.Title, a.AccountType, a.OwnerType, a.Group != null ? a.Group.Name : null,
            a.TaxNumber, a.ContactName, a.Phone, a.Email, a.City, a.Country,
            a.CreditLimit, a.Currency, a.IsActive, a.CreatedAt));
        return Result.Success(new GridExportSource<CurrentAccountExportRow>(count, rows));
    }
}
