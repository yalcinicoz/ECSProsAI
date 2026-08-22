using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ECSPros.Api.Services.Store;
using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking.Feed;

public sealed record FeedRunResult(int ProductCount, int ItemCount, int InStockCount, long XmlBytes, long CsvBytes, string XmlPath, string CsvPath);

/// <summary>
/// İE-5 Faz E (2026-08-22): Google Shopping XML (RSS 2.0 + g:) ve Meta katalog CSV üretimi —
/// tmp dosyaya yazıp atomik rename. Giyim alanları: item_group_id/color/size/gender/age_group;
/// id = varyant SKU (tracking item_id ile aynı); price/sale_price KDV dahil; availability sellable stok;
/// shipping google_merchant ayarından (karar §7-7); link kanal slug'ı (kanonik) veya /urun/{kod}.
/// </summary>
public sealed class FeedGenerator(
    FeedProductReader reader,
    ICatalogDbContext catalogDb,
    ICoreDbContext coreDb,
    ITrackingSettingsProvider trackingSettings)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);

    public async Task<FeedRunResult> GenerateAsync(Guid platformId, string platformCode, string outputDir, CancellationToken ct)
    {
        var s = await trackingSettings.GetAsync(platformId, ct);
        var merchant = s.Servis("google_merchant") ?? throw new InvalidOperationException("google_merchant entegrasyonu aktif değil.");
        var currency = merchant.Get("currency") ?? "TRY";
        var country = (merchant.Get("feedCountry") ?? "TR").ToUpperInvariant();
        var includeOos = merchant.Bool("includeOutOfStock");
        var shippingPrice = decimal.TryParse((merchant.Get("shippingPrice") ?? "").Replace(',', '.'), NumberStyles.Any, Inv, out var sp) && sp >= 0 ? sp : (decimal?)null;
        var shippingService = merchant.Get("shippingService") ?? "Standart Kargo";

        var platform = await coreDb.FirmPlatforms.AsNoTracking().Where(p => p.Id == platformId)
            .Select(p => new { p.Code, p.Settings, p.NameI18n }).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Kanal bulunamadı.");
        var kok = (platform.Settings.TryGetValue("canonicalDomain", out var cd) && cd?.ToString() is { Length: > 0 } cds ? cds : "https://www.misharitalia.com").TrimEnd('/');
        var brandVarsayilan = platform.Settings.TryGetValue("brandName", out var bn) && bn?.ToString() is { Length: > 0 } bns ? bns
            : (platform.NameI18n.TryGetValue("tr", out var pn) ? pn : platformCode);
        var cdn = await CdnHelper.BuildZoomUrlAsync(catalogDb, ct);

        var items = await reader.ReadAsync(platformId, ct);
        Directory.CreateDirectory(outputDir);
        var xmlPath = Path.Combine(outputDir, "google-shopping.xml");
        var csvPath = Path.Combine(outputDir, "meta-catalog.csv");
        var xmlTmp = xmlPath + ".tmp"; var csvTmp = csvPath + ".tmp";

        int itemCount = 0, inStock = 0; var products = new HashSet<Guid>();
        var xs = new XmlWriterSettings { Indent = false, Encoding = new UTF8Encoding(false), Async = true };
        await using (var xw = XmlWriter.Create(xmlTmp, xs))
        await using (var csv = new StreamWriter(csvTmp, false, new UTF8Encoding(true)))
        {
            xw.WriteStartDocument();
            xw.WriteStartElement("rss"); xw.WriteAttributeString("version", "2.0");
            xw.WriteAttributeString("xmlns", "g", null, "http://base.google.com/ns/1.0");
            xw.WriteStartElement("channel");
            xw.WriteElementString("title", brandVarsayilan);
            xw.WriteElementString("link", kok);
            xw.WriteElementString("description", $"{brandVarsayilan} ürün feed'i");
            await csv.WriteLineAsync("id,title,description,availability,condition,price,sale_price,link,image_link,additional_image_link,brand,item_group_id,color,size,gender,age_group,google_product_category,product_type,gtin,mpn");

            foreach (var it in items)
            {
                ct.ThrowIfCancellationRequested();
                var stokta = it.Stock > 0;
                if (!stokta && !includeOos) continue;
                if (it.ImageFiles.Count == 0) continue; // görselsiz ürün Merchant'ta reddedilir

                itemCount++; if (stokta) inStock++; products.Add(it.ProductId);
                var title = Kisalt(Baslik(it), 150);
                var desc = Kisalt(Temizle(it.Description) ?? title, 5000);
                var link = it.Slug is { Length: > 0 } sl ? $"{kok}/{sl.TrimStart('/')}" : $"{kok}/urun/{Uri.EscapeDataString(it.ProductCode)}{(it.ColorValueId is { } cv ? "?color=" + cv : "")}";
                var img = cdn + it.ImageFiles[0];
                var ekGorseller = it.ImageFiles.Skip(1).Take(10).Select(f => cdn + f).ToList();
                var (price, salePrice) = it.CompareAtPrice is { } cap ? (cap, (decimal?)it.Price) : (it.Price, null);
                var gtin = it.Barcode is { } bc && bc.All(char.IsDigit) && bc.Length is 8 or 12 or 13 or 14 ? bc : null;
                var (gender, age) = CinsiyetCevir(it.Gender);
                var brand = it.Brand ?? brandVarsayilan;

                xw.WriteStartElement("item");
                G(xw, "id", it.Sku); G(xw, "title", title); G(xw, "description", desc); G(xw, "link", link);
                G(xw, "image_link", img); foreach (var e in ekGorseller) G(xw, "additional_image_link", e);
                G(xw, "availability", stokta ? "in_stock" : "out_of_stock");
                G(xw, "price", Para(price, currency)); if (salePrice is { } spx) G(xw, "sale_price", Para(spx, currency));
                G(xw, "brand", brand); if (gtin is not null) G(xw, "gtin", gtin); G(xw, "mpn", it.Sku);
                G(xw, "condition", "new"); G(xw, "item_group_id", it.ProductCode);
                if (it.Color is not null) G(xw, "color", it.Color); if (it.Size is not null) G(xw, "size", it.Size);
                if (gender is not null) G(xw, "gender", gender); if (age is not null) G(xw, "age_group", age);
                if (it.GoogleCategory is not null) G(xw, "google_product_category", it.GoogleCategory);
                if (it.CategoryPath is not null) G(xw, "product_type", it.CategoryPath);
                if (shippingPrice is { } shp)
                {
                    xw.WriteStartElement("g", "shipping", null);
                    G(xw, "country", country); G(xw, "service", shippingService); G(xw, "price", Para(shp, currency));
                    xw.WriteEndElement();
                }
                xw.WriteEndElement();

                await csv.WriteLineAsync(string.Join(",", new[]
                {
                    C(it.Sku), C(title), C(desc.Length > 1000 ? desc[..1000] : desc), stokta ? "in stock" : "out of stock", "new",
                    Para(price, currency), salePrice is { } sp2 ? Para(sp2, currency) : "", C(link), C(img),
                    C(string.Join(",", ekGorseller)), C(brand), C(it.ProductCode), C(it.Color ?? ""), C(it.Size ?? ""),
                    gender ?? "", age ?? "", C(it.GoogleCategory ?? ""), C(it.CategoryPath ?? ""), gtin ?? "", C(it.Sku)
                }));
            }
            xw.WriteEndElement(); xw.WriteEndElement(); xw.WriteEndDocument();
            await xw.FlushAsync();
        }
        File.Move(xmlTmp, xmlPath, true);
        File.Move(csvTmp, csvPath, true);
        return new FeedRunResult(products.Count, itemCount, inStock, new FileInfo(xmlPath).Length, new FileInfo(csvPath).Length, xmlPath, csvPath);
    }

    private static void G(XmlWriter xw, string ad, string deger) => xw.WriteElementString("g", ad, null, deger);
    private static string Para(decimal v, string cur) => v.ToString("0.00", Inv) + " " + cur;
    private static string C(string v) => "\"" + (v ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    private static string Kisalt(string v, int n) => v.Length <= n ? v : v[..n];
    private static string? Temizle(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var t = System.Net.WebUtility.HtmlDecode(HtmlTag.Replace(html, " "));
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t.Length == 0 ? null : t;
    }
    private static string Baslik(FeedItem it)
    {
        var parcalar = new List<string> { it.Title };
        if (!string.IsNullOrWhiteSpace(it.Color) && !it.Title.Contains(it.Color, StringComparison.OrdinalIgnoreCase)) parcalar.Add(it.Color);
        if (!string.IsNullOrWhiteSpace(it.Size)) parcalar.Add(it.Size);
        return string.Join(" - ", parcalar);
    }
    /// <summary>cinsiyet değeri (Kadın/Erkek/Unisex/Kız Çocuk/Erkek Çocuk/Bebek…) → Google gender + age_group.</summary>
    private static (string? Gender, string? Age) CinsiyetCevir(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return (null, null);
        var l = v.ToLowerInvariant();
        var age = l.Contains("bebek") ? "infant" : (l.Contains("çocuk") || l.Contains("cocuk") || l.Contains("kız") || l.Contains("kiz") && !l.Contains("kadın")) ? "kids" : "adult";
        string? g = l.Contains("unisex") ? "unisex" : (l.Contains("kadın") || l.Contains("kadin") || l.Contains("kız") || l.Contains("kiz")) ? "female" : (l.Contains("erkek") ? "male" : null);
        return (g, age);
    }
}
