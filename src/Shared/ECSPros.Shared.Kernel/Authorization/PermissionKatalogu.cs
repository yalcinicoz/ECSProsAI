namespace ECSPros.Shared.Kernel.Authorization;

/// <summary>Yetki türü (tasarım §B.2): sayfa erişimi / sayfa içi işlev / hassas alan görünürlüğü.</summary>
public enum PermissionTuru
{
    /// <summary>Ekrana erişim — menüde görünürlük + sayfanın veri uçları.</summary>
    Sayfa = 0,
    /// <summary>Sayfa içindeki gerçek iş (iptal et, adres değiştir…).</summary>
    Aksiyon = 1,
    /// <summary>Hassas alanı GÖRME — veri backend'de üretilmez, export kolonu da düşer.</summary>
    Alan = 2,
}

/// <summary>
/// Tek bir yetkinin KOD tarafındaki tanımı (tasarım §B.1, karar K4 "kod sahipli katalog").
/// Kod sahipli alanlar: <see cref="Key"/> (değişmez), <see cref="Tur"/>, <see cref="Modul"/>,
/// <see cref="Sayfa"/>, <see cref="KanalKapsamli"/>. Panel bunların üstüne yalnız GÖSTERİM
/// (ad/açıklama/sıra/aktiflik) ve gerekiyorsa kanal-kapsamlı bayrağını yazar.
/// </summary>
/// <param name="Key">Değişmez teknik anahtar (örn. "orders.cancel"). ASLA değişmez; gerekirse
/// yeni key açılır, eskisi pasife alınır (tasarım §M.2).</param>
/// <param name="KanalKapsamli">Yetki satış kanalı bazında mı verilir? false ise panelde kanal
/// seçici hiç görünmez ve kapsam yok sayılır (tasarım §B.3).</param>
public sealed record PermissionTanimi(
    string Key,
    PermissionTuru Tur,
    string Modul,
    string? Sayfa,
    bool KanalKapsamli,
    string Ad,
    string? Aciklama = null,
    int Sira = 0);

