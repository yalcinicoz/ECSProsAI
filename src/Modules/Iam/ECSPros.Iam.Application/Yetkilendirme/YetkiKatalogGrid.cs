using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Yetkilendirme;

/// <summary>
/// Yetki kataloğu DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /iam/permissions</c> TAM liste döner ve yetki grubu / kullanıcı yetkisi
/// ekranlarının kaynağıdır; sayfalamak onları kırar. Liste ekranı <c>/iam/permissions/grid</c> kullanır.
///
/// ★ Ekran eskiden satırları MODÜLE göre gruplayıp ayrı kartlarda gösteriyordu; DataGrid düz tablo
/// olduğu için gruplama "MODÜL" sütunu + filtresi olarak korundu (modüle göre sırala/filtrele).
///
/// <para><c>sorunlu</c>: kodda karşılığı olmayan ya da pasif yetki — ekranın "yalnız sorunlular"
/// süzgeci bu alanı kullanır. Kodda karşılığı olmayan yetki hiçbir şeyi korumaz, yalnız yanlış
/// güven verir; bu yüzden ayrı bir bayrak olarak süzülebilir.</para>
/// </summary>
public static class YetkiKatalogGrid
{
    public static readonly string[] Turler = { "page", "action", "field" };

    public static readonly GridSchema<Permission> Schema = new GridSchema<Permission>()
        .Text("code", p => p.Code)
        .Text("ad", p => GridJson.Text(p.NameI18n, "tr"))
        .Text("aciklama", p => GridJson.Text(p.DescriptionI18n, "tr"))
        .Text("sayfa", p => p.PageCode)
        .Enum("modul", p => p.Module)
        .Enum("tur", p => p.Kind, Turler)
        .Bool("aktif", p => p.IsActive)
        .Bool("kanalKapsamli", p => p.ChannelScoped)
        .Bool("koddaTanimli", p => p.IsCodeDefined)
        .Bool("sorunlu", p => !p.IsCodeDefined || !p.IsActive)
        .Bool("kullanimda", p => p.RolePermissions.Any(rp => !rp.IsDeleted) || p.UserPermissions.Any(up => !up.IsDeleted))
        .Number("sira", p => p.SortOrder)
        .Number("grupSayisi", p => p.RolePermissions.Count(rp => !rp.IsDeleted))
        .Number("kullaniciSayisi", p => p.UserPermissions.Count(up => !up.IsDeleted))
        .Sort("code", p => p.Code)
        .Sort("ad", p => GridJson.Text(p.NameI18n, "tr"))
        .Sort("modul", p => p.Module)
        .Sort("tur", p => p.Kind)
        .Sort("sayfa", p => p.PageCode)
        .Sort("aktif", p => p.IsActive)
        .Sort("kanalKapsamli", p => p.ChannelScoped)
        .Sort("koddaTanimli", p => p.IsCodeDefined)
        .Sort("sira", p => p.SortOrder)
        .Sort("grupSayisi", p => p.RolePermissions.Count(rp => !rp.IsDeleted))
        .Sort("kullaniciSayisi", p => p.UserPermissions.Count(up => !up.IsDeleted))
        // Ekranın doğal sırası: modül, sonra sıra, sonra kod (eski gruplu görünümün sırası).
        .DefaultSort(p => p.Module, desc: false)
        .TieBreaker(p => p.Code);

    public static IQueryable<Permission> ApplyNamed(IQueryable<Permission> query, YetkiKatalogFiltreleri f)
    {
        if (f.YalnizAktif) query = query.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Tur)) query = query.Where(p => p.Kind == f.Tur);
        if (f.YalnizSorunlu) query = query.Where(p => !p.IsCodeDefined || !p.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var t = f.Arama.Trim().ToLower();
            query = query.Where(p => p.Code.ToLower().Contains(t)
                || GridJson.Text(p.NameI18n, "tr")!.ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<Permission> ApplyAll(IQueryable<Permission> query, YetkiKatalogFiltreleri f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string TurEtiketi(string t) => t switch
    {
        "page" => "Sayfa", "field" => "Alan", "action" => "İşlem", _ => t,
    };
}

public record YetkiKatalogFiltreleri(
    bool YalnizAktif = false, string? Tur = null, bool YalnizSorunlu = false, string? Arama = null);

public record GetYetkiKataloguGridQuery(
    YetkiKatalogFiltreleri Filtreler, int Page = 1, int PageSize = 50, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<YetkiKatalogSatiri>>>;

public class GetYetkiKataloguGridQueryHandler(IIamDbContext db)
    : IRequestHandler<GetYetkiKataloguGridQuery, Result<PagedResult<YetkiKatalogSatiri>>>
{
    public async Task<Result<PagedResult<YetkiKatalogSatiri>>> Handle(GetYetkiKataloguGridQuery r, CancellationToken ct)
    {
        var q = YetkiKatalogGrid.ApplyAll(db.Permissions.AsNoTracking(), r.Filtreler, r.Grid);
        var toplam = await q.CountAsync(ct);
        var satirlar = await YetkiKatalogGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(p => new YetkiKatalogSatiri(
                p.Id, p.Code,
                GridJson.Text(p.NameI18n, "tr") ?? p.Code,
                GridJson.Text(p.DescriptionI18n, "tr"),
                p.Module, p.PageCode, p.Kind, p.ChannelScoped, p.IsActive, p.IsCodeDefined, p.SortOrder,
                p.RolePermissions.Count(rp => !rp.IsDeleted),
                p.UserPermissions.Count(up => !up.IsDeleted)))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<YetkiKatalogSatiri>(satirlar, toplam, r.Page, r.PageSize));
    }
}

public record YetkiKatalogExportRow(
    string Code, string Ad, string? Aciklama, string Modul, string? Sayfa, string Tur,
    bool KanalKapsamli, bool Aktif, bool KoddaTanimli, int Sira, int GrupSayisi, int KullaniciSayisi);

public record ExportYetkiKataloguQuery(YetkiKatalogFiltreleri Filtreler, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<YetkiKatalogExportRow>>>;

public class ExportYetkiKataloguQueryHandler(IIamDbContext db)
    : IRequestHandler<ExportYetkiKataloguQuery, Result<GridExportSource<YetkiKatalogExportRow>>>
{
    public async Task<Result<GridExportSource<YetkiKatalogExportRow>>> Handle(ExportYetkiKataloguQuery r, CancellationToken ct)
    {
        var q = YetkiKatalogGrid.ApplyAll(db.Permissions.AsNoTracking(), r.Filtreler, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<YetkiKatalogExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = YetkiKatalogGrid.Schema.ApplySort(q, r.Grid).Select(p => new YetkiKatalogExportRow(
            p.Code, GridJson.Text(p.NameI18n, "tr") ?? p.Code, GridJson.Text(p.DescriptionI18n, "tr"),
            p.Module, p.PageCode, p.Kind, p.ChannelScoped, p.IsActive, p.IsCodeDefined, p.SortOrder,
            p.RolePermissions.Count(rp => !rp.IsDeleted), p.UserPermissions.Count(up => !up.IsDeleted)));
        return Result.Success(new GridExportSource<YetkiKatalogExportRow>(count, rows));
    }
}
