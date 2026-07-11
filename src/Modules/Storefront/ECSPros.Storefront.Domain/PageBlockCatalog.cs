namespace ECSPros.Storefront.Domain;

/// <summary>
/// G2: vitrin blok paletinin tek doğruluk kaynağı (spec: anasayfa-dizayn-yönetimi.txt +
/// misharix GorunumTipleri partial'ları). Admin CRUD doğrulaması, Yayınla validasyonu
/// ve Razor render eşlemesi bu katalogdan beslenir — tip/şablon listesi başka yerde
/// tekrarlanmaz. Kural seviyeleri spec'in blok-tipi tablosuna birebir uyar; banner
/// öğeleri bilinçli olarak kuralsızdır (öğe gizlenirse grid şablonu kırılır).
/// </summary>
public static class PageBlockCatalog
{
    /// <summary>Spec'teki kural seviyesi: kural bloğa mı, öğelere mi, ikisine mi verilebilir.</summary>
    public enum RuleLevel { Block, Item, BlockAndItem }

    public sealed record BlockTypeDef(
        string Code,
        string DisplayName,
        RuleLevel Rules,
        bool SupportsItems,
        /// <summary>Boş liste = şablon seçilmez (tek genel şablon).</summary>
        IReadOnlyList<string> Templates,
        /// <summary>Ürün kaynağı/filtre konfigürasyonu (G3) zorunlu mu (carousel/infinity) — tabs'ta öğe config'inde.</summary>
        bool RequiresProductSource,
        /// <summary>Koleksiyon kaynağı konfigürasyonu zorunlu mu (yalnız onaylı+herkese açık koleksiyonlar, E6).</summary>
        bool RequiresCollectionSource);

    public sealed record PlacementDef(string Code, string DisplayName);

    /// <summary>Yerleşimler (2026-07-07 kararı). Kod, PageBlock.Placement ve store API route'unda kullanılır.</summary>
    public static readonly IReadOnlyList<PlacementDef> Placements =
    [
        new("homepage", "Ana Sayfa"),
        new("global-top", "Global Üst Alan (Duyuru)"),
        new("list-top", "Ürün Listesi Üst"),
        new("list-bottom", "Ürün Listesi Alt"),
        new("detail-bottom", "Ürün Detay Alt"),
        new("cart", "Sepet"),
        new("checkout-delivery", "Teslimat"),
        new("checkout-payment", "Ödeme"),
    ];

    /// <summary>Banner grid şablonları (GorunumTipleri/_Banner sekmeleri); reklam = banner+ürün vitrini kompoziti.</summary>
    public static readonly IReadOnlyList<string> BannerTemplates =
        ["tekli", "ikili", "uclu", "dortlu", "besli", "reklam"];

    /// <summary>Carousel varyantları (_Carousel'deki üç demo).</summary>
    public static readonly IReadOnlyList<string> CarouselTemplates =
        ["standart", "ozel-fiyat", "flash"];

    /// <summary>Standart carousel arka plan temaları (config.tema — yalnız standart şablonda anlamlı).</summary>
    public static readonly IReadOnlyList<string> CarouselThemes =
    [
        "varsayilan", "sicak", "mavi", "yesil", "lila", "turuncu-kirmizi", "mavi-mor",
        "yesil-turkuaz", "pembe-sari", "lacivert-mavi", "gun-batimi", "deniz-uclu",
        "orman-uclu", "seker-uclu", "premium-uclu", "flash-urunler",
    ];

    public static readonly IReadOnlyList<BlockTypeDef> BlockTypes =
    [
        new("banner", "Banner", RuleLevel.Block, SupportsItems: true, BannerTemplates,
            RequiresProductSource: false, RequiresCollectionSource: false),
        new("slider", "Slider", RuleLevel.Item, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false),
        new("story", "Story", RuleLevel.Item, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false),
        new("carousel", "Carousel Ürün Listesi", RuleLevel.Block, SupportsItems: false, CarouselTemplates,
            RequiresProductSource: true, RequiresCollectionSource: false),
        new("infinity", "Infinity Ürün Listesi", RuleLevel.Block, SupportsItems: false, [],
            RequiresProductSource: true, RequiresCollectionSource: false),
        new("tabs", "Tabs", RuleLevel.BlockAndItem, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false), // ürün kaynağı tab öğesinin config'inde
        new("collection", "Koleksiyonlar", RuleLevel.Block, SupportsItems: false, [],
            RequiresProductSource: false, RequiresCollectionSource: true),
        new("categories", "Kategoriler", RuleLevel.Block, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false),
        new("brands", "Markalar", RuleLevel.Block, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false),
        new("instagram", "Instagram", RuleLevel.Block, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false),
        new("announcement", "Duyuru Şeridi", RuleLevel.Item, SupportsItems: true, [],
            RequiresProductSource: false, RequiresCollectionSource: false),
    ];

