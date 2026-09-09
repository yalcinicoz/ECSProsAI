using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetUiTranslations;

/// <summary>
/// Arayüz çevirileri DataGrid şeması (2026-09-09, tur 12).
///
/// ★ EKRAN BİR PİVOT: satır = ANAHTAR, kolon = DİL, hücre = düzenlenebilir değer. Veritabanında ise
/// her satır tek bir (anahtar, dil) çiftidir. Bu yüzden sayfalama ANAHTAR üzerinden yapılır:
///  1. şema süzgeçleri ham çeviri satırlarına uygulanır → hangi ANAHTARLARIN kaldığı bulunur,
///  2. sayfaya düşen anahtarlar için TÜM diller yeniden çekilir → pivot eksiksiz görünür.
/// Yani "en değeri X içeren" süzgeci anahtarı listede tutar ama satırın Türkçe hücresini gizlemez;
/// süzgeç satır seçer, hücreleri kırpmaz.
///
/// ★ AYRI UÇ: mevcut <c>GET /core/ui-translations</c> bir grubun TAM listesini döner (panelin çeviri
/// sözlüğünü besler); sayfalamak onu kırar. Liste ekranı <c>/core/ui-translations/grid</c> kullanır.
///
/// <para>Sınır: dil kolonları veritabanında AYNI alandır (<c>Value</c>) — bu yüzden dil başına ayrı
/// başlık süzgeci yoktur; "hangi dilde ara" araç çubuğundaki dil seçicisidir (<c>dil</c> named filtresi),
/// değer süzgeci de <c>value</c>. Sıralama yalnız anahtar ve son güncelleme üzerinden anlamlıdır.</para>
/// </summary>
public static class UiTranslationGrid
{
    public static readonly GridSchema<UiTranslation> Schema = new GridSchema<UiTranslation>()
        .Text("key", t => t.Key)
        .Text("value", t => t.Value)
        .Enum("lang", t => t.Lang)
        .Enum("namespace", t => t.Namespace)
        .Bool("bos", t => t.Value == "")
        .Date("guncelleme", t => t.UpdatedAt ?? t.CreatedAt)
        // ⚠ Sıralama pivot kurulurken ANAHTAR listesi üzerinde elle uygulanır (Distinct sonrası ORDER BY
        // korunmaz). Bu iki bildirim istemcideki sortable kolonların sunucu karşılığıdır — anahtar
        // denetim testi (DataGridKeyConsistencyTests) bunları arar.
        .Sort("key", t => t.Key)
        .Sort("guncelleme", t => t.UpdatedAt ?? t.CreatedAt)
        .DefaultSort(t => t.Key, desc: false)
        .TieBreaker(t => t.Lang);

