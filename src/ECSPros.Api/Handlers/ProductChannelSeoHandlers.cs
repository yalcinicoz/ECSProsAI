using ECSPros.Catalog.Application.Services;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Handlers;

/// <summary>
/// Kanal bazlı ürün SEO (2026-09-11, kullanıcı): sitede ürün URL'si ürün kaydındaki slug'tan DEĞİL, kanal + renk
/// bazlı <c>channel_variants.Slug</c>'tan üretilir (legacy aktarımı `{ad}-{eskiAnaVaryantId}`); meta başlık/açıklama
/// da kanal başına <c>channel_products</c>'ta tutulur. Bu handler'lar ürün detayının SEO sekmesini besler:
/// kanal başına URL'ler (renk başına tek slug; rengin TÜM bedenlerine yazılır) + meta alanları.
/// Api katmanında: üç modül (Catalog renk ekseni, Storefront slug/meta, Core kanal adı + canonicalDomain).
/// </summary>
public record GetProductChannelSeoQuery(Guid ProductId) : IRequest<Result<List<ProductChannelSeoDto>>>;
public record ProductChannelSeoColorDto(Guid? ColorValueId, string ColorName, string? Slug, List<Guid> VariantIds);
public record ProductChannelSeoDto(Guid FirmPlatformId, string ChannelCode, string ChannelName, string CanonicalDomain,
    Dictionary<string, string>? MetaTitleI18n, Dictionary<string, string>? MetaDescriptionI18n, bool IsActive, List<ProductChannelSeoColorDto> Colors);

public record SaveProductChannelSeoCommand(Guid ProductId, Guid FirmPlatformId,
    Dictionary<string, string>? MetaTitleI18n, Dictionary<string, string>? MetaDescriptionI18n,
    List<ProductChannelSeoColorInput> Colors, Guid UserId) : IRequest<Result<bool>>;
public record ProductChannelSeoColorInput(Guid? ColorValueId, string? Slug);

public static class ProductChannelSeoKurali
{
    /// <summary>Slug: küçük harf, rakam, tire (Türkçe karakterler dönüştürülür).</summary>
    public static string Normalize(string s)
    {
        var map = new Dictionary<char, char> { ['ç'] = 'c', ['ğ'] = 'g', ['ı'] = 'i', ['ö'] = 'o', ['ş'] = 's', ['ü'] = 'u', ['Ç'] = 'c', ['Ğ'] = 'g', ['İ'] = 'i', ['Ö'] = 'o', ['Ş'] = 's', ['Ü'] = 'u' };
        var t = new string(s.Trim().Select(c => map.TryGetValue(c, out var m) ? m : char.ToLowerInvariant(c)).ToArray());
        t = System.Text.RegularExpressions.Regex.Replace(t, "[^a-z0-9]+", "-").Trim('-');
        return t;
    }

