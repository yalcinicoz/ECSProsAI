using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Yetkilendirme;

/// <summary>
/// Yetki grupları DataGrid şeması (2026-09-09, tur 11).
///
/// ★ AYRI UÇ: mevcut <c>GET /iam/permission-groups</c> TAM liste döner ve Kullanıcı Yetkileri
/// ekranının grup seçim kaynağıdır; sayfalamak onu kırar. Liste ekranı
/// <c>/iam/permission-groups/grid</c> kullanır.
///
/// <para>Grup = toplu yetki verme aracı; yalnız VERİR, yasaklamaz (Y4). "tam erişim" uyarısı
/// grup yetki sayısını aktif yetki sayısıyla karşılaştırır — bu karşılaştırma EKRANDA yapılır
/// (aktif yetki sayısı geçiş panosundan gelir), şemada <c>yetkiSayisi</c> süzgeci vardır.</para>
///
/// <para>⚠ jsonb adlar <c>GridJson.Text(NameI18n,"tr")</c> ile süzülür/sıralanır; sözlük
/// indeksleyicisi (<c>NameI18n["tr"]</c>) SQL'e çevrilmez.</para>
/// </summary>
public static class YetkiGrubuGrid
{
    /// <summary>Açılışta kurulan geçiş grubunun kodu — ekranda "geçici" rozeti bundan gelir.</summary>
    public const string GecisGrubuKodu = "gecis_tam_erisim";

    public static readonly GridSchema<Role> Schema = new GridSchema<Role>()
        .Text("ad", r => GridJson.Text(r.NameI18n, "tr"))
        .Text("code", r => r.Code)
        .Text("aciklama", r => GridJson.Text(r.DescriptionI18n, "tr"))
        .Bool("aktif", r => r.IsActive)
        .Bool("sistem", r => r.IsSystem)
        .Bool("gecisGrubu", r => r.Code == GecisGrubuKodu)
        .Bool("bosGrup", r => !r.UserRoles.Any(ur => !ur.IsDeleted))
        .Number("kullaniciSayisi", r => r.UserRoles.Count(ur => !ur.IsDeleted))
        .Number("yetkiSayisi", r => r.RolePermissions.Count(rp => !rp.IsDeleted))
        .Date("olusturma", r => r.CreatedAt)
        .Sort("ad", r => GridJson.Text(r.NameI18n, "tr"))
        .Sort("code", r => r.Code)
        .Sort("aciklama", r => GridJson.Text(r.DescriptionI18n, "tr"))
        .Sort("aktif", r => r.IsActive)
        .Sort("sistem", r => r.IsSystem)
        .Sort("kullaniciSayisi", r => r.UserRoles.Count(ur => !ur.IsDeleted))
        .Sort("yetkiSayisi", r => r.RolePermissions.Count(rp => !rp.IsDeleted))
        .Sort("olusturma", r => r.CreatedAt)
        // Eski ekranın sırası koda göreydi; kullanıcı grubu adıyla arar → ad varsayılan, kod eşitlik kırıcı.
        .DefaultSort(r => GridJson.Text(r.NameI18n, "tr"), desc: false)
        .TieBreaker(r => r.Code);

    public static IQueryable<Role> ApplyNamed(IQueryable<Role> query, YetkiGrubuFiltreleri f)
    {
        if (f.YalnizAktif) query = query.Where(r => r.IsActive);
        if (f.SistemHaric) query = query.Where(r => !r.IsSystem);
        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var t = f.Arama.Trim().ToLower();
            query = query.Where(r => r.Code.ToLower().Contains(t)
                || GridJson.Text(r.NameI18n, "tr")!.ToLower().Contains(t)
                || GridJson.Text(r.DescriptionI18n, "tr")!.ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<Role> ApplyAll(IQueryable<Role> query, YetkiGrubuFiltreleri f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    /// <summary>Satır/dışa aktarma projeksiyonu tek yerde — liste ile Excel aynı değerleri üretsin.</summary>
    public static IQueryable<YetkiGrubuSatiri> Projeksiyon(IQueryable<Role> query)
        => query.Select(r => new YetkiGrubuSatiri(
            r.Id, r.Code,
            GridJson.Text(r.NameI18n, "tr") ?? r.Code,
            GridJson.Text(r.DescriptionI18n, "tr"),
            r.IsActive, r.IsSystem,
            r.UserRoles.Count(ur => !ur.IsDeleted),
            r.RolePermissions.Count(rp => !rp.IsDeleted),
            r.Code == GecisGrubuKodu));
}

public record YetkiGrubuFiltreleri(bool YalnizAktif = false, bool SistemHaric = false, string? Arama = null);

public record GetYetkiGruplariGridQuery(
    YetkiGrubuFiltreleri Filtreler, int Page = 1, int PageSize = 50, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<YetkiGrubuSatiri>>>;

public class GetYetkiGruplariGridQueryHandler(IIamDbContext db)
    : IRequestHandler<GetYetkiGruplariGridQuery, Result<PagedResult<YetkiGrubuSatiri>>>
{
    public async Task<Result<PagedResult<YetkiGrubuSatiri>>> Handle(GetYetkiGruplariGridQuery r, CancellationToken ct)
    {
        var q = YetkiGrubuGrid.ApplyAll(db.Roles.AsNoTracking(), r.Filtreler, r.Grid);
        var toplam = await q.CountAsync(ct);   // sayım SAYFALAMADAN ÖNCE
        var satirlar = await YetkiGrubuGrid.Projeksiyon(YetkiGrubuGrid.Schema.ApplySort(q, r.Grid)
                .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<YetkiGrubuSatiri>(satirlar, toplam, r.Page, r.PageSize));
    }
}

public record YetkiGrubuExportRow(
    string Ad, string Code, string? Aciklama, int KullaniciSayisi, int YetkiSayisi,
    bool Aktif, bool Sistem, bool GecisGrubu);

public record ExportYetkiGruplariQuery(YetkiGrubuFiltreleri Filtreler, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<YetkiGrubuExportRow>>>;

public class ExportYetkiGruplariQueryHandler(IIamDbContext db)
    : IRequestHandler<ExportYetkiGruplariQuery, Result<GridExportSource<YetkiGrubuExportRow>>>
{
    public async Task<Result<GridExportSource<YetkiGrubuExportRow>>> Handle(ExportYetkiGruplariQuery r, CancellationToken ct)
    {
        var q = YetkiGrubuGrid.ApplyAll(db.Roles.AsNoTracking(), r.Filtreler, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<YetkiGrubuExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = YetkiGrubuGrid.Schema.ApplySort(q, r.Grid).Select(role => new YetkiGrubuExportRow(
            GridJson.Text(role.NameI18n, "tr") ?? role.Code, role.Code,
            GridJson.Text(role.DescriptionI18n, "tr"),
            role.UserRoles.Count(ur => !ur.IsDeleted),
            role.RolePermissions.Count(rp => !rp.IsDeleted),
            role.IsActive, role.IsSystem, role.Code == YetkiGrubuGrid.GecisGrubuKodu));
        return Result.Success(new GridExportSource<YetkiGrubuExportRow>(count, rows));
    }
}