/// <summary>
/// Kod sahipli permission kataloğu (K4). Uygulama açılışında DB'ye senkronlanır
/// (<c>PermissionKatalogSenkronu</c>): katalogda olmayan kayıt pasife alınır, panelde
/// "uygulamada karşılığı yok" olarak görünür; katalogdaki yeni kayıt eklenir ama
/// KİMSEYE otomatik atanmaz (default deny, tasarım §M.2).
///
/// Kural: buraya eklenen her key'in kodda gerçek bir zorlama noktası olmalıdır
/// (uç kaplaması Y2, alan yetkileri Y6). Karşılığı olmayan key "hayalet yetki"dir.
/// </summary>
public static class PermissionKatalogu
{
    public static readonly IReadOnlyList<PermissionTanimi> Tumu =
    [
        // ── Platform yönetimi (geliştirici firma) ────────────────────────────────
        new(Permissions.CatalogPlatformManage, PermissionTuru.Aksiyon, "Katalog", "Ürün Grupları", false,
            "Ürün Grubu ve Özellik Yönetimi",
            "Özellik tipleri, değerleri, ürün grupları ve grup konfigürasyonu.", 10),
        new(Permissions.DefinitionManage, PermissionTuru.Aksiyon, "Sistem", "Tanımlar", false,
            "Tanım Şeması Yönetimi",
            "Servis kataloğu vb. definition şeması kayıtları — yalnız platform yönetimi.", 20),

        // ── Katalog ──────────────────────────────────────────────────────────────
        new(Permissions.CatalogProductsManage, PermissionTuru.Aksiyon, "Katalog", "Ürünler", false,
            "Ürün Yönetimi", "Ürün ve varyant oluşturma/güncelleme.", 30),
        new(Permissions.CatalogCategoriesManage, PermissionTuru.Aksiyon, "Katalog", "Kategoriler", false,
            "Kategori Yönetimi", null, 40),
        new(Permissions.CatalogImagesManage, PermissionTuru.Aksiyon, "Katalog", "Ürünler", false,
            "Ürün Görseli Yönetimi", null, 50),
        new(Permissions.CatalogSettingsManage, PermissionTuru.Aksiyon, "Katalog", "Katalog Ayarları", false,
            "Katalog Ayarları", null, 60),

        // ── Stok / tedarik ───────────────────────────────────────────────────────
        new(Permissions.InventoryManage, PermissionTuru.Aksiyon, "Stok", "Stoklar", false,
            "Stok ve Depo Yönetimi", "Depo, stok ve transfer işlemleri.", 70),
        new(Permissions.InventoryCountApply, PermissionTuru.Aksiyon, "Stok", "Raf İşlemleri", false,
            "Raf Sayımı Farkını Uygula", "Bitirilmiş raf sayımının farkını stoğa yazar (sayan personelden ayrı, depo sorumlusu).", 71),
        new(Permissions.ProcurementManage, PermissionTuru.Aksiyon, "Tedarik", "Satın Almalar", false,
            "Tedarik Yönetimi", "Satın alma ve mal kabul.", 80),
        new(Permissions.ProcurementSort, PermissionTuru.Aksiyon, "Tedarik", "Sayım / Teslim", false,
            "Ayrıştırma ve Yerleştirme", "Depo personeli sayım/teslim işlemleri.", 90),

        // ── Sipariş (kanal kapsamlı) ─────────────────────────────────────────────
        new(Permissions.OrderPackagesMerge, PermissionTuru.Aksiyon, "Sipariş", "Paketler", true,
            "Paket Birleştirme (tek fatura)",
            "Normal akış paket başına faturadır; bu izin bilinçli istisna içindir.", 100),

        // ── Entegrasyon ──────────────────────────────────────────────────────────
        new(Permissions.IntegrationCredentialsReveal, PermissionTuru.Aksiyon, "Sistem", "Entegrasyonlar", false,
            "Kimlik Bilgisini Görüntüle",
            "API anahtarlarını açık metin olarak görme; her görüntüleme loglanır.", 110),

        // ── Yetkilendirme yönetimi (yeni — Y4 ekranları bunlarla korunur) ────────
        new(Permissions.IamPermissionsManage, PermissionTuru.Sayfa, "Yetkilendirme", "Yetki Grupları", false,
            "Yetkilendirme Yönetimi",
            "Yetki grupları, kullanıcı yetkileri ve yetki içerikleri ekranları. " +
            "Bu yetkiye sahip kullanıcı YALNIZ kendi sahip olduğu yetkileri dağıtabilir.", 200),
        new(Permissions.IamPermissionsSimulate, PermissionTuru.Aksiyon, "Yetkilendirme", "Kullanıcı Yetkileri", false,
            "Kullanıcı Simülasyonu",
            "\"Bu kullanıcı panelde ne görüyor?\" önizlemesi; salt okunur ve loglanır.", 210),
        new(Permissions.IamAuditView, PermissionTuru.Sayfa, "Yetkilendirme", "Yetki Logları", false,
            "Yetki Loglarını Görüntüle", null, 220),

        // ── Panel sayfa/aksiyon yetkileri (Y2) ──────────────────────────────────
        // Kaynak: panel sayfa envanteri (admin/src/components/layout/sidebarNavigation.ts).
        new(Permissions.PanelDashboardView, PermissionTuru.Sayfa, "Genel", "Dashboard", false,
            "Dashboard", "Özet metrikler ve günlük durum ekranı.", 410),
        new(Permissions.RequestsView, PermissionTuru.Sayfa, "Genel", "Proje Talepleri", false,
            "Proje Taleplerini Görüntüle", null, 420),
        new(Permissions.RequestsManage, PermissionTuru.Aksiyon, "Genel", "Proje Talepleri", false,
            "Proje Talebi Yönet", "Talep açma, durum değiştirme, atama.", 430),
        new(Permissions.CatalogProductsView, PermissionTuru.Sayfa, "Katalog", "Ürün Kartları", false,
            "Ürün Kartlarını Görüntüle", null, 440),
        new(Permissions.StorefrontChannelsView, PermissionTuru.Sayfa, "Satış Kanalları", "Kanal Ürünleri", true,
            "Kanal Ürün/Kategori Görüntüle", "Kanal ürünleri, kanal kategorileri, menü yerleşimi, kanal kapsamı.", 450),
        new(Permissions.StorefrontChannelsManage, PermissionTuru.Aksiyon, "Satış Kanalları", "Kanal Ürünleri", true,
            "Kanal Ürün/Kategori Yönet", null, 460),
        new(Permissions.MarketplacesView, PermissionTuru.Sayfa, "Satış Kanalları", "Pazaryerleri", true,
            "Pazaryerlerini Görüntüle", null, 470),
        new(Permissions.MarketplacesManage, PermissionTuru.Aksiyon, "Satış Kanalları", "Pazaryerleri", true,
            "Pazaryeri Yönet", "Mağaza tanımı, eşleme, gönderim.", 480),
        new(Permissions.OrdersView, PermissionTuru.Sayfa, "Sipariş", "Siparişler", true,
            "Siparişleri Görüntüle", null, 490),
        new(Permissions.OrdersManage, PermissionTuru.Aksiyon, "Sipariş", "Siparişler", true,
            "Sipariş İşlemleri", "Onay, iptal, kargoya verme, adres/kargo değişikliği.", 500),
        new(Permissions.OrdersReturnsView, PermissionTuru.Sayfa, "Sipariş", "İadeler", true,
            "İadeleri Görüntüle", null, 510),
        new(Permissions.OrdersReturnsManage, PermissionTuru.Aksiyon, "Sipariş", "İadeler", true,
            "İade İşlemleri", "Onay/ret, teslim alma, geri ödeme.", 520),
        new(Permissions.OrdersInvoicesView, PermissionTuru.Sayfa, "Sipariş", "Faturalar", true,
            "Faturaları Görüntüle", null, 530),
        new(Permissions.OrdersInvoicesManage, PermissionTuru.Aksiyon, "Sipariş", "Faturalar", true,
            "Fatura İşlemleri", "Fatura oluşturma/iptal, gönderim.", 540),
        new(Permissions.OrdersQuotesView, PermissionTuru.Sayfa, "Sipariş", "Teklifler", true,
            "Teklifleri Görüntüle", null, 550),
        new(Permissions.OrdersQuotesManage, PermissionTuru.Aksiyon, "Sipariş", "Teklifler", true,
            "Teklif İşlemleri", null, 560),
        new(Permissions.PosView, PermissionTuru.Sayfa, "Sipariş", "POS", true,
            "POS Satışlarını Görüntüle", null, 570),
        new(Permissions.PosManage, PermissionTuru.Aksiyon, "Sipariş", "POS", true,
            "POS İşlemleri", "Kasa oturumu, satış, iade.", 580),
        new(Permissions.FulfillmentView, PermissionTuru.Sayfa, "Depo & Operasyon", "Toplama Planlama", true,
            "Operasyonu Görüntüle", "Toplama planları, masalar, paketleme.", 590),
        new(Permissions.FulfillmentManage, PermissionTuru.Aksiyon, "Depo & Operasyon", "Toplama Planlama", true,
            "Operasyon İşlemleri", "Plan oluşturma, toplama, paketleme, kargo yönlendirme.", 600),
        new(Permissions.InventoryView, PermissionTuru.Sayfa, "Stok", "Stok Takibi", false,
            "Stokları Görüntüle", null, 610),
        new(Permissions.ProcurementView, PermissionTuru.Sayfa, "Tedarik", "Satın Almalar", false,
            "Tedariki Görüntüle", null, 620),
        new(Permissions.CrmMembersView, PermissionTuru.Sayfa, "Müşteriler", "Üyeler", false,
            "Üyeleri Görüntüle", null, 630),
        new(Permissions.CrmMembersManage, PermissionTuru.Aksiyon, "Müşteriler", "Üyeler", false,
            "Üye Yönet", "Üye oluşturma/güncelleme, grup, cüzdan/puan işlemleri.", 640),
        new(Permissions.CrmTicketsView, PermissionTuru.Sayfa, "Müşteriler", "Müşteri İlişkileri", false,
            "Müşteri Taleplerini Görüntüle", null, 650),
        new(Permissions.CrmTicketsManage, PermissionTuru.Aksiyon, "Müşteriler", "Müşteri İlişkileri", false,
            "Müşteri Talebi Yönet", null, 660),
        new(Permissions.StorefrontModerationView, PermissionTuru.Sayfa, "Müşteriler", "Moderasyon", true,
            "Mesaj/Soru/Yorum Görüntüle", "İletişim mesajları, ürün soruları, yorumlar.", 670),
        new(Permissions.StorefrontModerationManage, PermissionTuru.Aksiyon, "Müşteriler", "Moderasyon", true,
            "Mesaj/Soru/Yorum Yönet", "Cevaplama, onay/ret.", 680),
        new(Permissions.AccountsView, PermissionTuru.Sayfa, "Cari & Finans", "Cari Kartlar", false,
            "Cari Kartları Görüntüle", null, 690),
        new(Permissions.AccountsManage, PermissionTuru.Aksiyon, "Cari & Finans", "Cari Kartlar", false,
            "Cari Kart Yönet", "Cari açma/güncelleme, hareket girişi.", 700),
        new(Permissions.CommissionView, PermissionTuru.Sayfa, "Cari & Finans", "Komisyon Yönetimi", false,
            "Komisyonları Görüntüle", null, 710),
        new(Permissions.CommissionManage, PermissionTuru.Aksiyon, "Cari & Finans", "Komisyon Yönetimi", false,
            "Komisyon Yönet", null, 720),
        new(Permissions.FinanceView, PermissionTuru.Sayfa, "Cari & Finans", "Finans", false,
            "Finansı Görüntüle", "Tedarikçi faturaları.", 730),
        new(Permissions.FinanceManage, PermissionTuru.Aksiyon, "Cari & Finans", "Finans", false,
            "Finans İşlemleri", null, 740),
        new(Permissions.PromotionView, PermissionTuru.Sayfa, "Pazarlama", "Kampanyalar", true,
            "Kampanya/Kupon Görüntüle", null, 750),
        new(Permissions.PromotionManage, PermissionTuru.Aksiyon, "Pazarlama", "Kampanyalar", true,
            "Kampanya/Kupon Yönet", "Kampanya, kupon, hediye kartı.", 760),
        new(Permissions.StorefrontNotificationsView, PermissionTuru.Sayfa, "Pazarlama", "Bildirimler", true,
            "Bildirim/Bülten Görüntüle", null, 770),
        new(Permissions.StorefrontNotificationsManage, PermissionTuru.Aksiyon, "Pazarlama", "Bildirimler", true,
            "Bildirim/Bülten Yönet", "Push gönderimi, şablon, abone yönetimi.", 780),
        new(Permissions.MarketingTrackingView, PermissionTuru.Sayfa, "Pazarlama", "Takip & Reklam", true,
            "Takip/Reklam Görüntüle", null, 790),
        new(Permissions.MarketingTrackingManage, PermissionTuru.Aksiyon, "Pazarlama", "Takip & Reklam", true,
            "Takip/Reklam Yönet", null, 800),
        new(Permissions.StorefrontContentView, PermissionTuru.Sayfa, "Vitrin & İçerik", "Vitrin Yönetimi", true,
            "Vitrin İçeriğini Görüntüle", "Vitrin blokları, ürün kartı ayarı, çerez metni.", 810),
        new(Permissions.StorefrontContentManage, PermissionTuru.Aksiyon, "Vitrin & İçerik", "Vitrin Yönetimi", true,
            "Vitrin İçeriğini Yönet", null, 820),
        new(Permissions.CmsView, PermissionTuru.Sayfa, "Vitrin & İçerik", "Sayfalar", true,
            "CMS Sayfalarını Görüntüle", null, 830),
        new(Permissions.CmsManage, PermissionTuru.Aksiyon, "Vitrin & İçerik", "Sayfalar", true,
            "CMS Sayfası Yönet", null, 840),
        new(Permissions.DefinitionsView, PermissionTuru.Sayfa, "Tanımlar", "Tanımlar", false,
            "Tanımları Görüntüle", "Depolar, gruplar, seriler, kargo bölgeleri, şablonlar, lookup.", 850),
        new(Permissions.DefinitionsManage, PermissionTuru.Aksiyon, "Tanımlar", "Tanımlar", false,
            "Tanım Yönet", null, 860),
        new(Permissions.SystemFirmsView, PermissionTuru.Sayfa, "Sistem", "Firmalar", false,
            "Firma/Kanal Tanımlarını Görüntüle", null, 870),
        new(Permissions.SystemFirmsManage, PermissionTuru.Aksiyon, "Sistem", "Firmalar", false,
            "Firma/Kanal Tanımı Yönet", "Firma, satış kanalı, platform tipi.", 880),
        new(Permissions.SystemTranslationsManage, PermissionTuru.Aksiyon, "Sistem", "Çeviriler", false,
            "Çeviri Yönet", null, 890),
        new(Permissions.SystemMigrationManage, PermissionTuru.Aksiyon, "Sistem", "Migration", false,
            "Veri Aktarımı Çalıştır", "Eski sistemden aktarım işleri — yüksek etkili.", 900),
        new(Permissions.SystemIntegrationsView, PermissionTuru.Sayfa, "Sistem", "Entegrasyon Logları", false,
            "Entegrasyon Loglarını Görüntüle", null, 910),
        new(Permissions.SystemIntegrationsManage, PermissionTuru.Aksiyon, "Sistem", "Entegrasyon Logları", false,
            "Entegrasyon Yönet", "Yeniden deneme, kuyruk işlemleri.", 920),
        new(Permissions.IamUsersView, PermissionTuru.Sayfa, "Yetkilendirme", "Kullanıcılar", false,
            "Kullanıcıları Görüntüle", null, 930),
        new(Permissions.IamUsersManage, PermissionTuru.Aksiyon, "Yetkilendirme", "Kullanıcılar", false,
            "Kullanıcı Yönet", "Kullanıcı açma/güncelleme, şifre sıfırlama, oturum sonlandırma.", 940),

        // ── Hassas alanlar (K6 — v1'de bu beş alanla SINIRLI) ───────────────────
        new(Permissions.FieldViewCost, PermissionTuru.Alan, "Hassas Alanlar", null, false,
            "Maliyet Gör", "Ürün/sipariş maliyet tutarları — liste, detay ve export.", 300),
        new(Permissions.FieldViewMargin, PermissionTuru.Alan, "Hassas Alanlar", null, false,
            "Kâr / Kâr Oranı Gör", null, 310),
        new(Permissions.FieldViewCustomerPhone, PermissionTuru.Alan, "Hassas Alanlar", null, false,
            "Müşteri Telefonunu Gör", null, 320),
        new(Permissions.FieldViewCustomerAddress, PermissionTuru.Alan, "Hassas Alanlar", null, false,
            "Müşteri Adresini Gör", null, 330),
        new(Permissions.FieldViewInternalNotes, PermissionTuru.Alan, "Hassas Alanlar", null, false,
            "Personel/Özel Notları Gör", null, 340),
    ];

    private static readonly Dictionary<string, PermissionTanimi> Index =
        Tumu.ToDictionary(p => p.Key, StringComparer.Ordinal);

    public static PermissionTanimi? Bul(string key) =>
        key is not null && Index.TryGetValue(key, out var t) ? t : null;

    public static bool Var(string key) => key is not null && Index.ContainsKey(key);

    /// <summary>Kanal kapsamlı yetkiler — kapsam hesabı yalnız bunlarda kanal kümesi taşır.</summary>
    public static IEnumerable<PermissionTanimi> KanalKapsamlilar() => Tumu.Where(p => p.KanalKapsamli);

    /// <summary>K6 alan yetkileri — DTO/export kısıtı bu kümeden beslenir.</summary>
    public static IEnumerable<PermissionTanimi> AlanYetkileri() => Tumu.Where(p => p.Tur == PermissionTuru.Alan);
}