    /// <summary>Ürünün varyantlarını renge (grubun birincil ekseni) göre gruplar; eksensiz üründe tek grup (ColorValueId null).</summary>
    public static async Task<List<(Guid? ColorValueId, string ColorName, List<Guid> VariantIds)>> RenkGruplariAsync(ICatalogDbContext catDb, Guid productId, CancellationToken ct)
    {
        var urun = await catDb.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => new { p.Id, p.ProductGroupId }).FirstOrDefaultAsync(ct);
        if (urun is null) return new();
        var varyantlar = await catDb.ProductVariants.AsNoTracking().Where(v => v.ProductId == productId).Select(v => v.Id).ToListAsync(ct);
        var eksen = await catDb.ProductGroupAttributes.AsNoTracking()
            .Where(ga => ga.ProductGroupId == urun.ProductGroupId && ga.IsPrimaryAxis).Select(ga => (Guid?)ga.AttributeTypeId).FirstOrDefaultAsync(ct);
        if (eksen is null || varyantlar.Count == 0)
            return new() { (null, "Ürün", varyantlar) };
        var atamalar = await catDb.ProductVariantAttributes.AsNoTracking()
            .Where(a => varyantlar.Contains(a.VariantId) && a.AttributeTypeId == eksen.Value)
            .Select(a => new { a.VariantId, a.AttributeValueId }).ToListAsync(ct);
        var degerIds = atamalar.Select(a => a.AttributeValueId).Distinct().ToList();
        var adlar = await catDb.AttributeValues.AsNoTracking().Where(v => degerIds.Contains(v.Id))
            .Select(v => new { v.Id, v.NameI18n, v.SortOrder }).ToListAsync(ct);
        var gruplar = atamalar.GroupBy(a => a.AttributeValueId)
            .Select(g => { var ad = adlar.FirstOrDefault(x => x.Id == g.Key); return (ColorValueId: (Guid?)g.Key,
                ColorName: ad?.NameI18n.TryGetValue("tr", out var n) == true ? n : (ad?.NameI18n.Values.FirstOrDefault() ?? g.Key.ToString()[..8]),
                VariantIds: g.Select(x => x.VariantId).ToList(), Sira: ad?.SortOrder ?? 0); })
            .OrderBy(x => x.Sira).ThenBy(x => x.ColorName).Select(x => (x.ColorValueId, x.ColorName, x.VariantIds)).ToList();
        var atanmayan = varyantlar.Except(atamalar.Select(a => a.VariantId)).ToList();
        if (atanmayan.Count > 0) gruplar.Add((null, "Renksiz", atanmayan));
        return gruplar;
    }
}

public class GetProductChannelSeoHandler(ICatalogDbContext catDb, IStorefrontDbContext sfDb, ICoreDbContext coreDb)
    : IRequestHandler<GetProductChannelSeoQuery, Result<List<ProductChannelSeoDto>>>
{
    public async Task<Result<List<ProductChannelSeoDto>>> Handle(GetProductChannelSeoQuery r, CancellationToken ct)
    {
        var renkler = await ProductChannelSeoKurali.RenkGruplariAsync(catDb, r.ProductId, ct);
        if (renkler.Count == 0) return Result.Failure<List<ProductChannelSeoDto>>("Ürün bulunamadı.");
        var tumVaryantlar = renkler.SelectMany(x => x.VariantIds).ToList();
        var kanalUrunleri = await sfDb.ChannelProducts.AsNoTracking().Where(cp => cp.ProductId == r.ProductId)
            .Select(cp => new { cp.FirmPlatformId, cp.MetaTitleI18n, cp.MetaDescriptionI18n, cp.IsActive }).ToListAsync(ct);
        var sluglar = await sfDb.ChannelVariants.AsNoTracking()
            .Where(cv => tumVaryantlar.Contains(cv.VariantId) && cv.Slug != null)
            .Select(cv => new { cv.FirmPlatformId, cv.VariantId, cv.Slug }).ToListAsync(ct);
        var platformIds = kanalUrunleri.Select(k => k.FirmPlatformId).Union(sluglar.Select(s => s.FirmPlatformId)).Distinct().ToList();
        var platformlar = await coreDb.FirmPlatforms.AsNoTracking().Where(fp => platformIds.Contains(fp.Id) && !fp.IsDeleted)
            .Select(fp => new { fp.Id, fp.Code, fp.NameI18n, fp.Settings }).ToListAsync(ct);
        var sonuc = new List<ProductChannelSeoDto>();
        foreach (var fp in platformlar.OrderBy(p => p.Code))
        {
            var kp = kanalUrunleri.FirstOrDefault(k => k.FirmPlatformId == fp.Id);
            var canonical = fp.Settings.TryGetValue("canonicalDomain", out var cd) && cd is not null && !string.IsNullOrWhiteSpace(cd.ToString()) ? cd.ToString()! : "";
            var renkDto = renkler.Select(rg => new ProductChannelSeoColorDto(rg.ColorValueId, rg.ColorName,
                sluglar.Where(s => s.FirmPlatformId == fp.Id && rg.VariantIds.Contains(s.VariantId)).Select(s => s.Slug).FirstOrDefault(),
                rg.VariantIds)).ToList();
            sonuc.Add(new ProductChannelSeoDto(fp.Id, fp.Code, fp.NameI18n.TryGetValue("tr", out var ad) ? ad : fp.Code, canonical,
                kp?.MetaTitleI18n, kp?.MetaDescriptionI18n, kp?.IsActive ?? false, renkDto));
        }
        return Result.Success(sonuc);
    }
}

