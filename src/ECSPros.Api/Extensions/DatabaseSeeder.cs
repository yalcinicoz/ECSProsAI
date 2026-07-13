using ECSPros.Catalog.Domain.Entities;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Iam.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Extensions;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        await SeedIamAsync(scope.ServiceProvider);
        await SeedCoreAsync(scope.ServiceProvider);
        await SeedCatalogAsync(scope.ServiceProvider);
        await SeedPlatformTypesAsync(services);
        await SeedStorefrontDefaultsAsync(scope.ServiceProvider);
        await SeedCrmDefaultsAsync(scope.ServiceProvider);
        await SeedCmsLegalPagesAsync(scope.ServiceProvider);
        await SeedReturnReasonsAsync(scope.ServiceProvider);
        await SeedCorporatePagesAsync(scope.ServiceProvider);
        await SeedFaqPageAsync(scope.ServiceProvider);
        await SeedDefaultVitrinAsync(scope.ServiceProvider);
        await SeedCargoCarriersAsync(scope.ServiceProvider);
        await SeedPlatformServiceCatalogAsync(scope.ServiceProvider);
    }

    /// <summary>
    /// Platform servisleri kataloğu — SMTP (email) + görsel arama (visual_search)
    /// IntegrationService satırları. Kimlik bilgileri buraya DEĞİL, admin firma detayından
    /// açılan FirmPlatformIntegration kaydına girilir (Credentials şifreli); SettingsSchema
    /// admin formunun alanlarını tanımlar (secret=true → Credentials'a, değilse Settings'e).
    /// Kod bazlı idempotent.
    /// </summary>
    private static async Task SeedPlatformServiceCatalogAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        var servisler = new (string Kod, string Ad, string Tip, Dictionary<string, object> Sema)[]
        {
            ("smtp", "SMTP E-Posta", "email", new Dictionary<string, object>
            {
                ["fields"] = new object[]
                {
                    new { code = "host",     label = "Sunucu",       type = "string", secret = false, required = true },
                    new { code = "port",     label = "Port",         type = "number", secret = false, required = false, @default = 587 },
                    new { code = "user",     label = "Kullanıcı",    type = "string", secret = true,  required = false },
                    new { code = "password", label = "Şifre",        type = "string", secret = true,  required = false },
                    new { code = "from",     label = "Gönderen",     type = "string", secret = false, required = false },
                    new { code = "fromName", label = "Gönderen Adı", type = "string", secret = false, required = false },
                    new { code = "useSsl",   label = "SSL",          type = "boolean", secret = false, required = false, @default = true }
                }
            }),
            ("visual_search", "Görsel Arama", "visual_search", new Dictionary<string, object>
            {
                ["fields"] = new object[]
                {
                    new { code = "apiUrl", label = "API Adresi",  type = "string", secret = false, required = true },
                    new { code = "apiKey", label = "API Anahtarı", type = "string", secret = true,  required = true }
                }
            })
        };

        var mevcutKodlar = await context.IntegrationServices
            .Where(s => s.ServiceType == "email" || s.ServiceType == "visual_search")
            .Select(s => s.Code)
            .ToListAsync();

        var yeniler = servisler
            .Where(s => !mevcutKodlar.Contains(s.Kod))
            .Select(s => new IntegrationService
            {
                Code = s.Kod,
                NameI18n = new() { { "tr", s.Ad }, { "en", s.Ad } },
                ServiceType = s.Tip,
                IsAvailable = true,
                SettingsSchema = s.Sema
            })
            .ToList();

        if (yeniler.Count == 0) return;

        context.IntegrationServices.AddRange(yeniler);
        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: {yeniler.Count} platform servisi eklendi (smtp/visual_search IntegrationService).");
    }

    /// <summary>
    /// H2: Kargo firması kataloğu — IntegrationService (ServiceType=cargo) satırları
    /// takip URL şablonlarıyla. Firma hangi kargolarla çalışacaksa admin firma-sözleşme
    /// ekranından FirmPlatformIntegration açar; storefront kargo modalı adı/logoyu/takip linkini
    /// buradan çözer. Kod bazlı idempotent (var olana dokunmaz — admin düzenlemesi ezilmez).
    /// </summary>
    private static async Task SeedCargoCarriersAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        var firmalar = new (string Kod, string Ad, string SablonUrl)[]
        {
            ("aras", "Aras Kargo", "https://kargotakip.araskargo.com.tr/mainpage.aspx?code={trackingNumber}"),
            ("yurtici", "Yurtiçi Kargo", "https://www.yurticikargo.com/tr/online-servisler/gonderi-sorgula?code={trackingNumber}"),
            ("mng", "MNG Kargo", "https://kargotakip.mngkargo.com.tr/?takipNo={trackingNumber}"),
            ("ptt", "PTT Kargo", "https://gonderitakip.ptt.gov.tr/Track/Verify?q={trackingNumber}"),
            ("surat", "Sürat Kargo", "https://www.suratkargo.com.tr/KargoTakip/?kargotakipno={trackingNumber}"),
            ("hepsijet", "HepsiJet", "https://www.hepsijet.com/gonderi-takibi/{trackingNumber}"),
            ("kolaygelsin", "Kolay Gelsin", "https://esube.kolaygelsin.com/shipments?trackingId={trackingNumber}"),
            ("ups", "UPS", "https://www.ups.com/track?loc=tr_TR&tracknum={trackingNumber}")
        };

        var mevcutKodlar = await context.IntegrationServices
            .Where(s => s.ServiceType == "cargo")
            .Select(s => s.Code)
            .ToListAsync();

        var yeniler = firmalar
            .Where(f => !mevcutKodlar.Contains(f.Kod))
            .Select(f => new IntegrationService
            {
                Code = f.Kod,
                NameI18n = new() { { "tr", f.Ad }, { "en", f.Ad } },
                ServiceType = "cargo",
                IsAvailable = true,
                TrackingUrlTemplate = f.SablonUrl
                // LogoUrl bilinçli null — logo görselleri edinildikçe admin/SQL ile dolar,
                // storefront logo yoksa yalnız adı basar.
            })
            .ToList();

        if (yeniler.Count == 0) return;

        context.IntegrationServices.AddRange(yeniler);
        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: {yeniler.Count} kargo firması eklendi (cargo IntegrationService).");
    }

    /// <summary>
    /// F2: SSS sayfası — "kurumsal-sss" (PageType corporate) + "faq" section'ı +
    /// soru/cevap item'ları (TitleI18n=soru, DescriptionI18n=cevap; tasarımın 9 sorusu).
    /// Admin CMS'ten soru ekler/düzenler; Kurumsal SSS akordiyonu buradan render olur.
    /// Kod bazlı idempotent.
    /// </summary>
    private static async Task SeedFaqPageAsync(IServiceProvider sp)
    {
        var cms = sp.GetRequiredService<ECSPros.Cms.Infrastructure.Persistence.CmsDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();

        var sectionType = await cms.SectionTypes.FirstOrDefaultAsync(t => t.Code == "faq");
        if (sectionType is null)
        {
            sectionType = new ECSPros.Cms.Domain.Entities.SectionType
            {
                Code = "faq",
                NameI18n = new() { ["tr"] = "Soru / Cevap Listesi" },
                SettingsSchema = new(),
                SupportsItems = true
            };
            cms.SectionTypes.Add(sectionType);
        }

        var template = await cms.PageTemplates.FirstAsync(t => t.Code == "icerik-sayfasi");
        var platformIdler = await core.FirmPlatforms
            .Where(fp => fp.IsActive).Select(fp => fp.Id).ToListAsync();

        var sorular = new (string Soru, string Cevap)[]
        {
            ("Üyelik", "Üyelik ücretsizdir. Ana sayfadaki \"ÜYE OL\" seçeneğine tıklayarak kayıt olabilirsiniz. Üyeliğinizi iptal etmek veya şifrenizi değiştirmek için Müşteri Hizmetleri ile iletişime geçebilirsiniz."),
            ("İptal ve Değişim", "Ürün iadeleri için İadelerim sayfasından iade talebi oluşturabilirsiniz. Değişim yapılmamaktadır."),
            ("Kargo ve Teslimat", "Siparişiniz anlaşmalı kargo firmalarımız aracılığıyla teslim edilir. Siparişinizin durumunu \"Hesabım\" altından takip edebilirsiniz."),
            ("Sipariş", "Sipariş durumunuzu, değişiklik veya iptal işlemlerinizi \"Hesabım\" üzerinden yönetebilirsiniz."),
            ("Ödeme", "Banka veya kredi kartı ile ödeme seçeneklerimiz mevcuttur. Taksit seçenekleri için kredi kartınızı kullanabilirsiniz."),
            ("İndirim Kuponları ve Kodları", "Sepet özetinin altında yer alan alana indirim kodunuzu girebilirsiniz. Bazı butiklerimiz indirimlere kapalıdır."),
            ("Fatura", "Faturalar sipariş sırasında belirttiğiniz adrese gönderilir. Şirket adına fatura düzenlenmemektedir."),
            ("Hesabım", "Şifre ve e-posta adresi güncellemelerinizi \"Hesabım\" üzerinden yapabilirsiniz."),
            ("Şikayet ve Öneriler", "Şikayetlerinizi mesai saatlerinde çağrı merkezimize iletebilirsiniz.")
        };

        var eklenen = 0;
        foreach (var platformId in platformIdler)
        {
            if (await cms.Pages.AnyAsync(p => p.FirmPlatformId == platformId
                                              && p.PageType == "corporate" && p.Code == "kurumsal-sss"))
                continue;

            var sayfa = new ECSPros.Cms.Domain.Entities.Page
            {
                FirmPlatformId = platformId,
                TemplateId = template.Id,
                Code = "kurumsal-sss",
                NameI18n = new() { ["tr"] = "Sık Sorulan Sorular" },
                SlugI18n = new() { ["tr"] = "kurumsal-sss" },
                PageType = "corporate"
            };
            cms.Pages.Add(sayfa);

            var section = new ECSPros.Cms.Domain.Entities.PageSection
            {
                PageId = sayfa.Id,
                SectionTypeId = sectionType.Id,
                Name = "SSS",
                Settings = new(),
                SortOrder = 0
            };
            cms.PageSections.Add(section);

            var sira = 0;
            foreach (var (soru, cevap) in sorular)
                cms.PageSectionItems.Add(new ECSPros.Cms.Domain.Entities.PageSectionItem
                {
                    SectionId = section.Id,
                    ItemType = "faq",
                    TitleI18n = new() { ["tr"] = soru },
                    DescriptionI18n = new() { ["tr"] = cevap },
                    SortOrder = sira++
                });
            eklenen++;
        }

        if (eklenen > 0)
        {
            await cms.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {eklenen} platforma SSS sayfası oluşturuldu.");
        }
    }

    /// <summary>
    /// F1: kurumsal içerik sayfaları — PageType "corporate", kod "kurumsal-*" önekli
    /// (legal sayfalarla kod çakışmasın). İçerik tasarım partial'larının panel HTML'i;
    /// admin'den düzenlenebilir, Kurumsal sayfaları CMS'ten basar (boşsa tasarım demo
    /// yedeği — D3 deseni). SSS (F2) ve İletişim (F3) ayrı yapıda. Kod bazlı idempotent;
    /// canlıya SQL ile eklendi.
    /// </summary>
    private static async Task SeedCorporatePagesAsync(IServiceProvider sp)
    {
        var cms = sp.GetRequiredService<ECSPros.Cms.Infrastructure.Persistence.CmsDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();

        var sectionType = await cms.SectionTypes.FirstAsync(t => t.Code == "rich_text");
        var template = await cms.PageTemplates.FirstAsync(t => t.Code == "icerik-sayfasi");

        var platformIdler = await core.FirmPlatforms
            .Where(fp => fp.IsActive).Select(fp => fp.Id).ToListAsync();

        var eklenen = 0;
        foreach (var platformId in platformIdler)
        {
            var mevcutKodlar = await cms.Pages
                .Where(s => s.FirmPlatformId == platformId && s.PageType == "corporate")
                .Select(s => s.Code).ToListAsync();

            foreach (var (kod, baslik, html) in KurumsalSayfaIcerikleri())
            {
                if (mevcutKodlar.Contains(kod)) continue;
                var sayfa = new ECSPros.Cms.Domain.Entities.Page
                {
                    FirmPlatformId = platformId,
                    TemplateId = template.Id,
                    Code = kod,
                    NameI18n = new() { ["tr"] = baslik },
                    SlugI18n = new() { ["tr"] = kod },
                    PageType = "corporate"
                };
                cms.Pages.Add(sayfa);
                cms.PageSections.Add(new ECSPros.Cms.Domain.Entities.PageSection
                {
                    PageId = sayfa.Id,
                    SectionTypeId = sectionType.Id,
                    Name = baslik,
                    Settings = new() { ["html"] = html },
                    SortOrder = 0
                });
                eklenen++;
            }
        }

        if (eklenen > 0)
        {
            await cms.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {eklenen} kurumsal CMS sayfası oluşturuldu.");
        }
    }

    /// <summary>F1: kurumsal sayfa içerikleri — tasarım partial'larının panel iç HTML'i
    /// (section kökü partial'da kalır; buradaki içerik admin'den düzenlenir).</summary>
    private static IEnumerable<(string Kod, string Baslik, string Html)> KurumsalSayfaIcerikleri()
    {
        yield return ("kurumsal-hakkimizda", "Hakkımızda", """
<header class="ms-kurumsal-hero">
    <span>Premium Moda</span>
    <h2>Mishar Italia: Premium Modanın Yeni Tanımı</h2>
    <p>Mishar Italia, kadın, erkek ve çocuk giyiminde şıklığı ve üst segment kalite standartlarını bir araya getiren premium bir moda markasıdır.</p>
</header>
<div class="ms-kurumsal-metin">
    <p>Her yaşa ve her tarza hitap eden seçkin koleksiyonlarımızla, modern gardıropların vazgeçilmez markası olma vizyonuyla hareket ediyoruz.</p>
    <p>Markamızın şık duruşu ve sunduğu premium deneyim, arkasındaki güçlü üretim altyapısıyla desteklenmektedir. Mishar Italia bünyesindeki tüm koleksiyonların üretimi, sektörün öncü ve köklü kuruluşu GÜLSELİ TEKSTİL tarafından gerçekleştirilmektedir.</p>
    <p>Tasarımlarımızı, GÜLSELİ TEKSTİL'in yüksek kalite standartları ve kurumsal güvencesiyle sizlere sunmaktan gurur duyuyoruz.</p>
</div>
<div class="ms-kurumsal-bilgi-kartlari">
    <article><strong>Segment</strong><span>Premium kadın, erkek ve çocuk giyim koleksiyonları.</span></article>
    <article><strong>Üretim güvencesi</strong><span>GÜLSELİ TEKSTİL kalite standartlarıyla desteklenen güçlü altyapı.</span></article>
</div>
""");

        yield return ("kurumsal-kargo-teslimat", "Kargo ve Teslimat", """
<header class="ms-kurumsal-panel-baslik">
    <span>Teslimat</span>
    <h2>Kargomu Nasıl Takip Ederim?</h2>
    <p>Kargo bilgileriniz e-posta ve sipariş takip bilgileriniz üzerinden kontrol edilebilir.</p>
</header>
<div class="ms-kurumsal-adimlar">
    <article><strong>1</strong><span>Ürünleriniz kargo firmasına teslim edildiğinde e-posta adresinize "Teslimat Bilginiz" konulu otomatik bilgilendirme gönderilir.</span></article>
    <article><strong>2</strong><span>Bu e-postada kargoya verilen ürünleriniz, teslimat adresiniz, teslimat bilginiz ve kargo firması bilgileri yer alır.</span></article>
    <article><strong>3</strong><span>E-postanın alt kısmındaki izleme numarası veya sipariş numaranız ile kargonuzun durumunu takip edebilirsiniz.</span></article>
    <article><strong>4</strong><span>E-posta bilgisi bulunmayan müşterilerimiz sipariş numarasını müşteri temsilcilerimizden öğrenerek kargo durumunu sorgulayabilir.</span></article>
</div>
""");

        yield return ("kurumsal-iade-degisim", "İade ve Değişim", """
<header class="ms-kurumsal-panel-baslik">
    <span>İade</span>
    <h2>İade ve Değişim Şartları</h2>
    <p>İade süresi, ücretsiz gönderim ve kabul edilmeyen ürün koşulları.</p>
</header>
<div class="ms-kurumsal-vurgu-grid">
    <article><strong>14 Gün</strong><span>Sebep göstermeden cayma hakkı, ürünü teslim aldığınız günü takip eden ilk 14 gün içinde geçerlidir.</span></article>
    <article><strong>30 Gün</strong><span>Kusurlu ürünlerde iade süresi 30 güne kadar uzamaktadır.</span></article>
    <article><strong>Ücretsiz İade</strong><span>Anlaşmalı kargo firmalarımızla ücretsiz gönderim yapılabilir; iade kodunuzu İadelerim sayfasından alabilirsiniz.</span></article>
</div>
<div class="ms-kurumsal-metin">
    <h3>Hangi ürünler iade edilemez?</h3>
    <ul>
        <li>Tasarım ve abiye ürünler 48 saat içinde iade edilebilir; süre aşımında iade kabul edilmez.</li>
        <li>Kozmetik, kişisel bakım, iç giyim, mayo ve bikini ürünleri iade edilemez.</li>
        <li>Kitap, kopyalanabilir yazılım, DVD, VCD, CD ve kasetlerde ambalaj açılmamış olmalıdır.</li>
        <li>Ev tekstili, kişisel ürünler ve küçük ev aletleri orijinal ambalajında, eksiksiz ve kullanılmamış olmalıdır.</li>
    </ul>
    <h3>İade süreci</h3>
    <p>İade talebi teslimat tarihinden itibaren en geç 14 gün içinde oluşturulmalı ve anlaşmalı kargolarla gönderilmelidir. Faturasız, etiketsiz, yıkanmış, kullanılmış veya hasar görmüş ürünler iade edilmez.</p>
    <p>İade onaylandığında ürün tutarı en geç 3 gün içinde iade edilir. Taksitli ödemelerde iade bankanız tarafından taksitli olarak yansıtılır.</p>
</div>
""");

        yield return ("kurumsal-kullanim-kosullari", "Kullanım Koşulları", """
<header class="ms-kurumsal-panel-baslik">
    <span>Koşullar</span>
    <h2>Kullanım Koşulları</h2>
    <p>Bu internet sitesine girmeniz veya sitedeki bilgileri kullanmanız aşağıdaki koşulları kabul ettiğiniz anlamına gelir.</p>
</header>
<div class="ms-kurumsal-madde-listesi">
    <article><strong>1. Sorumluluk Reddi</strong><p>Bu internet sitesine girilmesi veya sitedeki bilgilerin kullanılması sebebiyle doğabilecek doğrudan ya da dolaylı hiçbir zarardan firmamız sorumlu değildir.</p></article>
    <article><strong>2. Değişiklik ve Güncellemeler</strong><p>Hizmetlerimizi, ürünlerimizi, kullanım koşullarını ve sitede sunulan bilgileri önceden haber vermeksizin değiştirme hakkımız saklıdır.</p></article>
    <article><strong>3. Bağlantılı Siteler</strong><p>Bu internet sitesi, kontrolümüz altında olmayan başka sitelere bağlantı içerebilir. Bu sitelerin içeriklerinden sorumluluk kabul edilmez.</p></article>
    <article><strong>4. Fikri Mülkiyet Hakları</strong><p>Sitedeki marka, logo, tasarım, yazılım, görsel ve tüm materyaller yasal koruma altındadır; izinsiz kullanılamaz, kopyalanamaz veya dağıtılamaz.</p></article>
    <article><strong>5. Yurtdışı Siparişleri</strong><p>Yurtdışı siparişlerde gümrük bedelleri ülkeye göre değişebilir. Gönderilerin gümrüğe takılması durumunda sorumluluk müşteriye aittir.</p></article>
    <article><strong>6. Yasal Uyarı Güncellemeleri</strong><p>Yasal uyarı sayfası içeriğini dilediğimiz zaman güncelleme hakkımız saklıdır.</p></article>
</div>
""");

        yield return ("kurumsal-gizlilik-guvenlik", "Gizlilik ve Güvenlik", """
<header class="ms-kurumsal-panel-baslik">
    <span>Güvenlik</span>
    <h2>Gizlilik ve Güvenlik</h2>
    <p>Gizliliğinize önem veriyoruz. Siteyi kullanarak aşağıdaki şartları kabul etmiş sayılırsınız.</p>
</header>
<div class="ms-kurumsal-madde-listesi">
    <article><strong>Kişisel Bilgiler</strong><p>Üyelik aşamasında ve sonrasında talep edilen kişisel bilgiler, Üyelik Sözleşmesi'nde belirtilen amaçlar dışında kullanılmaz ve üçüncü şahıslarla paylaşılmaz.</p></article>
    <article><strong>IP Adresleri</strong><p>Sistem sorunlarının tespiti ve çözümü için IP adresleri kullanılabilir; ayrıca demografik bilgi toplamak için anonim biçimde değerlendirilebilir.</p></article>
    <article><strong>Verilerin Kullanım Amaçları</strong><p>Siparişlerin alınması, ödeme ve teslimat süreçleri, üyelik yönetimi, pazarlama iletişimi ve size özel önerilerin sunulması için bilgileriniz işlenebilir.</p></article>
    <article><strong>Bilgilendirme E-postaları</strong><p>Üye olduğunuz andan itibaren, aksi talep edilmedikçe bilgilendirme e-postaları gönderilebilir. Dilediğiniz zaman vazgeçebilirsiniz.</p></article>
    <article><strong>Mali Bilgiler</strong><p>Satın alma işlemlerinde mali bilgiler, işleminizi gerçekleştirmek için gerekli banka ve kredi kartı kuruluşlarıyla paylaşılabilir.</p></article>
    <article><strong>Güvenlik</strong><p>Tüm kredi kartı ve kişisel bilgileriniz SSL Secure sistemi ile 256 bit şifrelenerek korunur.</p></article>
    <article><strong>KVKK</strong><p>Kişisel verileriniz izin tercihleriniz doğrultusunda analiz edilebilir ve gerekli güvenlik önlemleri alınarak işlenebilir. Sorularınız için iletişim bölümünden bize ulaşabilirsiniz.</p></article>
</div>
""");
    }

    /// <summary>
    /// E8: iade neden listesi — tasarımın _HesabimIadelerim script'indeki ana/alt neden
    /// haritası Lookup'a taşındı (plan şartı). Ana nedenler `return_reason` tipinin
    /// değerleri; alt nedenler ilgili değerin ExtraData["subReasons"] listesinde
    /// (LookupValue'da hiyerarşi yok; alt nedenler seçim anında metin snapshot olarak
    /// ReturnItem'a yazılır). İdempotent: tip varsa dokunmaz. Canlıya SQL ile eklendi.
    /// </summary>
    private static async Task SeedReturnReasonsAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        if (await context.LookupTypes.AnyAsync(t => t.Code == "return_reason"))
            return;

        var type = new LookupType
        {
            Code = "return_reason",
            NameI18n = new() { { "tr", "İade Nedeni" }, { "en", "Return Reason" } },
            IsSystem = true
        };
        context.LookupTypes.Add(type);
        await context.SaveChangesAsync();

        var nedenler = new (string Ad, string[] AltNedenler)[]
        {
            ("Beden / Kalıp Problemi", new[] { "Küçük geldi", "Büyük geldi", "Dar geldi", "Bol geldi", "Kalıbı dar", "Kalıbı geniş", "Belden olmadı", "Basenden olmadı", "Kalçadan olmadı", "Göğüs kısmı olmadı", "Omuzdan olmadı", "Kol kısmı dar geldi", "Kol kısmı bol geldi", "Bacak kısmı dar geldi", "Normal bedenime uymadı", "Beden tablosu ile uyumsuz", "Üzerimde istediğim gibi durmadı" }),
            ("Ürün Beklediğim Gibi Değil", new[] { "Fotoğraftaki gibi değil", "Açıklamadaki gibi değil", "Ürün görselden farklı duruyor", "Beklediğim kalitede değil", "Beklediğim tarzda değil", "Ürün üzerimde güzel durmadı", "Kesimi beklediğim gibi değil", "Kumaşı beklediğim gibi değil", "Ürün beklentimi karşılamadı", "Ürün anlatıldığı gibi değil", "Ürün ölçüleri beklentime uymadı" }),
            ("Defolu / Hasarlı Ürün", new[] { "Yırtık geldi", "Sökük geldi", "Leke var", "Delik var", "İp çekilmesi var", "Dikiş hatası var", "Fermuar bozuk", "Düğme eksik", "Düğme kopuk", "Baskı hatalı", "Aksesuar eksik", "Ürün deforme olmuş", "Ürün ezilmiş geldi", "Ambalaj hasarlı geldi", "Kargo sırasında zarar görmüş" }),
            ("Yanlış veya Eksik Ürün Geldi", new[] { "Yanlış ürün gönderildi", "Yanlış beden gönderildi", "Yanlış renk gönderildi", "Yanlış model gönderildi", "Siparişimde olmayan ürün geldi", "Eksik ürün geldi", "Takımın parçası eksik geldi", "Ürünün aksesuarı eksik geldi", "Kemer / kuşak eksik geldi", "Hediye ürün eksik geldi", "Ürün adedi eksik geldi" }),
            ("Renk / Model / Kumaş Problemi", new[] { "Rengi görselden farklı", "Renk tonu beklediğim gibi değil", "Yanlış renk gönderildi", "Modelini beğenmedim", "Model üzerimde iyi durmadı", "Kumaşı ince", "Kumaşı kalın", "Kumaşı sert", "Kumaşı esnemiyor", "Kumaşı rahatsız etti", "Kumaşı iç gösteriyor", "Kumaşı terletiyor", "Dokusu hoşuma gitmedi", "Deseni görselden farklı", "Kumaş kalitesi beklentimi karşılamadı" }),
            ("Sipariş Hatası Yaptım", new[] { "Yanlış beden sipariş verdim", "Yanlış renk sipariş verdim", "Yanlış ürün sipariş verdim", "Yanlış adet sipariş verdim", "Yanlışlıkla sipariş verdim", "Aynı üründen fazla sipariş verdim", "Sepete yanlış ürün ekledim", "Farklı ürün almak istiyorum", "Farklı beden almak istiyorum", "Farklı renk almak istiyorum" }),
            ("Vazgeçtim / Beğenmedim", new[] { "Almaktan vazgeçtim", "Ürünü beğenmedim", "Ürüne ihtiyacım kalmadı", "Fikrimi değiştirdim", "Hediye olarak uygun olmadı", "Hediye edilen kişi beğenmedi", "Kombinime uymadı", "Tarzıma uygun değil", "Başka ürün almaya karar verdim", "Fiyatına değmediğini düşündüm" }),
            ("Teslimat Problemi", new[] { "Geç teslim edildi", "İhtiyacım olan tarihe yetişmedi", "Teslimat süreci uzadı", "Yanlış adrese teslim edildi", "Kargo paketi hasarlı geldi", "Kargo firması kaynaklı sorun yaşadım", "Ürün teslim edilmeden iade etmek istiyorum", "Siparişi iptal edemedim, iade etmek istiyorum", "Kargo sürecinden memnun kalmadım" }),
            ("Diğer", Array.Empty<string>())
        };

        var sira = 1;
        foreach (var (ad, altNedenler) in nedenler)
        {
            context.LookupValues.Add(new LookupValue
            {
                LookupTypeId = type.Id,
                NameI18n = new() { { "tr", ad } },
                ExtraData = new() { ["subReasons"] = altNedenler.ToList() },
                SortOrder = sira++,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
        Console.WriteLine("✓ Seed: İade nedenleri (return_reason) oluşturuldu.");
    }

    /// <summary>
    /// C8: checkout sözleşme modalı + ödeme bilgi grupları için legal CMS sayfaları.
    /// Her aktif platforma 5 sayfa (mesafeli satış, ön bilgilendirme, gizlilik, kullanım,
    /// kargo) — içerik firmanın unvan/adres/vergi bilgileriyle üretilir, CMS admin'den
    /// düzenlenebilir. İdempotent: platformda herhangi bir legal sayfa varsa dokunmaz.
    /// Canlıya 2026-07-09'da aynı içerik SQL ile eklendi.
    /// </summary>
    private static async Task SeedCmsLegalPagesAsync(IServiceProvider sp)
    {
        var cms = sp.GetRequiredService<ECSPros.Cms.Infrastructure.Persistence.CmsDbContext>();
        await cms.Database.MigrateAsync();
        var core = sp.GetRequiredService<CoreDbContext>();

        var sectionType = await cms.SectionTypes.FirstOrDefaultAsync(t => t.Code == "rich_text");
        if (sectionType is null)
        {
            sectionType = new ECSPros.Cms.Domain.Entities.SectionType
            {
                Code = "rich_text",
                NameI18n = new() { ["tr"] = "Zengin Metin" },
                SettingsSchema = new() { ["html"] = "string" },
                SupportsItems = false
            };
            cms.SectionTypes.Add(sectionType);
        }

        var template = await cms.PageTemplates.FirstOrDefaultAsync(t => t.Code == "icerik-sayfasi");
        if (template is null)
        {
            template = new ECSPros.Cms.Domain.Entities.PageTemplate
            {
                Code = "icerik-sayfasi",
                NameI18n = new() { ["tr"] = "İçerik Sayfası" },
                TemplateType = "content",
                DefaultLayout = "full"
            };
            cms.PageTemplates.Add(template);
        }

        var platformlar = await core.FirmPlatforms
            .Where(fp => fp.IsActive)
            .Select(fp => new { fp.Id, FirmaAd = fp.Firm.NameI18n, fp.Firm.Address, fp.Firm.TaxOffice, fp.Firm.TaxNumber })
            .ToListAsync();

        var eklenen = 0;
        foreach (var p in platformlar)
        {
            // D3: kod bazlı idempotenlik — yeni belge türleri (üyelik/KVKK) mevcut
            // platformlara da eklenebilsin (C8'deki "hiç legal yoksa" guard'ı yetmiyordu).
            var mevcutKodlar = await cms.Pages
                .Where(s => s.FirmPlatformId == p.Id && s.PageType == "legal")
                .Select(s => s.Code).ToListAsync();

            var firma = p.FirmaAd.TryGetValue("tr", out var ad) ? ad : p.FirmaAd.Values.FirstOrDefault() ?? "Satıcı";
            var satici = $"{firma} — {p.Address}. Vergi Dairesi/No: {p.TaxOffice} / {p.TaxNumber}.";

            foreach (var (kod, baslik, html) in LegalSayfaIcerikleri(satici))
            {
                if (mevcutKodlar.Contains(kod)) continue;
                var sayfa = new ECSPros.Cms.Domain.Entities.Page
                {
                    FirmPlatformId = p.Id,
                    TemplateId = template.Id,
                    Code = kod,
                    NameI18n = new() { ["tr"] = baslik },
                    SlugI18n = new() { ["tr"] = kod },
                    PageType = "legal"
                };
                cms.Pages.Add(sayfa);
                cms.PageSections.Add(new ECSPros.Cms.Domain.Entities.PageSection
                {
                    PageId = sayfa.Id,
                    SectionTypeId = sectionType.Id,
                    Name = baslik,
                    Settings = new() { ["html"] = html },
                    SortOrder = 0
                });
                eklenen++;
            }
        }

        if (eklenen > 0 || cms.ChangeTracker.HasChanges())
        {
            await cms.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {eklenen} legal CMS sayfası oluşturuldu (C8 sözleşmeler).");
        }
    }

    private static IEnumerable<(string Kod, string Baslik, string Html)> LegalSayfaIcerikleri(string satici)
    {
        yield return ("mesafeli-satis-sozlesmesi", "Mesafeli Satış Sözleşmesi",
            $"<p><strong>Satıcı:</strong> {satici}</p>" +
            "<p><strong>Konu:</strong> Alıcının internet sitesi üzerinden elektronik ortamda sipariş verdiği ürünlerin satışı ve teslimi ile tarafların 6502 sayılı Tüketicinin Korunması Hakkında Kanun ve Mesafeli Sözleşmeler Yönetmeliği kapsamındaki hak ve yükümlülüklerinin belirlenmesidir.</p>" +
            "<p><strong>Ürün ve teslimat:</strong> Ürün cinsi, miktarı, satış bedeli, teslimat adresi, fatura adresi, teslim edilecek kişi ve teslim şekli sipariş bilgileri içinde gösterilir. Ürünler yasal süreyi aşmamak kaydıyla alıcının belirttiği adrese teslim edilir.</p>" +
            "<p><strong>Genel hükümler:</strong> Alıcı, ürünün temel nitelikleri, vergiler dahil satış fiyatı, ödeme şekli, teslimat bilgileri, satıcı bilgileri ve cayma hakkı konusunda bilgilendirildiğini kabul eder. Teslimat sırasında ürün kontrol edilmeli, kargo kaynaklı sorunlarda tutanak tutulmalıdır.</p>" +
            "<p><strong>Cayma hakkı:</strong> Alıcı, ürünü teslim aldığı tarihten itibaren 14 gün içinde herhangi bir gerekçe göstermeksizin cayma hakkını kullanabilir. İade edilecek ürünlerin faturası, ambalajı, varsa aksesuarları eksiksiz ve hasarsız olmalıdır.</p>");

        yield return ("on-bilgilendirme-formu", "Ön Bilgilendirme Formu",
            "<p><strong>Konu:</strong> Bu form, alıcı ile satıcı arasında kurulacak mesafeli satış sözleşmesine ilişkin tüketicinin önceden bilgilendirilmesi amacıyla hazırlanmıştır.</p>" +
            $"<p><strong>Satıcı bilgileri:</strong> {satici}</p>" +
            "<p><strong>Alıcı bilgileri:</strong> Alıcının adı, soyadı, adresi, telefon ve e-posta bilgileri sipariş sırasında beyan edilen bilgiler esas alınarak kullanılır.</p>" +
            "<p><strong>Teslimat ve kargo:</strong> Teslimat masrafları kampanya koşullarına göre alıcıya yansıtılabilir veya satıcı tarafından karşılanabilir. Ürünler yasal süreyi aşmamak kaydıyla alıcının belirttiği adrese gönderilir.</p>" +
            "<p><strong>Ödeme ve iade:</strong> Sipariş iptali veya cayma hakkı kullanımında ödeme yapılan yönteme göre iade süreci işletilir. Banka kaynaklı iade yansıma süreleri ilgili finans kuruluşunun işlem süreçlerine bağlıdır.</p>");

        yield return ("gizlilik-guvenlik", "Gizlilik ve Güvenlik",
            "<p>Kişisel verileriniz 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında, siparişinizin oluşturulması, teslimatı ve satış sonrası süreçlerin yürütülmesi amacıyla işlenir; yasal zorunluluklar dışında üçüncü kişilerle paylaşılmaz.</p>" +
            "<p>Ödeme sayfasında girilen kart bilgileri sitemizde saklanmaz; ödeme işlemleri güvenli ödeme altyapısı üzerinden gerçekleştirilir.</p>");

        yield return ("kullanim-kosullari", "Kullanım Koşulları",
            "<p>Bu siteyi kullanarak site kullanım koşullarını kabul etmiş sayılırsınız. Sitede yer alan ürün görselleri ve içerikler bilgilendirme amaçlıdır; izinsiz kopyalanamaz ve çoğaltılamaz.</p>" +
            "<p>Sipariş verilmesi, mesafeli satış sözleşmesi ve ön bilgilendirme formu hükümlerinin elektronik ortamda kabulü anlamına gelir.</p>");

        yield return ("kargo-teslimat", "Kargo ve Teslimat",
            "<p>Siparişleriniz ödeme onayının ardından kargoya teslim edilir. Teslimat süresi, yasal azami süre olan 30 günü aşmamak üzere adresinize ve kargo yoğunluğuna göre değişebilir.</p>" +
            "<p>Kargo ücreti ve varsa ücretsiz kargo koşulları ödeme adımındaki sipariş özetinde gösterilir. Teslimat sırasında paketi kontrol ediniz; hasarlı paketlerde kargo yetkilisine tutanak tutturunuz.</p>");

        // D3: kayıt modalındaki belge onayları
        yield return ("uyelik-sozlesmesi", "Üyelik Sözleşmesi",
            $"<p><strong>Taraflar:</strong> {satici} ile siteye üye olan kullanıcı arasında, üyeliğin oluşturulmasıyla birlikte aşağıdaki koşullar yürürlüğe girer.</p>" +
            "<p><strong>Üyelik:</strong> Üye, kayıt sırasında verdiği bilgilerin doğru ve güncel olduğunu kabul eder; hesap bilgilerini üçüncü kişilerle paylaşmamakla yükümlüdür. Hesap üzerinden yapılan işlemler üyenin sorumluluğundadır.</p>" +
            "<p><strong>Kullanım koşulları:</strong> Üye, siteyi hukuka ve dürüstlük kurallarına uygun kullanacağını; site işleyişini bozacak müdahalelerde bulunmayacağını kabul eder. Satıcı, üyelik hizmetini ve site içeriğini değiştirme, askıya alma veya sonlandırma hakkını saklı tutar.</p>" +
            "<p><strong>Fesih:</strong> Üye dilediği zaman üyeliğini sonlandırabilir. Sözleşmeye aykırılık hâlinde satıcı üyeliği askıya alabilir veya sonlandırabilir.</p>");

        yield return ("kvkk-aydinlatma", "Aydınlatma Metni",
            $"<p><strong>Veri sorumlusu:</strong> {satici}</p>" +
            "<p><strong>İşleme amaçları:</strong> Kişisel verileriniz (kimlik, iletişim, adres ve alışveriş bilgileri) 6698 sayılı Kişisel Verilerin Korunması Kanunu uyarınca; üyeliğin oluşturulması, siparişlerin alınması ve teslimi, satış sonrası hizmetler, yasal yükümlülüklerin yerine getirilmesi ve açık rızanız bulunması hâlinde ticari elektronik ileti gönderimi amaçlarıyla işlenir.</p>" +
            "<p><strong>Aktarım:</strong> Verileriniz yalnızca hizmetin gerektirdiği ölçüde kargo, ödeme ve bilişim hizmeti sağlayıcılarıyla ve yasal zorunluluk hâlinde yetkili kurumlarla paylaşılır.</p>" +
            "<p><strong>Haklarınız:</strong> KVKK'nın 11. maddesi kapsamında verilerinize erişme, düzeltme, silme, işlemeye itiraz etme ve diğer haklarınızı veri sorumlusuna başvurarak kullanabilirsiniz.</p>");
    }

    /// <summary>
    /// B4: Üyelik kaydının çalışması için varsayılan üye grubu (RegisterMemberCommand
    /// IsDefault grup arar). İdempotent — grup varsa dokunmaz. Canlıya 2026-07-09'da
    /// aynı kayıt SQL ile eklendi (Code='standart').
    /// </summary>
    private static async Task SeedCrmDefaultsAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ECSPros.Crm.Infrastructure.Persistence.CrmDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.MemberGroups.AnyAsync(g => g.IsDefault))
        {
            db.MemberGroups.Add(new ECSPros.Crm.Domain.Entities.MemberGroup
            {
                Code = "standart",
                NameI18n = new Dictionary<string, string> { ["tr"] = "Standart Üye" },
                IsDefault = true,
                IsActive = true
            });
            await db.SaveChangesAsync();
            Console.WriteLine("✓ Seed: varsayılan üye grubu (standart) oluşturuldu.");
        }
    }

    /// <summary>
    /// Storefront migration'larını uygular ve tüm kategorilere varsayılan listing_mode atar.
    /// İdempotent — zaten doğru değeri olan kayıtlara dokunmaz.
    /// </summary>
    private static async Task SeedStorefrontDefaultsAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<StorefrontDbContext>();

        // Bekleyen migration'ları uygula (listing_mode sütununu ekler)
        await db.Database.MigrateAsync();

        // "model" dışındaki tüm kategorilere listing_mode = "color" ata
        var updated = await db.ChannelCategories
            .Where(c => c.ListingMode != "model" && c.ListingMode != "color")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ListingMode, "color"));

        if (updated > 0)
            Console.WriteLine($"✓ Seed: {updated} kanal kategorisine listing_mode='color' atandı.");
    }

    private static async Task SeedIamAsync(IServiceProvider sp)
    {
        await SeedPermissionsAndRolesAsync(sp);
        await SeedAdminUserAsync(sp);
    }

    /// <summary>
    /// İdempotent: eksik dilleri ekler. Her ortamda (Production dahil) çalışır.
    /// </summary>
    public static async Task SeedLanguagesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existingCodes = (await context.Languages
            .Select(l => l.Code).ToListAsync()).ToHashSet();

        var allLanguages = new[]
        {
            new Language { Code = "tr", NativeName = "Türkçe",  Direction = "ltr", IsDefault = true,  IsActive = true, SortOrder = 1 },
            new Language { Code = "en", NativeName = "English", Direction = "ltr", IsDefault = false, IsActive = true, SortOrder = 2 },
            new Language { Code = "ar", NativeName = "العربية", Direction = "rtl", IsDefault = false, IsActive = true, SortOrder = 3 },
        };

        var toAdd = allLanguages.Where(l => !existingCodes.Contains(l.Code)).ToList();
        if (toAdd.Count > 0)
        {
            context.Languages.AddRange(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {toAdd.Count} dil eklendi ({string.Join(", ", toAdd.Select(l => l.Code))}).");
        }
    }

    /// <summary>
    /// İdempotent: permission ve rol kayıtlarını oluşturur/günceller.
    /// Mevcut kayıtlara dokunmaz, eksik olanları ekler.
    /// </summary>
    public static async Task SeedPermissionsAndRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<IamDbContext>();

        // ── Permissions ────────────────────────────────────────────────────────
        var permDefs = new[]
        {
            (Code: Permissions.CatalogPlatformManage,   Name: "Katalog Platform Yönetimi",  Module: "catalog"),
            (Code: Permissions.CatalogProductsManage,   Name: "Ürün Yönetimi",              Module: "catalog"),
            (Code: Permissions.CatalogCategoriesManage, Name: "Kategori Yönetimi",           Module: "catalog"),
            (Code: Permissions.CatalogImagesManage,     Name: "Görsel Yönetimi",             Module: "catalog"),
            (Code: Permissions.CatalogSettingsManage,   Name: "Katalog Ayarları",            Module: "catalog"),
            (Code: Permissions.InventoryManage,         Name: "Envanter Yönetimi",           Module: "inventory"),
        };

        var existingCodes = await context.Permissions.Select(p => p.Code).ToListAsync();

        foreach (var def in permDefs.Where(d => !existingCodes.Contains(d.Code)))
        {
            context.Permissions.Add(new Permission
            {
                Code           = def.Code,
                NameI18n       = new Dictionary<string, string> { { "tr", def.Name } },
                Module         = def.Module,
                PermissionType = "manage",
                IsActive       = true,
            });
        }
        await context.SaveChangesAsync();

        // ── Roles ──────────────────────────────────────────────────────────────
        var roleDefs = new[]
        {
            (Code: "super_admin",    Name: "Süper Admin",     IsSystem: true),
            (Code: "platform_admin", Name: "Platform Admin",  IsSystem: true),
            (Code: "firm_admin",     Name: "Firma Admin",     IsSystem: false),
        };

        var existingRoleCodes = await context.Roles.Select(r => r.Code).ToListAsync();

        foreach (var rd in roleDefs.Where(r => !existingRoleCodes.Contains(r.Code)))
        {
            context.Roles.Add(new Role
            {
                Code     = rd.Code,
                NameI18n = new Dictionary<string, string> { { "tr", rd.Name }, { "en", rd.Name } },
                IsSystem = rd.IsSystem,
                IsActive = true,
            });
        }
        await context.SaveChangesAsync();

        // ── Role → Permission assignments ──────────────────────────────────────
        var allPermissions    = await context.Permissions.ToListAsync();
        var allRoles          = await context.Roles.ToListAsync();
        var existingRolePerms = await context.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();

        void AssignPerms(string roleCode, IEnumerable<string> permCodes)
        {
            var role = allRoles.FirstOrDefault(r => r.Code == roleCode);
            if (role is null) return;

            foreach (var permCode in permCodes)
            {
                var perm = allPermissions.FirstOrDefault(p => p.Code == permCode);
                if (perm is null) continue;
                if (existingRolePerms.Any(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id)) continue;

                context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }

        AssignPerms("super_admin",    Permissions.AllPermissions);
        AssignPerms("platform_admin", Permissions.AllPermissions);
        AssignPerms("firm_admin",     Permissions.FirmAdminPermissions);

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Permission ve roller oluşturuldu/güncellendi.");
    }

    private static async Task SeedAdminUserAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<IamDbContext>();
        var hasher  = sp.GetRequiredService<IPasswordHasher>();

        if (await context.Users.AnyAsync(u => u.Username == "admin"))
            return;

        var superAdminRole = await context.Roles.FirstAsync(r => r.Code == "super_admin");

        var adminUser = new User
        {
            Username          = "admin",
            Email             = "admin@ecspros.com",
            PasswordHash      = hasher.Hash("Admin123!"),
            FirstName         = "Sistem",
            LastName          = "Admin",
            Department        = "IT",
            IsActive          = true,
            MustChangePassword = true,
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = superAdminRole.Id });
        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Admin kullanıcısı oluşturuldu. (admin / Admin123!)");
    }

    /// <summary>
    /// İdempotent: eksik platform tiplerini ekler.
    /// </summary>
    public static async Task SeedPlatformTypesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existingCodes = (await context.PlatformTypes
            .Select(p => p.Code).ToListAsync()).ToHashSet();

        var defaults = new[]
        {
            new PlatformType { Code = "site",          NameI18n = new() { { "tr", "Web Sitesi" },   { "en", "Website" } },          IsMarketplace = false, IsActive = true },
            new PlatformType { Code = "trendyol",      NameI18n = new() { { "tr", "Trendyol" },     { "en", "Trendyol" } },         IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "hepsiburada",   NameI18n = new() { { "tr", "Hepsiburada" },  { "en", "Hepsiburada" } },      IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "n11",           NameI18n = new() { { "tr", "n11" },          { "en", "n11" } },              IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "amazon",        NameI18n = new() { { "tr", "Amazon" },       { "en", "Amazon" } },           IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "ciceksepeti",   NameI18n = new() { { "tr", "Çiçeksepeti" },  { "en", "Ciceksepeti" } },      IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "pazarama",      NameI18n = new() { { "tr", "Pazarama" },     { "en", "Pazarama" } },         IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "mobile_app",    NameI18n = new() { { "tr", "Mobil Uygulama" },{ "en", "Mobile App" } },      IsMarketplace = false, IsActive = true },
            new PlatformType { Code = "pos",           NameI18n = new() { { "tr", "Mağaza / POS" }, { "en", "Store / POS" } },      IsMarketplace = false, IsActive = true },
        };

        var toAdd = defaults.Where(p => !existingCodes.Contains(p.Code)).ToList();
        if (toAdd.Count > 0)
        {
            context.PlatformTypes.AddRange(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {toAdd.Count} platform tipi eklendi.");
        }
    }

    private static async Task SeedCoreAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        // Diğer referans veriler sadece ilk kurulumda eklenir
        if (await context.OrderStatuses.AnyAsync())
            return;

        // Sipariş durumları
        context.OrderStatuses.AddRange(
            new OrderStatus { Code = "pending", NameI18n = new() { { "tr", "Beklemede" }, { "en", "Pending" } }, Color = "#FFA500", SortOrder = 1, IsActive = true },
            new OrderStatus { Code = "confirmed", NameI18n = new() { { "tr", "Onaylandı" }, { "en", "Confirmed" } }, Color = "#2196F3", SortOrder = 2, IsActive = true },
            new OrderStatus { Code = "preparing", NameI18n = new() { { "tr", "Hazırlanıyor" }, { "en", "Preparing" } }, Color = "#9C27B0", SortOrder = 3, IsActive = true },
            new OrderStatus { Code = "shipped", NameI18n = new() { { "tr", "Kargoda" }, { "en", "Shipped" } }, Color = "#00BCD4", SortOrder = 4, IsActive = true },
            new OrderStatus { Code = "delivered", NameI18n = new() { { "tr", "Teslim Edildi" }, { "en", "Delivered" } }, Color = "#4CAF50", SortOrder = 5, IsActive = true },
            new OrderStatus { Code = "cancelled", NameI18n = new() { { "tr", "İptal Edildi" }, { "en", "Cancelled" } }, Color = "#F44336", SortOrder = 6, IsActive = true },
            new OrderStatus { Code = "returned", NameI18n = new() { { "tr", "İade Edildi" }, { "en", "Returned" } }, Color = "#795548", SortOrder = 7, IsActive = true }
        );

        // Ödeme yöntemleri
        context.PaymentMethods.AddRange(
            new PaymentMethod { Code = "credit_card", NameI18n = new() { { "tr", "Kredi Kartı" }, { "en", "Credit Card" } }, IsOnline = true, RequiresConfirmation = false, IsActive = true, SortOrder = 1 },
            new PaymentMethod { Code = "bank_transfer", NameI18n = new() { { "tr", "Havale/EFT" }, { "en", "Bank Transfer" } }, IsOnline = false, RequiresConfirmation = true, IsActive = true, SortOrder = 2 },
            new PaymentMethod { Code = "cash_on_delivery", NameI18n = new() { { "tr", "Kapıda Ödeme" }, { "en", "Cash on Delivery" } }, IsOnline = false, RequiresConfirmation = false, IsActive = true, SortOrder = 3 },
            new PaymentMethod { Code = "pos", NameI18n = new() { { "tr", "POS" }, { "en", "POS Terminal" } }, IsOnline = false, RequiresConfirmation = false, IsActive = true, SortOrder = 4 },
            new PaymentMethod { Code = "wallet", NameI18n = new() { { "tr", "Cüzdan" }, { "en", "Wallet" } }, IsOnline = true, RequiresConfirmation = false, IsActive = true, SortOrder = 5 }
        );

        // Lookup tipleri
        var genderType = new LookupType
        {
            Code = "gender",
            NameI18n = new() { { "tr", "Cinsiyet" }, { "en", "Gender" } },
            IsSystem = true
        };
        context.LookupTypes.Add(genderType);
        await context.SaveChangesAsync();

        context.LookupValues.AddRange(
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Erkek" }, { "en", "Male" } }, SortOrder = 1, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Kadın" }, { "en", "Female" } }, SortOrder = 2, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Diğer" }, { "en", "Other" } }, SortOrder = 3, IsActive = true }
        );

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Core referans verileri oluşturuldu.");
    }

    private static async Task SeedCatalogAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CatalogDbContext>();

        // ImageServer ayarları
        var imageServerKeys = new Dictionary<string, string>
        {
            ["ImageServer.FtpHost"]        = "localhost",
            ["ImageServer.FtpPort"]        = "21",
            ["ImageServer.FtpUser"]        = "anonymous",
            ["ImageServer.FtpPassword"]    = "",
            ["ImageServer.FtpBasePath"]    = "/images/products/",
            ["ImageServer.PublicBaseUrl"]  = "/media/images/products/",
            ["ImageServer.LocalSavePath"]  = "/opt/ECSProsAI/media/images/products/",
            // CDN ayarları — https://cdn.example.com/img/{height}/{quality}/{fileName}
            ["ImageServer.CdnBaseUrl"]     = "",
            ["ImageServer.CdnQuality"]     = "85",
            ["ImageServer.CdnThumbHeight"] = "240",
            ["ImageServer.CdnListHeight"]  = "640",
            ["ImageServer.CdnZoomHeight"]  = "1200",
            ["VideoServer.LocalSavePath"]  = "/opt/ECSProsAI/media/videos/products/",
            ["VideoServer.PublicBaseUrl"]  = "/media/videos/products/",
            ["VideoServer.FtpHost"]        = "localhost",
            ["VideoServer.FtpPort"]        = "21",
            ["VideoServer.FtpUser"]        = "anonymous",
            ["VideoServer.FtpPassword"]    = "",
            ["VideoServer.FtpBasePath"]    = "/videos/products/"
        };

        foreach (var (key, defaultValue) in imageServerKeys)
        {
            if (!await context.CatalogSettings.AnyAsync(x => x.Key == key))
            {
                context.CatalogSettings.Add(new CatalogSetting { Key = key, Value = defaultValue });
            }
        }

        // Barkod sequence başlangıç değeri
        if (!await context.CatalogSettings.AnyAsync(x => x.Key == "barcode_sequence"))
        {
            context.CatalogSettings.Add(new CatalogSetting { Key = "barcode_sequence", Value = "1" });
        }

        await context.SaveChangesAsync();

        // Varsayılan görsel seti
        // Adım 1: 'varsayilan' gibi görünen ama farklı byte içeren eski/bozuk satırları soft-delete et
        // (Örn: i + Unicode combining dot — ASCII 'i'den farklı byte dizisi, ama görsel olarak aynı)
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE definition.image_sets
            SET ""IsDeleted"" = true, ""DeletedAt"" = NOW()
            WHERE ""IsDeleted"" = false
              AND ""Code"" LIKE 'varsay%'
              AND ""Code"" != 'varsayilan'
        ");

        // Adım 2: UPSERT — aktif saf-ASCII 'varsayilan' satırı varsa güncelle, yoksa ekle
        await context.Database.ExecuteSqlRawAsync($@"
            INSERT INTO definition.image_sets
                (""Id"", ""Code"", ""Name"", ""IsDefault"", ""FallbackSetId"",
                 ""SortPriority"", ""IsActive"", ""IsDeleted"", ""CreatedAt"",
                 ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"", ""DeletedAt"", ""DeletedBy"")
            VALUES
                ('{Guid.NewGuid()}', 'varsayilan', 'Varsayılan Resim Seti', true, NULL,
                 0, true, false, NOW(), NULL, NULL, NULL, NULL, NULL)
            ON CONFLICT (""Code"") WHERE NOT ""IsDeleted""
            DO UPDATE SET
                ""Name""      = 'Varsayılan Resim Seti',
                ""IsDefault"" = true,
                ""IsActive""  = true,
                ""IsDeleted"" = false,
                ""DeletedAt"" = NULL,
                ""DeletedBy"" = NULL
        ");

        Console.WriteLine("✓ Seed: Catalog ayarları (ImageServer, barcode_sequence, ImageSet) oluşturuldu.");

        // Temel attribute type'ları
        await SeedAttributeTypesAsync(context);

        // "Filtre Rengi" attribute type + değerleri
        await SeedFilterRengiAttributeTypeAsync(context);

        // Demografik attribute değerleri
        await SeedCinsiyetValuesAsync(context);
        await SeedYasGrubuValuesAsync(context);

        // Var/Yok tipi ikili özelliklerin değerleri
        await SeedVarYokValuesAsync(context, "fermuar", "esneklik", "balen", "dolgu", "ic_cep");

        // Ayakkabı beden değerleri
        await SeedShoeBedenValuesAsync(context);

        // Ürün grupları
        await SeedProductGroupsAsync(context);

        // Ürün grubu özellik atamaları
        await SeedProductGroupAttributesAsync(context);
    }

    private static async Task SeedAttributeTypesAsync(CatalogDbContext db)
    {
        // filtre_rengi SeedFilterRengiAttributeTypeAsync tarafından ayrıca ekleniyor
        // (code, name_tr, dataType) — NameI18n sadece "tr" içerir, İngilizce çeviri tutulmuyor
        var canonical = new (string Code, string Tr, string DataType)[]
        {
            // Temel varyant eksenleri
            ("renk",         "Renk",              "select"),
            ("beden",        "Beden",             "select"),
            // Demografik
            ("cinsiyet",     "Cinsiyet",          "select"),
            ("yas_grubu",    "Yaş Grubu",         "select"),
            ("boy",          "Boy",               "select"),
            // Genel ürün özellikleri
            ("season",       "Sezon",             "select"),
            ("yil",          "Yıl",               "select"),
            ("malzeme",      "Malzeme",           "select"),
            ("kumas_turu",   "Kumaş Türü",        "select"),
            ("marka",        "Marka",             "select"),
            ("desen",        "Desen",             "select"),
            // Kesim / silüet
            ("kalip",        "Kalıp",             "select"),
            ("kol_tipi",     "Kol Tipi",          "select"),
            ("yaka_tipi",    "Yaka Tipi",         "select"),
            ("etek_tipi",    "Etek Tipi",         "select"),
            // Alt giyim
            ("paca_tipi",    "Paça Tipi",         "select"),
            ("bel_tipi",     "Bel Tipi",          "select"),
            ("bel",          "Bel Ölçüsü",        "select"),
            ("ic_uzunluk",   "İç Uzunluk",        "select"),
            ("gogus",           "Göğüs Ölçüsü",   "select"),
            ("basen",           "Basen Ölçüsü",   "select"),
            ("kol_boyu",        "Kol Boyu",       "select"),
            ("omuz_genisligi",  "Omuz Genişliği", "select"),
            ("urun_boyu",       "Ürün Boyu",      "select"),
            // Dış giyim
            ("astar_durumu", "Astar Durumu",      "select"),
            ("kapatma_tipi", "Kapatma Tipi",      "select"),
            ("kalinlik",     "Kalınlık",          "select"),
            ("fermuar",      "Fermuar",           "select"),
            ("esneklik",     "Esneklik",          "select"),
            ("balen",        "Balen / Tel",       "select"),
            ("dolgu",        "Dolgu",             "select"),
            ("ic_cep",       "İç Cep",            "select"),
            // Ayakkabı
            ("topuk_tipi",   "Topuk Tipi",        "select"),
            ("topuk_boyu",   "Topuk Boyu",        "select"),
            ("ortam",        "Ortam",             "select"),
            ("taban_ozelligi",   "Taban Özelliği",     "select"),
            ("taban_yuksekligi", "Taban Yüksekliği",   "select"),
            ("dis_materyal",     "Dış Materyal",       "select"),
            ("ic_yuzey",         "İç Yüzey",           "select"),
            // Çanta
            ("canta_agzi",   "Çanta Ağzı",        "select"),
            ("aski_tipi",    "Askı Tipi",         "select"),
            ("aski_boyu",    "Askı Boyu",         "select"),
            // Manken (bkz. docs/manken-ozelligi-spec.md) — varyant üretmez, bilgilendirici;
            // değeri ProductAttribute.CustomValue JSONB alanında tutulur, AttributeValue havuzu kullanılmaz
            ("manken",       "Manken",            "json"),
            // Diğer
            ("cep_tipi",     "Cep Tipi",          "select"),
            ("cep_sayisi",   "Cep Sayısı",        "select"),
        };

        var existingCodes = new HashSet<string>(await db.AttributeTypes.Select(a => a.Code).ToListAsync());
        int added = 0, updated = 0;

        foreach (var (code, tr, dt) in canonical)
        {
            if (!existingCodes.Contains(code))
            {
                db.AttributeTypes.Add(new AttributeType
                {
                    Id = Guid.NewGuid(), Code = code,
                    NameI18n = new Dictionary<string, string> { ["tr"] = tr },
                    DataType = dt, IsActive = true, CreatedAt = DateTime.UtcNow
                });
                added++;
            }
        }

        if (added > 0) await db.SaveChangesAsync();
        if (added > 0 || updated > 0)
            Console.WriteLine($"✓ Seed: {added} attribute type eklendi.");
        else
            Console.WriteLine("✓ Seed: Attribute type'lar güncel.");
    }

    private static async Task SeedShoeBedenValuesAsync(CatalogDbContext db)
    {
        var bedenType = await db.AttributeTypes.FirstOrDefaultAsync(a => a.Code == "beden");
        if (bedenType == null) return;

        // Avrupa numaralandırması: bebek (16–27), çocuk (28–35), yetişkin (36–50)
        var sizes = Enumerable.Range(16, 35).Select(n => n.ToString()).ToList(); // 16–50

        var existingNames = new HashSet<string>(await db.AttributeValues
            .Where(v => v.AttributeTypeId == bedenType.Id)
            .Select(v => v.NameI18n["tr"])
            .ToListAsync());

        int added = 0;
        // Başlangıç sort: mevcut maksimumun üstünden devam et
        int sort = (await db.AttributeValues
            .Where(v => v.AttributeTypeId == bedenType.Id)
            .MaxAsync(v => (int?)v.SortOrder) ?? 0) + 10;

        foreach (var size in sizes)
        {
            if (existingNames.Contains(size)) continue;
            db.AttributeValues.Add(new AttributeValue
            {
                Id = Guid.NewGuid(),
                AttributeTypeId = bedenType.Id,
                NameI18n = new Dictionary<string, string> { ["tr"] = size, ["en"] = size },
                SortOrder = sort,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            sort += 10;
            added++;
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: {added} ayakkabı bedeni eklendi (beden attribute).");
    }

    private static async Task SeedProductGroupsAsync(CatalogDbContext db)
    {
        // (code, nameTr, sortOrder)
        var groups = new (string code, string name, int sort)[]
        {
            // ── Aktarım kökenli gruplar (grp_X) ──────────────────────────────
            ("grp_1",   "Elbise",                    1),
            ("grp_10",  "Etek",                      1),
            ("grp_11",  "Sweatshirt",                1),
            ("grp_118", "İç Giyim",                  1),
            ("grp_12",  "Hırka",                     1),
            ("grp_123", "Plaj Giyim",                1),
            ("grp_132", "Kişisel Bakım",             1),
            ("grp_137", "Makyaj Malzemeleri",        1),
            ("grp_14",  "Triko",                     1),
            ("grp_149", "Kostüm",                    1),
            ("grp_15",  "Pijama",                    1),
            ("grp_159", "Zıbın",                     1),
            ("grp_16",  "Bolero",                    1),
            ("grp_165", "Banyo Giyim",               1),
            ("grp_17",  "Yelek",                     1),
            ("grp_174", "Şal",                       1),
            ("grp_176", "Telefon ve Aksesuarları",   1),
            ("grp_177", "Bilgisayar",                1),
            ("grp_178", "Televizyon ve Aksesuar",    1),
            ("grp_179", "Elektirikli Ev Aletleri",   1),
            ("grp_18",  "Tunik",                     1),
            ("grp_180", "Beyaz Eşya",                1),
            ("grp_181", "Mobilyalar",                1),
            ("grp_182", "Dekorasyon ve Aydınlatma",  1),
            ("grp_183", "Ev Tekstil",                1),
            ("grp_184", "Mutfak Gereçleri",          1),
            ("grp_185", "Banyo ve Ev Gereçleri",     1),
            ("grp_186", "Çeyiz Setleri",             1),
            ("grp_198", "Kimono",                    1),
            ("grp_2",   "Aksesuar",                  1),
            ("grp_21",  "Bot",                       1),
            ("grp_44",  "Body",                      1),
            ("grp_24",  "Çizme",                     1),
            ("grp_25",  "Babet",                     1),
            ("grp_254", "Kapri",                     1),
            ("grp_262", "Trençkot",                  0),
            ("grp_269", "Kulaklık",                  0),
            ("grp_27",  "Sandalet",                  1),
            ("grp_3",   "Pantolon",                  1),
            ("grp_33",  "Terlik",                    1),
            ("grp_36",  "Tulum",                     1),
            ("grp_46",  "Ceket",                     1),
            ("grp_47",  "Eşofman",                   1),
            ("grp_48",  "İkili Takım",               1),
            ("grp_5",   "Gömlek",                    1),
            ("grp_6",   "Bluz",                      1),
            ("grp_63",  "Takım Elbise",              1),
            ("grp_7",   "T-Shirt",                   1),
            ("grp_70",  "Aktif Spor",                1),
            ("grp_73",  "Mont",                      1),
            ("grp_77",  "Kap",                       1),
            ("grp_80",  "Panço",                     1),
            ("grp_83",  "Çanta",                     1),
            ("grp_9",   "Bustiyer",                  1),
            ("grp_95",  "Şort",                      1),
            // ── Yeni gruplar ──────────────────────────────────────────────────
            ("spor_ayakkabi",   "Spor Ayakkabı",     1),
            ("gunluk_ayakkabi", "Günlük Ayakkabı",   1),
            ("topuklu_ayakkabi","Topuklu Ayakkabı",  1),
            ("stiletto",        "Stiletto",           1),
            ("klasik_ayakkabi", "Klasik Ayakkabı",   1),
            ("kaban",           "Kaban",              1),
            ("atlet",           "Atlet",              1),
            ("bornoz",          "Bornoz",             1),
            ("bone",            "Bone",               1),
            ("havlu",           "Havlu",              1),
            ("pestemal",        "Peştemal",           1),
            ("ferace",          "Ferace",             1),
            ("esarp",           "Eşarp",              1),
            ("pelus_terlik",        "Peluş Terlik",       1),
            ("hamile_giyim",        "Hamile Giyim",       1),
            ("sevgili_kombini",     "Sevgili Kombini",    1),
        };

        var existingCodes = new HashSet<string>(
            await db.ProductGroups.Select(x => x.Code).ToListAsync());

        int added = 0;
        foreach (var (code, name, sort) in groups)
        {
            if (existingCodes.Contains(code)) continue;
            db.ProductGroups.Add(new ProductGroup
            {
                Id        = Guid.NewGuid(),
                Code      = code,
                NameI18n  = new Dictionary<string, string> { ["tr"] = name },
                IsActive  = true,
                SortOrder = sort,
                CreatedAt = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Ürün grupları — {added} yeni eklendi, {existingCodes.Count} zaten vardı.");
    }

    private static async Task SeedProductGroupAttributesAsync(CatalogDbContext db)
    {
        var attrTypes = await db.AttributeTypes
            .ToDictionaryAsync(a => a.Code, a => a.Id);
        var groups = await db.ProductGroups
            .ToDictionaryAsync(g => g.Code, g => g.Id);

        var existingPgas = await db.ProductGroupAttributes
            .Select(x => new { x.ProductGroupId, x.AttributeTypeId })
            .ToListAsync();
        var pgaSet = new HashSet<(Guid, Guid)>(existingPgas.Select(x => (x.ProductGroupId, x.AttributeTypeId)));

        var existingSubAttrs = await db.ProductGroupAxisSubAttributes
            .Select(x => new { x.ProductGroupId, x.AxisAttributeTypeId, x.SubAttributeTypeId })
            .ToListAsync();
        var subSet = new HashSet<(Guid, Guid, Guid)>(existingSubAttrs.Select(x => (x.ProductGroupId, x.AxisAttributeTypeId, x.SubAttributeTypeId)));

        int added = 0, subAdded = 0;

        void Attr(string grpCode, string attrCode, bool isVariant, bool isPrimary, bool isRequired, int sort)
        {
            if (!groups.TryGetValue(grpCode, out var gid)) return;
            if (!attrTypes.TryGetValue(attrCode, out var aid)) return;
            if (pgaSet.Contains((gid, aid))) return;
            db.ProductGroupAttributes.Add(new ProductGroupAttribute
            {
                Id = Guid.NewGuid(), ProductGroupId = gid, AttributeTypeId = aid,
                IsVariant = isVariant, IsPrimaryAxis = isPrimary,
                IsRequired = isRequired, SortOrder = sort, CreatedAt = DateTime.UtcNow
            });
            pgaSet.Add((gid, aid));
            added++;
        }

        void Sub(string grpCode, string axisCode, string subCode, bool isRequired, int sort)
        {
            if (!groups.TryGetValue(grpCode, out var gid)) return;
            if (!attrTypes.TryGetValue(axisCode, out var aid)) return;
            if (!attrTypes.TryGetValue(subCode, out var sid)) return;
            if (subSet.Contains((gid, aid, sid))) return;
            db.ProductGroupAxisSubAttributes.Add(new ProductGroupAxisSubAttribute
            {
                Id = Guid.NewGuid(), ProductGroupId = gid, AxisAttributeTypeId = aid,
                SubAttributeTypeId = sid, IsRequired = isRequired, SortOrder = sort,
                CreatedAt = DateTime.UtcNow
            });
            subSet.Add((gid, aid, sid));
            subAdded++;
        }

        // Giyim: renk (V,P) + beden (V) + material + cinsiyet + season + esneklik + ekstralar
        // + ürün boyu (beden eksenine bağlı ölçü sub-attribute'ü — hemen hemen tüm giyimde geçerli)
        // astar_durumu/boy/fermuar/kumas_turu/yas_grubu: 2026-07-02 legacy veri analizinde neredeyse
        // tüm giyim gruplarında gerçek değer bulunduğu görüldü (bkz. project_phase13_product_attribute_values_2026-07-02),
        // bu yüzden extras yerine ortak base attribute'e taşındı.
        void Cloth(string g, params string[] extras)
        {
            Attr(g, "renk",      true,  true,  true,  1);
            Attr(g, "beden",     true,  false, true,  2);
            Attr(g, "malzeme",  false, false, false, 3);
            Attr(g, "cinsiyet",  false, false, false, 4);
            Attr(g, "season",    false, false, false, 5);
            Attr(g, "esneklik",  false, false, false, 6);
            Attr(g, "manken",    false, false, false, 99);
            Attr(g, "astar_durumu", false, false, false, 100);
            Attr(g, "boy",          false, false, false, 101);
            Attr(g, "fermuar",      false, false, false, 102);
            Attr(g, "kumas_turu",   false, false, false, 103);
            Attr(g, "yas_grubu",    false, false, false, 104);
            int s = 7;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
            Sub(g, "beden", "urun_boyu", false, 50);
        }

        // Ayakkabı: renk (V,P) + beden (V) + material + cinsiyet + season + taban/dış materyal + ekstralar
        // astar_durumu/yas_grubu: 2026-07-02 legacy veri analizinde ayakkabı gruplarında da gerçek
        // değer bulunduğu görüldü, base attribute'e taşındı (bkz. project_phase13_product_attribute_values_2026-07-02).
        void Shoe(string g, params string[] extras)
        {
            Attr(g, "renk",     true,  true,  true,  1);
            Attr(g, "beden",    true,  false, true,  2);
            Attr(g, "malzeme", false, false, false, 3);
            Attr(g, "cinsiyet", false, false, false, 4);
            Attr(g, "season",   false, false, false, 5);
            Attr(g, "taban_ozelligi",   false, false, false, 6);
            Attr(g, "taban_yuksekligi", false, false, false, 7);
            Attr(g, "dis_materyal",     false, false, false, 8);
            Attr(g, "ic_yuzey",         false, false, false, 9);
            Attr(g, "manken",           false, false, false, 99);
            Attr(g, "astar_durumu",     false, false, false, 100);
            Attr(g, "yas_grubu",        false, false, false, 101);
            int s = 10;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
        }

        // Aksesuar/çanta: renk (V,P) + material + cinsiyet + season + ekstralar (beden yok)
        // astar_durumu/fermuar/kumas_turu/yas_grubu: 2026-07-02 legacy veri analizinde çanta/aksesuar
        // gruplarında da gerçek değer bulunduğu görüldü, base attribute'e taşındı
        // (bkz. project_phase13_product_attribute_values_2026-07-02).
        void Acc(string g, params string[] extras)
        {
            Attr(g, "renk",     true,  true,  true,  1);
            Attr(g, "malzeme", false, false, false, 2);
            Attr(g, "cinsiyet", false, false, false, 3);
            Attr(g, "season",   false, false, false, 4);
            Attr(g, "astar_durumu", false, false, false, 100);
            Attr(g, "fermuar",      false, false, false, 101);
            Attr(g, "kumas_turu",   false, false, false, 102);
            Attr(g, "yas_grubu",    false, false, false, 103);
            int s = 5;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
        }

        // Ev/tekstil/elektronik: renk (V,P) + ekstralar
        void Home(string g, params string[] extras)
        {
            Attr(g, "renk", true, true, false, 1);
            int s = 2;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
        }

        // Kolu olan üst giyim için beden eksenine bağlı ölçü sub-attribute'leri
        void UstOlcu(string g)
        {
            Sub(g, "beden", "gogus",          false, 10);
            Sub(g, "beden", "kol_boyu",       false, 11);
            Sub(g, "beden", "omuz_genisligi", false, 12);
        }

        // ── ÜST GİYİM ───────────────────────────────────────────────────
        Cloth("grp_6",   "desen", "kol_tipi", "yaka_tipi", "kalip");           // Bluz
        UstOlcu("grp_6");
        Cloth("grp_16",  "desen", "kol_tipi");                                          // Bolero
        UstOlcu("grp_16");
        Cloth("grp_9",   "desen", "kapatma_tipi", "balen", "dolgu");                    // Bustiyer
        Cloth("grp_46",  "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "cep_tipi", "fermuar", "ic_cep"); // Ceket
        UstOlcu("grp_46");
        Cloth("grp_47",  "desen", "kalip");                                     // Eşofman
        Cloth("grp_5",   "desen", "kol_tipi", "yaka_tipi", "kalip");           // Gömlek
        UstOlcu("grp_5");
        Cloth("grp_12",  "desen", "kol_tipi", "yaka_tipi", "kalip");                   // Hırka
        UstOlcu("grp_12");
        Cloth("grp_77",  "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu");   // Kap
        UstOlcu("grp_77");
        Cloth("grp_198", "desen", "kol_tipi", "kalip");                                // Kimono
        UstOlcu("grp_198");
        Cloth("grp_73",  "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "kalinlik", "cep_tipi", "fermuar", "ic_cep"); // Mont
        UstOlcu("grp_73");
        Cloth("grp_80",  "desen", "kalip");                                             // Panço
        Cloth("grp_11",  "desen", "kol_tipi", "yaka_tipi", "kalip");           // Sweatshirt
        UstOlcu("grp_11");
        Cloth("grp_7",   "desen", "kol_tipi", "yaka_tipi", "kalip");           // T-Shirt
        UstOlcu("grp_7");
        Cloth("grp_262", "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "fermuar", "ic_cep"); // Trençkot
        UstOlcu("grp_262");
        Cloth("grp_14",  "desen", "kol_tipi", "yaka_tipi", "kalip", "kalinlik");       // Triko
        UstOlcu("grp_14");
        Cloth("grp_18",  "desen", "kol_tipi", "yaka_tipi", "kalip");                   // Tunik
        UstOlcu("grp_18");
        Cloth("grp_17",  "desen", "yaka_tipi", "kalip");                               // Yelek

        // ── ALT GİYİM ───────────────────────────────────────────────────
        // Etek: paca_tipi yok; bel_tipi + etek_tipi grup seviyesinde
        Cloth("grp_10",  "desen", "kalip", "bel_tipi", "etek_tipi");
        Sub("grp_10",  "beden", "bel",   false, 1);
        Sub("grp_10",  "beden", "basen", false, 2);

        // Kapri: paca_tipi ve bel_tipi grup seviyesinde; bel ölçüsü beden sub-attr
        Cloth("grp_254", "desen", "kalip", "bel_tipi", "paca_tipi");
        Sub("grp_254", "beden", "bel",        false, 1);
        Sub("grp_254", "beden", "ic_uzunluk", false, 2);
        Sub("grp_254", "beden", "basen",      false, 3);

        // Pantolon: paca_tipi ve bel_tipi grup seviyesinde; bel + iç uzunluk beden sub-attr
        Cloth("grp_3",   "desen", "kalip", "bel_tipi", "paca_tipi", "cep_tipi");
        Sub("grp_3",   "beden", "bel",        false, 1);
        Sub("grp_3",   "beden", "ic_uzunluk", false, 2);
        Sub("grp_3",   "beden", "basen",      false, 3);

        // Şort: bel_tipi grup seviyesinde; bel ölçüsü beden sub-attr
        Cloth("grp_95",  "desen", "kalip", "bel_tipi");
        Sub("grp_95",  "beden", "bel",   false, 1);
        Sub("grp_95",  "beden", "basen", false, 2);

        // ── ELBİSE / TAM VÜCUT ──────────────────────────────────────────
        Cloth("grp_1",   "desen", "kol_tipi", "yaka_tipi", "kalip", "etek_tipi");
        Sub("grp_1",   "beden", "bel",   false, 1);
        Sub("grp_1",   "beden", "basen", false, 2);
        UstOlcu("grp_1");

        Cloth("grp_44",  "desen", "kol_tipi", "yaka_tipi", "kapatma_tipi", "balen", "dolgu"); // Body
        UstOlcu("grp_44");
        Cloth("grp_149", "desen");                                              // Kostüm
        Cloth("grp_36",  "desen", "kol_tipi", "yaka_tipi", "kalip");                   // Tulum
        UstOlcu("grp_36");

        // ── TAKIM / SET ─────────────────────────────────────────────────
        Cloth("grp_48",  "desen", "kalip");                                    // İkili Takım
        Cloth("grp_63",  "desen", "kalip", "bel_tipi", "paca_tipi");          // Takım Elbise
        Sub("grp_63",  "beden", "bel",        false, 1);
        Sub("grp_63",  "beden", "ic_uzunluk", false, 2);
        Sub("grp_63",  "beden", "basen",      false, 3);

        // ── İÇ GİYİM / PİJAMA ───────────────────────────────────────────
        Cloth("grp_118", "desen", "kapatma_tipi", "balen", "dolgu");                   // İç Giyim
        Cloth("grp_15",  "desen", "kalip");                                            // Pijama

        // ── SPOR / AKTİF GİYİM ─────────────────────────────────────────
        Cloth("grp_70",  "desen", "kalip");                                    // Aktif Spor

        // ── PLAJ / BANYO GİYİM ──────────────────────────────────────────
        Cloth("grp_123", "desen", "kalip");                                            // Plaj Giyim
        Cloth("grp_165", "desen");                                                     // Banyo Giyim

        // ── BEBEK ───────────────────────────────────────────────────────
        Cloth("grp_159", "desen", "yas_grubu");                                        // Zıbın

        // ── AYAKKABI ────────────────────────────────────────────────────
        Shoe("grp_25",   "topuk_boyu", "topuk_tipi");                                  // Babet
        Shoe("grp_21",   "topuk_boyu", "topuk_tipi", "astar_durumu", "ortam", "fermuar"); // Bot
        Shoe("grp_24",   "topuk_boyu", "topuk_tipi", "astar_durumu", "ortam", "fermuar"); // Çizme
        Shoe("grp_27",   "topuk_boyu", "topuk_tipi");                                  // Sandalet
        Shoe("grp_33",   "ortam");                                                     // Terlik

        // ── AKSESUAR / ŞAL / ÇANTA ──────────────────────────────────────
        Acc("grp_174", "desen");                                                       // Şal
        Acc("grp_2");                                                                  // Aksesuar
        Acc("grp_83",  "desen", "kapatma_tipi", "fermuar", "ic_cep", "canta_agzi", "aski_tipi", "aski_boyu"); // Çanta

        // ── EV TEKSTİL / ÇEYİZ ─────────────────────────────────────────
        Home("grp_183", "malzeme", "desen", "kumas_turu", "fermuar");         // Ev Tekstil
        Home("grp_186", "malzeme", "desen");                                          // Çeyiz Setleri
        Home("grp_185", "malzeme");                                           // Banyo ve Ev Gereçleri

        // ── ELEKTRONİK / EV ALETLERİ ───────────────────────────────────
        Home("grp_176", "malzeme");                                                   // Telefon ve Aksesuarları
        Home("grp_177", "malzeme");                                                   // Bilgisayar
        Home("grp_178");                                                               // Televizyon ve Aksesuar
        Home("grp_179");                                                               // Elektirikli Ev Aletleri
        Home("grp_180");                                                               // Beyaz Eşya
        Home("grp_181", "malzeme");                                           // Mobilyalar
        Home("grp_182", "malzeme");                                           // Dekorasyon ve Aydınlatma
        Home("grp_184", "malzeme");                                                   // Mutfak Gereçleri

        // ── KİŞİSEL BAKIM / MAKYAJ ─────────────────────────────────────
        Home("grp_132");                                                               // Kişisel Bakım
        Home("grp_137");                                                               // Makyaj Malzemeleri
        Home("grp_269", "malzeme");                                                   // Kulaklık

        // ── YENİ GRUPLAR — Ayakkabı ─────────────────────────────────────
        Shoe("spor_ayakkabi",        "ortam");                                         // Spor Ayakkabı
        Shoe("gunluk_ayakkabi",      "topuk_boyu", "topuk_tipi", "ortam");             // Günlük Ayakkabı
        Shoe("topuklu_ayakkabi",     "topuk_boyu", "topuk_tipi");                      // Topuklu Ayakkabı
        Shoe("stiletto",             "topuk_boyu", "topuk_tipi");                      // Stiletto
        Shoe("klasik_ayakkabi",      "topuk_boyu", "topuk_tipi", "astar_durumu", "ortam"); // Klasik Ayakkabı
        Shoe("pelus_terlik",         "ortam");                                         // Peluş Terlik

        // ── YENİ GRUPLAR — Giyim ────────────────────────────────────────
        Cloth("kaban",           "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "kalinlik", "cep_tipi", "fermuar", "ic_cep"); // Kaban
        UstOlcu("kaban");
        Cloth("atlet",           "desen", "yaka_tipi");                                 // Atlet
        Cloth("hamile_giyim",    "desen", "kol_tipi", "yaka_tipi", "kalip");    // Hamile Giyim
        UstOlcu("hamile_giyim");
        Sub("hamile_giyim", "beden", "basen", false, 1);
        Cloth("ferace",          "desen", "kol_tipi", "yaka_tipi", "kalip");            // Ferace
        UstOlcu("ferace");
        Cloth("sevgili_kombini", "desen", "kalip");                             // Sevgili Kombini
        Cloth("bornoz",          "desen", "kalip");                                     // Bornoz

        // ── YENİ GRUPLAR — Aksesuar ─────────────────────────────────────
        Acc("bone",  "desen");                                                          // Bone
        Acc("esarp", "desen");                                                          // Eşarp

        // ── YENİ GRUPLAR — Ev Tekstil ───────────────────────────────────
        Home("havlu",    "malzeme", "desen");                                          // Havlu
        Home("pestemal", "malzeme", "desen");                                          // Peştemal

        if (added > 0 || subAdded > 0)
            await db.SaveChangesAsync();

        Console.WriteLine($"✓ Seed: {added} grup özelliği, {subAdded} eksen alt özelliği eklendi.");
    }

    private static async Task SeedFilterRengiAttributeTypeAsync(CatalogDbContext context)
    {
        const string typeCode = "filtre_rengi";

        var attrType = await context.AttributeTypes.FirstOrDefaultAsync(a => a.Code == typeCode);
        if (attrType is null)
        {
            attrType = new AttributeType
            {
                Id        = Guid.NewGuid(),
                Code      = typeCode,
                NameI18n  = new Dictionary<string, string> { ["tr"] = "Filtre Rengi", ["en"] = "Filter Color" },
                DataType  = "select",
                IsActive  = true,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow,
            };
            context.AttributeTypes.Add(attrType);
            await context.SaveChangesAsync();
            Console.WriteLine("✓ Seed: 'filtre_rengi' attribute type eklendi.");
        }

        var colors = new[]
        {
            ("siyah",      "Siyah",      "Black",       "#000000"),
            ("beyaz",      "Beyaz",      "White",       "#FFFFFF"),
            ("gri",        "Gri",        "Grey",        "#808080"),
            ("acik_gri",   "Açık Gri",   "Light Grey",  "#D3D3D3"),
            ("koyu_gri",   "Koyu Gri",   "Dark Grey",   "#404040"),
            ("kirmizi",    "Kırmızı",    "Red",         "#E53935"),
            ("pembe",      "Pembe",      "Pink",        "#EC407A"),
            ("turuncu",    "Turuncu",    "Orange",      "#FB8C00"),
            ("sari",       "Sarı",       "Yellow",      "#FDD835"),
            ("bej",        "Bej",        "Beige",       "#F5F0DC"),
            ("krem",       "Krem",       "Cream",       "#FFFDD0"),
            ("yesil",      "Yeşil",      "Green",       "#43A047"),
            ("acik_yesil", "Açık Yeşil", "Light Green", "#A5D6A7"),
            ("koyu_yesil", "Koyu Yeşil", "Dark Green",  "#1B5E20"),
            ("haki",       "Haki",       "Khaki",       "#8D7156"),
            ("mavi",       "Mavi",       "Blue",        "#1E88E5"),
            ("acik_mavi",  "Açık Mavi",  "Light Blue",  "#90CAF9"),
            ("koyu_mavi",  "Koyu Mavi",  "Dark Blue",   "#0D47A1"),
            ("lacivert",   "Lacivert",   "Navy",        "#1A237E"),
            ("turkuaz",    "Turkuaz",    "Turquoise",   "#00BCD4"),
            ("mor",        "Mor",        "Purple",      "#8E24AA"),
            ("lila",       "Lila",       "Lilac",       "#CE93D8"),
            ("kahve",      "Kahve",      "Brown",       "#6D4C41"),
            ("altin",      "Altın",      "Gold",        "#FFD600"),
            ("gumus",      "Gümüş",      "Silver",      "#B0BEC5"),
        };

        var existingNames = (await context.AttributeValues
            .Where(v => v.AttributeTypeId == attrType.Id)
            .Select(v => v.NameI18n)
            .ToListAsync())
            .Select(n => n.GetValueOrDefault("tr", "").ToUpperInvariant())
            .ToHashSet();

        int added = 0;
        foreach (var (_, tr, en, hex) in colors)
        {
            if (existingNames.Contains(tr.ToUpperInvariant())) continue;
            context.AttributeValues.Add(new AttributeValue
            {
                Id              = Guid.NewGuid(),
                AttributeTypeId = attrType.Id,
                NameI18n        = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                HexCode         = hex,
                SortOrder       = added * 10,
                IsActive        = true,
                CreatedAt       = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {added} filtre rengi değeri eklendi.");
        }
    }

    private static async Task SeedCinsiyetValuesAsync(CatalogDbContext db)
    {
        var attrType = await db.AttributeTypes.FirstOrDefaultAsync(a => a.Code == "cinsiyet");
        if (attrType is null) return;

        var values = new (string Tr, string En, int Sort)[]
        {
            ("Erkek",  "Male",   10),
            ("Kadın",  "Female", 20),
            ("Unisex", "Unisex", 30),
        };

        var existing = new HashSet<string>(
            await db.AttributeValues
                .Where(v => v.AttributeTypeId == attrType.Id)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());

        int added = 0;
        foreach (var (tr, en, sort) in values)
        {
            if (existing.Contains(tr)) continue;
            db.AttributeValues.Add(new AttributeValue
            {
                Id = Guid.NewGuid(), AttributeTypeId = attrType.Id,
                NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                SortOrder = sort, IsActive = true, CreatedAt = DateTime.UtcNow,
            });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Cinsiyet değerleri — {added} yeni eklendi.");
    }

    private static async Task SeedYasGrubuValuesAsync(CatalogDbContext db)
    {
        var attrType = await db.AttributeTypes.FirstOrDefaultAsync(a => a.Code == "yas_grubu");
        if (attrType is null) return;

        var values = new (string Tr, string En, int Sort)[]
        {
            ("Yeni Doğan", "Newborn", 10),
            ("Bebek",      "Baby",    20),
            ("Çocuk",      "Kids",    30),
            ("Genç",       "Teen",    40),
            ("Yetişkin",   "Adult",   50),
        };

        var existing = new HashSet<string>(
            await db.AttributeValues
                .Where(v => v.AttributeTypeId == attrType.Id)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());

        int added = 0;
        foreach (var (tr, en, sort) in values)
        {
            if (existing.Contains(tr)) continue;
            db.AttributeValues.Add(new AttributeValue
            {
                Id = Guid.NewGuid(), AttributeTypeId = attrType.Id,
                NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                SortOrder = sort, IsActive = true, CreatedAt = DateTime.UtcNow,
            });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Yaş grubu değerleri — {added} yeni eklendi.");
    }

    private static async Task SeedVarYokValuesAsync(CatalogDbContext db, params string[] codes)
    {
        var attrTypes = await db.AttributeTypes
            .Where(a => codes.Contains(a.Code))
            .ToListAsync();

        int added = 0;
        foreach (var attrType in attrTypes)
        {
            var existing = new HashSet<string>(
                await db.AttributeValues
                    .Where(v => v.AttributeTypeId == attrType.Id)
                    .Select(v => v.NameI18n["tr"])
                    .ToListAsync());

            foreach (var (tr, en, sort) in new[] { ("Var", "Yes", 10), ("Yok", "No", 20) })
            {
                if (existing.Contains(tr)) continue;
                db.AttributeValues.Add(new AttributeValue
                {
                    Id = Guid.NewGuid(), AttributeTypeId = attrType.Id,
                    NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                    SortOrder = sort, IsActive = true, CreatedAt = DateTime.UtcNow,
                });
                added++;
            }
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Var/Yok değerleri — {added} yeni eklendi.");
    }

    /// <summary>
    /// G8: varsayılan vitrin — platformda hiç taslak blok ve yayın yoksa B6 geçici
    /// kompozisyonunun birebir karşılığını taslak bloklar olarak kurar ve v1 olarak
    /// yayınlar: duyuru şeridi (B3 metinleri) + kapsül kategori şeridi (kök kanal
    /// kategorileri; görsel = ilk ürün görseli, görselsüz kapsül basılmaz — B6 kuralı)
    /// + ilk 3 kök kategori için standart carousel (category kaynağı, 10 ürün).
    /// Böylece B6/B3 geçici kodları kaldırılınca canlı görünüm DEĞİŞMEZ; içerik artık
    /// admin Vitrin Yönetimi'nden yönetilir. Blok/yayın var olan platforma dokunulmaz.
    /// </summary>
    private static async Task SeedDefaultVitrinAsync(IServiceProvider sp)
    {
        var storefront = sp.GetRequiredService<StorefrontDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();
        var mediator = sp.GetRequiredService<MediatR.IMediator>();

        var platformlar = await core.FirmPlatforms.Where(fp => fp.IsActive).Select(fp => fp.Id).ToListAsync();
        foreach (var platformId in platformlar)
        {
            var dokunulmus = await storefront.PageBlocks.IgnoreQueryFilters().AnyAsync(b => b.FirmPlatformId == platformId)
                || await storefront.PublishedSnapshots.IgnoreQueryFilters().AnyAsync(x => x.FirmPlatformId == platformId);
            if (dokunulmus) continue;

            // Duyuru şeridi (B3 statik metinleri — artık admin'den yönetilir)
            var duyuru = new ECSPros.Storefront.Domain.Entities.PageBlock
            {
                FirmPlatformId = platformId, Placement = "global-top", BlockType = "announcement",
                TitleI18n = new() { ["tr"] = "Duyuru Şeridi" }, SortOrder = 1, IsActive = true,
            };
            var metinler = new[]
            {
                "Yeni sezon koleksiyonunu keşfet!",
                "Mishar'a özel fırsatlar seni bekliyor.",
                "Sepette avantajlı ürünleri kaçırma.",
            };
            for (var i = 0; i < metinler.Length; i++)
                duyuru.Items.Add(new ECSPros.Storefront.Domain.Entities.PageBlockItem
                {
                    TitleI18n = new() { ["tr"] = metinler[i] }, SortOrder = i + 1, IsActive = true,
                });
            storefront.PageBlocks.Add(duyuru);

            // Kök kategoriler (aktif, sıralı)
            var kokler = await storefront.ChannelCategories
                .Where(c => c.FirmPlatformId == platformId && c.ParentId == null && c.Status == "published")
                .OrderBy(c => c.SortOrder)
                .Select(c => new { c.Id, c.NameI18n, c.Slug, c.DisplayImageUrl })
                .ToListAsync();

            // Kapsül görseli: kategorinin kendi görseli yoksa ilk ürün görseli (B6 kuralı)
            var kapsulOgeleri = new List<ECSPros.Storefront.Domain.Entities.PageBlockItem>();
            var carouselKokleri = new List<(Guid Id, string Ad, string Slug)>();
            foreach (var kok in kokler)
            {
                var ad = kok.NameI18n.TryGetValue("tr", out var tr) ? tr : kok.NameI18n.Values.FirstOrDefault() ?? kok.Slug;
                var urunler = await mediator.Send(
                    new ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQuery(kok.Id, 1, 1));
                var ilkUrunGorseli = urunler.IsSuccess ? urunler.Value!.Items.FirstOrDefault()?.MainImageUrl : null;
                if (urunler.IsSuccess && urunler.Value!.Items.Any() && carouselKokleri.Count < 3)
                    carouselKokleri.Add((kok.Id, ad, kok.Slug));

                var gorsel = kok.DisplayImageUrl ?? ilkUrunGorseli;
                if (gorsel is null) continue;
                kapsulOgeleri.Add(new ECSPros.Storefront.Domain.Entities.PageBlockItem
                {
                    TitleI18n = new() { ["tr"] = ad }, ImageUrl = gorsel, LinkUrl = "/" + kok.Slug,
                    SortOrder = kapsulOgeleri.Count + 1, IsActive = true,
                });
            }

            var sira = 0;
            if (kapsulOgeleri.Count >= 2) // B6: kapsül şeridi yalnız >=2 kategoriyle basılırdı
            {
                var kapsul = new ECSPros.Storefront.Domain.Entities.PageBlock
                {
                    FirmPlatformId = platformId, Placement = "homepage", BlockType = "categories",
                    TitleI18n = new() { ["tr"] = "Öne Çıkan Kategoriler" }, SortOrder = ++sira, IsActive = true,
                    ConfigJson = "{\"gorunum\":\"kapsul\",\"mobileCarousel\":true}",
                };
                foreach (var oge in kapsulOgeleri) kapsul.Items.Add(oge);
                storefront.PageBlocks.Add(kapsul);
            }

            foreach (var (kokId, ad, slug) in carouselKokleri)
            {
                storefront.PageBlocks.Add(new ECSPros.Storefront.Domain.Entities.PageBlock
                {
                    FirmPlatformId = platformId, Placement = "homepage", BlockType = "carousel", Template = "standart",
                    TitleI18n = new() { ["tr"] = ad },
                    SubtitleI18n = new() { ["tr"] = ad + " kategorisinden öne çıkan ürünler." },
                    SortOrder = ++sira, IsActive = true,
                    ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        productSource = new { source = "category", categoryId = kokId, limit = 10 },
                        tema = "varsayilan",
                        seeAllUrl = "/" + slug,
                    }),
                });
            }

            await storefront.SaveChangesAsync();
            var yayin = await mediator.Send(
                new ECSPros.Storefront.Application.Commands.PublishPageSnapshot.PublishPageSnapshotCommand(
                    platformId, null, "G8 varsayılan vitrin (B6 kompozisyonu)"));
            Console.WriteLine(yayin.IsSuccess
                ? $"✓ Seed: {platformId} için varsayılan vitrin yayınlandı (v{yayin.Value})."
                : $"⚠ Seed: {platformId} vitrin yayını başarısız: {yayin.Error}");
        }
    }
}