    public static BlockTypeDef? Find(string blockType) =>
        BlockTypes.FirstOrDefault(t => t.Code == blockType);

    public static bool IsValidPlacement(string placement) =>
        Placements.Any(p => p.Code == placement);

    /// <summary>Şablon doğrulaması: şablonlu tiplerde listeden biri zorunlu, şablonsuz tiplerde boş olmalı.</summary>
    public static bool IsValidTemplate(string blockType, string? template)
    {
        var def = Find(blockType);
        if (def is null) return false;
        if (def.Templates.Count == 0) return string.IsNullOrEmpty(template);
        return template is not null && def.Templates.Contains(template);
    }

    /// <summary>Öğe kuralı bu tipte anlamlı mı (spec: banner öğesine kural verilemez).</summary>
    public static bool AllowsItemRules(string blockType) =>
        Find(blockType)?.Rules is RuleLevel.Item or RuleLevel.BlockAndItem;

    /// <summary>Blok kuralı bu tipte anlamlı mı (slider/story/duyuru yalnız öğe kuralı taşır).</summary>
    public static bool AllowsBlockRules(string blockType) =>
        Find(blockType)?.Rules is RuleLevel.Block or RuleLevel.BlockAndItem;

    /// <summary>G10 kural şemasının alanları — segment boyutlarıyla birebir (G9).</summary>
    public static readonly IReadOnlyList<string> RuleFields =
        ["city", "region", "gender", "device", "membership", "memberGroup"];

    /// <summary>
    /// G10: RuleJson yapısal doğrulaması (admin kaydet + Yayınla aynı kaynaktan — iki
    /// katmanlı). Şema: {"city":["06"],"device":["mobile"],...} — her alan string dizisi;
    /// alan içi OR, alanlar arası AND; boş/eksik alan değerlendirilmez. Şehir plaka
    /// kodudur (DB'ye Domain'den bakılamaz — 2 haneli rakam yapısal kontrolü yeterli;
    /// runtime'da bilinmeyen plaka zaten hiçbir ziyaretçiyle eşleşmez).
    /// </summary>
    public static List<string> ValidateRule(string? ruleJson)
    {
        var hatalar = new List<string>();
        if (string.IsNullOrWhiteSpace(ruleJson)) return hatalar;

        System.Text.Json.JsonDocument belge;
        try { belge = System.Text.Json.JsonDocument.Parse(ruleJson); }
        catch { hatalar.Add("kural JSON değil."); return hatalar; }

        using (belge)
        {
            if (belge.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            { hatalar.Add("kural bir JSON nesnesi olmalı."); return hatalar; }

            foreach (var alan in belge.RootElement.EnumerateObject())
            {
                if (!RuleFields.Contains(alan.Name))
                { hatalar.Add($"bilinmeyen kural alanı '{alan.Name}'."); continue; }
                if (alan.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
                { hatalar.Add($"kural alanı '{alan.Name}' dizi olmalı."); continue; }

                foreach (var eleman in alan.Value.EnumerateArray())
                {
                    var deger = eleman.ValueKind == System.Text.Json.JsonValueKind.String
                        ? eleman.GetString() : null;
                    if (string.IsNullOrWhiteSpace(deger))
                    { hatalar.Add($"kural alanı '{alan.Name}' boş olmayan string'ler içermeli."); break; }

                    var gecerli = alan.Name switch
                    {
                        "city" => deger.Length == 2 && deger.All(char.IsAsciiDigit),
                        "region" => deger.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'),
                        "gender" => deger is "male" or "female",
                        "device" => deger is "mobile" or "tablet" or "desktop",
                        "membership" => deger is "member" or "guest",
                        "memberGroup" => Guid.TryParse(deger, out _),
                        _ => true,
                    };
                    if (!gecerli)
                        hatalar.Add($"kural alanı '{alan.Name}' geçersiz değer içeriyor: '{deger}'.");
                }
            }
        }
        return hatalar;
    }
}