public class SaveProductChannelSeoHandler(ICatalogDbContext catDb, IStorefrontDbContext sfDb)
    : IRequestHandler<SaveProductChannelSeoCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SaveProductChannelSeoCommand r, CancellationToken ct)
    {
        var renkler = await ProductChannelSeoKurali.RenkGruplariAsync(catDb, r.ProductId, ct);
        if (renkler.Count == 0) return Result.Failure<bool>("Ürün bulunamadı.");
        var tumVaryantlar = renkler.SelectMany(x => x.VariantIds).ToList();

        // Meta: channel_products satırı (yoksa oluştur — kapsam bayrakları dokunulmaz, InScope varsayılanı)
        var cp = await sfDb.ChannelProducts.FirstOrDefaultAsync(x => x.FirmPlatformId == r.FirmPlatformId && x.ProductId == r.ProductId, ct);
        if (cp is null)
        {
            cp = new ECSPros.Storefront.Domain.Entities.ChannelProduct { FirmPlatformId = r.FirmPlatformId, ProductId = r.ProductId, CreatedBy = r.UserId, ScopeSource = "manual" };
            sfDb.ChannelProducts.Add(cp);
        }
        cp.MetaTitleI18n = Temiz(r.MetaTitleI18n); cp.MetaDescriptionI18n = Temiz(r.MetaDescriptionI18n);
        cp.UpdatedAt = DateTime.UtcNow; cp.UpdatedBy = r.UserId;

        // Slug'lar: renk başına; rengin TÜM bedenlerine yazılır; kanal içinde başka ürünle çakışamaz
        var kanalVaryantlari = await sfDb.ChannelVariants.Where(cv => cv.FirmPlatformId == r.FirmPlatformId && tumVaryantlar.Contains(cv.VariantId)).ToListAsync(ct);
        var gorulen = new HashSet<string>();
        foreach (var girdi in r.Colors)
        {
            var grup = renkler.FirstOrDefault(x => x.ColorValueId == girdi.ColorValueId);
            if (grup.VariantIds is null) continue;
            var slug = string.IsNullOrWhiteSpace(girdi.Slug) ? null : ProductChannelSeoKurali.Normalize(girdi.Slug);
            if (slug is { Length: < 3 }) return Result.Failure<bool>($"'{grup.ColorName}' için URL en az 3 karakter olmalıdır.");
            if (slug is not null)
            {
                if (!gorulen.Add(slug)) return Result.Failure<bool>($"'{slug}' aynı üründe iki renge verilemez.");
                var cakisma = await sfDb.ChannelVariants.AsNoTracking()
                    .AnyAsync(cv => cv.FirmPlatformId == r.FirmPlatformId && cv.Slug == slug && !tumVaryantlar.Contains(cv.VariantId), ct);
                if (cakisma) return Result.Failure<bool>($"'{slug}' adresi bu kanalda başka bir üründe kullanılıyor.");
            }
            foreach (var vid in grup.VariantIds)
            {
                var cv = kanalVaryantlari.FirstOrDefault(x => x.VariantId == vid);
                if (cv is null)
                {
                    if (slug is null) continue;
                    cv = new ECSPros.Storefront.Domain.Entities.ChannelVariant { FirmPlatformId = r.FirmPlatformId, VariantId = vid, IsActive = true, CreatedBy = r.UserId };
                    sfDb.ChannelVariants.Add(cv); kanalVaryantlari.Add(cv);
                }
                if (cv.Slug != slug) { cv.Slug = slug; cv.UpdatedAt = DateTime.UtcNow; cv.UpdatedBy = r.UserId; }
            }
        }
        await sfDb.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    private static Dictionary<string, string>? Temiz(Dictionary<string, string>? m)
    {
        var t = m?.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.Trim());
        return t is { Count: > 0 } ? t : null;
    }
}
