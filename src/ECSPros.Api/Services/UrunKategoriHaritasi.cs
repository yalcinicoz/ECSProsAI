using ECSPros.Storefront.Application.Queries.GetProductsLeafChannelCategories;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services;

/// <summary>Platformun ürün → YAPRAK kanal kategorisi haritası (2026-08-15): liste
/// sayfalarındaki "Kategori" filtresi menü kökleri yerine sayfadaki ürünlerin gerçek
/// kategorilerinden üretilir. Kural değerlendirmesi ürün detay breadcrumb'ıyla aynı
/// (GetProductsLeafChannelCategoriesQuery). Platform başına 15 dk süreç-içi cache;
/// süresi dolunca eski harita servis edilirken arka planda tazelenir; ilk istek
/// senkron hesaplar (~27K ürün, birkaç sn).</summary>
public sealed class UrunKategoriHaritasi(IMemoryCache cache, IServiceScopeFactory scopeFactory, ILogger<UrunKategoriHaritasi> logger)
{
    public sealed record KategoriBilgi(Guid Id, string Ad, string Slug);
    public sealed record Harita(
        IReadOnlyDictionary<Guid, Guid> UrunKategori,
        IReadOnlyDictionary<Guid, KategoriBilgi> Kategoriler,
        DateTime HesaplandiUtc)
    {
        public HashSet<Guid> KategoriIdKumesi { get; } = new(Kategoriler.Keys);

        /// <summary>attrs= içindeki id'leri kategori / özellik değeri diye ayırır (kategori id'leri
        /// aynı parametrede taşınır — mevcut filtre UI'ı ve api/store sözleşmesi değişmeden).</summary>
        public (List<Guid> Kategoriler, List<Guid>? Ozellikler) Ayir(IReadOnlyList<Guid>? idler)
        {
            if (idler is null || idler.Count == 0) return ([], null);
            var kat = idler.Where(KategoriIdKumesi.Contains).Distinct().ToList();
            var oz = idler.Where(id => !KategoriIdKumesi.Contains(id)).Distinct().ToList();
            return (kat, oz.Count > 0 ? oz : null);
        }

        /// <summary>Seçili yaprak kategorilerdeki ürün id'leri (liste kısıtı için); seçim yoksa null.</summary>
        public List<Guid>? UrunIdleri(IReadOnlyCollection<Guid> kategoriIdler)
        {
            if (kategoriIdler.Count == 0) return null;
            var kume = kategoriIdler.ToHashSet();
            var liste = UrunKategori.Where(kv => kume.Contains(kv.Value)).Select(kv => kv.Key).ToList();
            return liste.Count > 0 ? liste : [Guid.Empty]; // hiç ürün yok → boş sonuç (kısıtsız değil)
        }
    }

    private static readonly TimeSpan TazelikSuresi = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CacheOmru = TimeSpan.FromHours(6); // stale-while-revalidate tavanı
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private static string Anahtar(Guid platformId) => $"store:urun-kategori-haritasi:{platformId}";
    private static string CalisiyorAnahtar(Guid platformId) => Anahtar(platformId) + ":calisiyor";

    public async Task<Harita?> GetAsync(Guid platformId, CancellationToken ct)
    {
        if (cache.TryGetValue(Anahtar(platformId), out Harita? mevcut) && mevcut is not null)
        {
            if (DateTime.UtcNow - mevcut.HesaplandiUtc > TazelikSuresi)
                ArkaPlandaTazele(platformId);
            return mevcut;
        }

        // İlk hesap senkron — aynı anda gelen istekler tek hesabı bekler
        await _kilit.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(Anahtar(platformId), out mevcut) && mevcut is not null)
                return mevcut;
            var harita = await HesaplaAsync(platformId, ct);
            if (harita is not null)
                cache.Set(Anahtar(platformId), harita, CacheOmru);
            return harita;
        }
        finally { _kilit.Release(); }
    }

    private void ArkaPlandaTazele(Guid platformId)
    {
        if (cache.TryGetValue(CalisiyorAnahtar(platformId), out bool _)) return;
        cache.Set(CalisiyorAnahtar(platformId), true, TimeSpan.FromMinutes(2));
        _ = Task.Run(async () =>
        {
            try
            {
                var harita = await HesaplaAsync(platformId, CancellationToken.None);
                if (harita is not null)
                    cache.Set(Anahtar(platformId), harita, CacheOmru);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Ürün-kategori haritası tazelenemedi (platform {P}).", platformId); }
            finally { cache.Remove(CalisiyorAnahtar(platformId)); }
        });
    }

    private async Task<Harita?> HesaplaAsync(Guid platformId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var sonuc = await mediator.Send(new GetProductsLeafChannelCategoriesQuery(platformId, null), ct);
        if (sonuc.IsFailure) return null;
        var urunKategori = new Dictionary<Guid, Guid>();
        var kategoriler = new Dictionary<Guid, KategoriBilgi>();
        foreach (var y in sonuc.Value!)
        {
            urunKategori[y.ProductId] = y.CategoryId;
            if (!kategoriler.ContainsKey(y.CategoryId))
                kategoriler[y.CategoryId] = new KategoriBilgi(y.CategoryId,
                    y.NameI18n.GetValueOrDefault("tr") ?? y.NameI18n.Values.FirstOrDefault() ?? y.Slug, y.Slug);
        }
        logger.LogInformation("Ürün-kategori haritası: {U} ürün, {K} yaprak kategori (platform {P}).",
            urunKategori.Count, kategoriler.Count, platformId);
        return new Harita(urunKategori, kategoriler, DateTime.UtcNow);
    }
}
