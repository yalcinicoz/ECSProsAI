using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetGeoLookups;

// C4 (K6): adres hiyerarşisi kademeli lookup'ları — teslimat adres formu, profil şehri ve
// kişiselleştirme şehir seçicisi (G9) aynı kaynaktan beslenir. Mahalle listesi büyük
// (73K+) olduğundan yalnız districtId + arama ile sayfalı döner (aramalı select bileşeni).

public record GeoCountryDto(Guid Id, string Code, Dictionary<string, string> NameI18n, string PhoneCode);
public record GeoCityDto(Guid Id, string Code, Dictionary<string, string> NameI18n, string? Region);
public record GeoDistrictDto(Guid Id, Dictionary<string, string> NameI18n);
public record GeoNeighborhoodDto(Guid Id, Dictionary<string, string> NameI18n, string? PostalCode);

public record GetGeoCountriesQuery() : IRequest<Result<List<GeoCountryDto>>>;
public record GetGeoCitiesQuery(Guid CountryId) : IRequest<Result<List<GeoCityDto>>>;
public record GetGeoDistrictsQuery(Guid CityId) : IRequest<Result<List<GeoDistrictDto>>>;
public record GetGeoNeighborhoodsQuery(Guid DistrictId, string? Search = null, int Limit = 50)
    : IRequest<Result<List<GeoNeighborhoodDto>>>;

public class GetGeoCountriesQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetGeoCountriesQuery, Result<List<GeoCountryDto>>>
{
    public async Task<Result<List<GeoCountryDto>>> Handle(GetGeoCountriesQuery request, CancellationToken ct) =>
        Result.Success(await db.Countries.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new GeoCountryDto(c.Id, c.Code, c.NameI18n, c.PhoneCode))
            .ToListAsync(ct));
}

public class GetGeoCitiesQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetGeoCitiesQuery, Result<List<GeoCityDto>>>
{
    public async Task<Result<List<GeoCityDto>>> Handle(GetGeoCitiesQuery request, CancellationToken ct) =>
        Result.Success(await db.Cities.AsNoTracking()
            .Where(c => c.CountryId == request.CountryId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new GeoCityDto(c.Id, c.Code, c.NameI18n, c.Region))
            .ToListAsync(ct));
}

public class GetGeoDistrictsQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetGeoDistrictsQuery, Result<List<GeoDistrictDto>>>
{
    public async Task<Result<List<GeoDistrictDto>>> Handle(GetGeoDistrictsQuery request, CancellationToken ct)
    {
        var satirlar = await db.Districts.AsNoTracking()
            .Where(d => d.CityId == request.CityId && d.IsActive)
            .Select(d => new GeoDistrictDto(d.Id, d.NameI18n))
            .ToListAsync(ct);

        // Türkçe sıralama bellek tarafında (SortOrder veri setinde yok; ~40 satır/il)
        var tr = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
        return Result.Success(satirlar.OrderBy(d => d.NameI18n.GetValueOrDefault("tr", ""), tr).ToList());
    }
}

public class GetGeoNeighborhoodsQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetGeoNeighborhoodsQuery, Result<List<GeoNeighborhoodDto>>>
{
    public async Task<Result<List<GeoNeighborhoodDto>>> Handle(GetGeoNeighborhoodsQuery request, CancellationToken ct)
    {
        // Arama/sıralama bellek tarafında: NameI18n indexer'ı dinamik JSON'da SQL'e
        // çevrilemiyor (B2 dersi) ve ilçe başına mahalle sayısı küçük (≤ ~1.5K).
        var satirlar = await db.Neighborhoods.AsNoTracking()
            .Where(n => n.DistrictId == request.DistrictId && n.IsActive)
            .Select(n => new GeoNeighborhoodDto(n.Id, n.NameI18n, n.PostalCode))
            .ToListAsync(ct);

        var tr = new System.Globalization.CultureInfo("tr-TR");
        var karsilastir = StringComparer.Create(tr, true);
        IEnumerable<GeoNeighborhoodDto> sonuc = satirlar;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var arama = request.Search.Trim().ToLower(tr);
            sonuc = sonuc.Where(n => n.NameI18n.GetValueOrDefault("tr", "").ToLower(tr).Contains(arama));
        }

        var limit = Math.Clamp(request.Limit, 1, 200);
        return Result.Success(sonuc
            .OrderBy(n => n.NameI18n.GetValueOrDefault("tr", ""), karsilastir)
            .Take(limit)
            .ToList());
    }
}

// P1b: id → görünen ad çözümü — admin sipariş detayı adres bölümü (ülke/il/ilçe/mahalle
// tek istekle). Genel amaçlı: P4 üye adresleri de kullanır.
public record GetGeoNamesQuery(List<Guid> Ids) : IRequest<Result<Dictionary<Guid, string>>>;

public class GetGeoNamesQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetGeoNamesQuery, Result<Dictionary<Guid, string>>>
{
    public async Task<Result<Dictionary<Guid, string>>> Handle(GetGeoNamesQuery request, CancellationToken ct)
    {
        var sonuc = new Dictionary<Guid, string>();
        if (request.Ids.Count == 0) return Result.Success(sonuc);

        var ids = request.Ids.Distinct().Take(50).ToList();

        foreach (var s in await db.Countries.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.NameI18n }).ToListAsync(ct))
            sonuc[s.Id] = s.NameI18n.GetValueOrDefault("tr", "");

        foreach (var s in await db.Cities.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.NameI18n }).ToListAsync(ct))
            sonuc[s.Id] = s.NameI18n.GetValueOrDefault("tr", "");

        foreach (var s in await db.Districts.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.NameI18n }).ToListAsync(ct))
            sonuc[s.Id] = s.NameI18n.GetValueOrDefault("tr", "");

        foreach (var s in await db.Neighborhoods.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.NameI18n }).ToListAsync(ct))
            sonuc[s.Id] = s.NameI18n.GetValueOrDefault("tr", "");

        return Result.Success(sonuc);
    }
}
