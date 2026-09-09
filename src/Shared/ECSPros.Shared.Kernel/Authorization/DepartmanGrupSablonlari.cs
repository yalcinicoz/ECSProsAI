namespace ECSPros.Shared.Kernel.Authorization;

/// <summary>
/// Bir departman grubunun KOD tarafındaki başlangıç tanımı (Y8 / karar K8 adım 3).
/// Şablon yalnız BAŞLANGIÇ noktasıdır: panelden grup kurulduktan sonra yetkileri
/// serbestçe düzenlenir, şablon bir daha o gruba dokunmaz (senkron YOK).
/// </summary>
/// <param name="Kod">Kurulacak grubun kodu; aynı kodlu grup varsa şablon "kurulu" sayılır.</param>
/// <param name="Yetkiler">Grubun açılışta alacağı yetki key'leri (katalogda olmayan key atlanır).</param>
public sealed record DepartmanGrupSablonu(
    string Kod,
    string Ad,
    string Aciklama,
    IReadOnlyList<string> Yetkiler);

/// <summary>
/// K8 adım 3 ("gerçek departman gruplarını oluştur") için hazır şablonlar.
///
/// NEDEN ŞABLON: geçiş grubundan çıkış, her firmanın kendi departman yapısını sıfırdan
/// tıklamasını gerektiriyordu; 73 yetkilik katalogda bu hem yorucu hem de hataya açıktır
/// (fazla yetki vermek default-deny'ın kazancını siler). Şablonlar "makul asgari"yi tek
/// tıkla kurar, panel üstünde daraltır/genişletir.
///
/// KURAL: şablonlar hiçbir `iam.*`, `definition.manage`, `system.migration.manage` ya da
/// `integration.credentials.reveal` yetkisi İÇERMEZ — bunlar süper adminde kalır (Y2 kararı).
/// </summary>
public static class DepartmanGrupSablonlari
{
    public static readonly IReadOnlyList<DepartmanGrupSablonu> Tumu =
    [
        new("yonetim", "Yönetim",
            "Tüm ekranları görür, sipariş/kampanya/fiyat kararlarını yürütür. Kullanıcı ve yetki yönetimi DAHİL DEĞİLDİR (o yalnız süper adminde).",
            [
                Permissions.PanelDashboardView,
                Permissions.OrdersView, Permissions.OrdersManage,
                Permissions.OrdersReturnsView, Permissions.OrdersReturnsManage,
                Permissions.OrdersInvoicesView, Permissions.OrdersInvoicesManage,
                Permissions.OrdersQuotesView, Permissions.OrdersQuotesManage,
                Permissions.PosView, Permissions.PosManage,
                Permissions.CatalogProductsView, Permissions.CatalogProductsManage,
                Permissions.InventoryView, Permissions.ProcurementView,
                Permissions.CrmMembersView, Permissions.CrmTicketsView, Permissions.CrmTicketsManage,
                Permissions.PromotionView, Permissions.PromotionManage,
                Permissions.FinanceView, Permissions.AccountsView,
                Permissions.CommissionView, Permissions.CommissionManage,
                Permissions.FulfillmentView,
                Permissions.StorefrontChannelsView, Permissions.MarketplacesView,
                Permissions.StorefrontContentView, Permissions.CmsView,
                Permissions.MarketingTrackingView,
                Permissions.RequestsView, Permissions.RequestsManage,
                Permissions.DefinitionsView,
                Permissions.FieldViewCost, Permissions.FieldViewMargin,
                Permissions.FieldViewInternalNotes,
                Permissions.FieldViewCustomerPhone, Permissions.FieldViewCustomerAddress,
            ]),

        new("musteri_iliskileri", "Müşteri İlişkileri",
            "Sipariş takibi, iade/talep yönetimi, üye ve yorum/soru moderasyonu. Müşteri telefonu ve adresini görür; maliyet/kâr GÖRMEZ.",
            [
                Permissions.PanelDashboardView,
                Permissions.OrdersView, Permissions.OrdersManage,
                Permissions.OrdersReturnsView, Permissions.OrdersReturnsManage,
                Permissions.OrdersInvoicesView,
                Permissions.CrmMembersView, Permissions.CrmMembersManage,
                Permissions.CrmTicketsView, Permissions.CrmTicketsManage,
                Permissions.StorefrontModerationView, Permissions.StorefrontModerationManage,
                Permissions.CatalogProductsView,
                Permissions.InventoryView,
                Permissions.RequestsView,
                Permissions.FieldViewCustomerPhone, Permissions.FieldViewCustomerAddress,
                Permissions.FieldViewInternalNotes,
            ]),

        new("depo_operasyon", "Depo & Operasyon",
            "Toplama/paketleme/kargo operasyonu, sayım ve yerleştirme. Sipariş içeriğini ve teslimat adresini görür; maliyet/kâr ve fiyat kararları DIŞARIDADIR.",
            [
                Permissions.PanelDashboardView,
                Permissions.FulfillmentView, Permissions.FulfillmentManage,
                Permissions.OrdersView,
                Permissions.OrdersReturnsView,
                Permissions.InventoryView, Permissions.InventoryManage,
                Permissions.ProcurementView, Permissions.ProcurementSort,
                Permissions.CatalogProductsView,
                Permissions.FieldViewCustomerPhone, Permissions.FieldViewCustomerAddress,
            ]),

        new("satin_alma", "Satın Alma",
            "Tedarik siparişi, mal kabul ve stok girişi. Alış fiyatı/maliyet görür; müşteri iletişim bilgisi GÖRMEZ.",
            [
                Permissions.PanelDashboardView,
                Permissions.ProcurementView, Permissions.ProcurementManage, Permissions.ProcurementSort,
                Permissions.InventoryView, Permissions.InventoryManage,
                Permissions.CatalogProductsView,
                Permissions.AccountsView,
                Permissions.FinanceView,
                Permissions.FieldViewCost, Permissions.FieldViewMargin,
            ]),

        new("muhasebe_finans", "Muhasebe & Finans",
            "Cari hesaplar, faturalar, tedarikçi faturaları ve komisyon. Maliyet/kâr görür; katalog ve operasyon düzenlemez.",
            [
                Permissions.PanelDashboardView,
                Permissions.FinanceView, Permissions.FinanceManage,
                Permissions.AccountsView, Permissions.AccountsManage,
                Permissions.CommissionView, Permissions.CommissionManage,
                Permissions.OrdersView,
                Permissions.OrdersInvoicesView, Permissions.OrdersInvoicesManage,
                Permissions.OrdersReturnsView,
                Permissions.PosView,
                Permissions.CatalogProductsView,
                Permissions.FieldViewCost, Permissions.FieldViewMargin,
                Permissions.FieldViewCustomerAddress,
            ]),

        new("icerik_pazarlama", "İçerik & Pazarlama",
            "Ürün içeriği, vitrin, kampanya ve bildirim yönetimi. Sipariş ve müşteri kişisel verisi DIŞARIDADIR.",
            [
                Permissions.PanelDashboardView,
                Permissions.CatalogProductsView, Permissions.CatalogProductsManage,
                Permissions.CatalogCategoriesManage, Permissions.CatalogImagesManage,
                Permissions.CmsView, Permissions.CmsManage,
                Permissions.StorefrontContentView, Permissions.StorefrontContentManage,
                Permissions.StorefrontChannelsView,
                Permissions.StorefrontNotificationsView, Permissions.StorefrontNotificationsManage,
                Permissions.StorefrontModerationView, Permissions.StorefrontModerationManage,
                Permissions.PromotionView, Permissions.PromotionManage,
                Permissions.MarketingTrackingView, Permissions.MarketingTrackingManage,
                Permissions.MarketplacesView,
                Permissions.InventoryView,
                Permissions.DefinitionsView,
            ]),

        new("raporlama", "Salt Okunur (Raporlama)",
            "Yalnız görüntüleme: hiçbir kayıt değiştiremez. Hassas alanlar (telefon/adres/maliyet/kâr) KAPALIDIR — gerekirse kullanıcı istisnasıyla tek tek açılır.",
            [
                Permissions.PanelDashboardView,
                Permissions.OrdersView, Permissions.OrdersReturnsView, Permissions.OrdersInvoicesView,
                Permissions.OrdersQuotesView, Permissions.PosView,
                Permissions.CatalogProductsView, Permissions.InventoryView, Permissions.ProcurementView,
                Permissions.CrmMembersView, Permissions.CrmTicketsView,
                Permissions.PromotionView, Permissions.FinanceView, Permissions.AccountsView,
                Permissions.CommissionView, Permissions.FulfillmentView,
                Permissions.StorefrontChannelsView, Permissions.StorefrontContentView,
                Permissions.CmsView, Permissions.MarketplacesView, Permissions.MarketingTrackingView,
                Permissions.RequestsView, Permissions.DefinitionsView,
            ]),
    ];

    public static DepartmanGrupSablonu? Bul(string kod)
        => Tumu.FirstOrDefault(s => s.Kod == kod);
}