    public static IQueryable<UiTranslation> ApplyNamed(IQueryable<UiTranslation> query, UiTranslationFiltreleri f)
    {
        if (!string.IsNullOrWhiteSpace(f.Namespace)) query = query.Where(t => t.Namespace == f.Namespace);
        if (!string.IsNullOrWhiteSpace(f.Dil)) query = query.Where(t => t.Lang == f.Dil);
        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var a = f.Arama.Trim().ToLower();
            query = query.Where(t => t.Key.ToLower().Contains(a) || t.Value.ToLower().Contains(a));
        }
        return query;
    }

    public static IQueryable<UiTranslation> ApplyAll(IQueryable<UiTranslation> query, UiTranslationFiltreleri f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

/// <param name="Dil">Yalnız bu dilin satırlarında ara (pivot kolonu süzgecinin karşılığı).</param>
/// <param name="EksikOlanlar">true → aktif dillerin hepsinde dolu OLMAYAN anahtarlar.</param>
public record UiTranslationFiltreleri(
    string? Namespace = null, string? Dil = null, bool EksikOlanlar = false, string? Arama = null);

/// <summary>Pivot satırı: anahtar + dil→değer sözlüğü (eksik dil sözlükte yoktur).</summary>
public record UiTranslationGridRow(
    string Namespace, string Key, Dictionary<string, string> Values,
    int DoluDilSayisi, bool EksikVar, DateTime? Guncelleme);

public record GetUiTranslationsGridQuery(
    UiTranslationFiltreleri Filtreler, int Page = 1, int PageSize = 50, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<UiTranslationGridRow>>>;

public class GetUiTranslationsGridQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetUiTranslationsGridQuery, Result<PagedResult<UiTranslationGridRow>>>
{
    public async Task<Result<PagedResult<UiTranslationGridRow>>> Handle(GetUiTranslationsGridQuery r, CancellationToken ct)
    {
        var aktifDil = await db.Languages.AsNoTracking().Where(l => l.IsActive).Select(l => l.Code).ToListAsync(ct);
        var ns = r.Filtreler.Namespace;

        // 1) Süzgeçler ham satırlara → hangi anahtarlar kalıyor (+ anahtar başına özet).
        var taban = UiTranslationGrid.ApplyAll(db.UiTranslations.AsNoTracking(), r.Filtreler, r.Grid);
        var gruplu = taban
            .GroupBy(t => new { t.Namespace, t.Key })
            .Select(g => new
            {
                g.Key.Namespace,
                g.Key.Key,
                DoluDil = g.Count(x => x.Value != ""),
                Son = g.Max(x => x.UpdatedAt ?? x.CreatedAt),
            });

        if (r.Filtreler.EksikOlanlar) gruplu = gruplu.Where(x => x.DoluDil < aktifDil.Count);

        var toplam = await gruplu.CountAsync(ct);   // sayım SAYFALAMADAN ÖNCE

        var desc = string.Equals(r.Grid?.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        gruplu = r.Grid?.Sort switch
        {
            "guncelleme" => desc ? gruplu.OrderByDescending(x => x.Son).ThenBy(x => x.Key)
                                 : gruplu.OrderBy(x => x.Son).ThenBy(x => x.Key),
            _ => desc ? gruplu.OrderByDescending(x => x.Key) : gruplu.OrderBy(x => x.Key),
        };

        var ozet = await gruplu.Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize).ToListAsync(ct);
        if (ozet.Count == 0)
            return Result.Success(new PagedResult<UiTranslationGridRow>([], toplam, r.Page, r.PageSize));

        // 2) Sayfaya düşen anahtarların TÜM dilleri (süzgeç hücre kırpmasın).
        var anahtarlar = ozet.Select(o => o.Key).ToList();
        var hepsi = await db.UiTranslations.AsNoTracking()
            .Where(t => anahtarlar.Contains(t.Key) && (ns == null || t.Namespace == ns))
            .Select(t => new { t.Namespace, t.Key, t.Lang, t.Value })
            .ToListAsync(ct);

        var satirlar = ozet.Select(o =>
        {
            var deger = hepsi
                .Where(h => h.Key == o.Key && h.Namespace == o.Namespace && h.Value != "")
                .ToDictionary(h => h.Lang, h => h.Value);
            return new UiTranslationGridRow(
                o.Namespace, o.Key, deger, deger.Count,
                aktifDil.Any(d => !deger.ContainsKey(d)), o.Son);
        }).ToList();

        return Result.Success(new PagedResult<UiTranslationGridRow>(satirlar, toplam, r.Page, r.PageSize));
    }
}

/// <summary>Excel DÜZ satırdır (anahtar × dil) — çevirmene gönderilen biçim.</summary>
public record UiTranslationExportRow(string Namespace, string Key, string Lang, string Value, DateTime? Guncelleme);

public record ExportUiTranslationsQuery(UiTranslationFiltreleri Filtreler, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<UiTranslationExportRow>>>;

public class ExportUiTranslationsQueryHandler(ICoreDbContext db)
    : IRequestHandler<ExportUiTranslationsQuery, Result<GridExportSource<UiTranslationExportRow>>>
{
    public async Task<Result<GridExportSource<UiTranslationExportRow>>> Handle(ExportUiTranslationsQuery r, CancellationToken ct)
    {
        var q = UiTranslationGrid.ApplyAll(db.UiTranslations.AsNoTracking(), r.Filtreler, r.Grid);

        // "Eksik olanlar" ANAHTAR seviyesinde bir süzgeçtir (liste ekranında gruplamadan sonra uygulanır);
        // düz dışa aktarmada sessizce yok saymak listeyle Excel'i ayrıştırırdı → burada da uygula.
        if (r.Filtreler.EksikOlanlar)
        {
            var aktifDil = await db.Languages.AsNoTracking().Where(l => l.IsActive).CountAsync(ct);
            var eksikAnahtarlar = q.GroupBy(t => t.Key)
                .Where(g => g.Count(x => x.Value != "") < aktifDil)
                .Select(g => g.Key);
            q = q.Where(t => eksikAnahtarlar.Contains(t.Key));
        }

        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<UiTranslationExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = q.OrderBy(t => t.Key).ThenBy(t => t.Lang)
            .Select(t => new UiTranslationExportRow(t.Namespace, t.Key, t.Lang, t.Value, t.UpdatedAt ?? t.CreatedAt));
        return Result.Success(new GridExportSource<UiTranslationExportRow>(count, rows));
    }
}
