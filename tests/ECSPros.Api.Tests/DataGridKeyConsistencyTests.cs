using System.Text.RegularExpressions;

namespace ECSPros.Api.Tests;

/// <summary>
/// DataGrid istemci↔sunucu ANAHTAR DENETİMİ (2026-09-09).
///
/// Neden: panelde bir kolona <c>sortable: true</c> yazmak yetmez — sunucu şemasında aynı anahtarla
/// <c>.Sort("anahtar")</c> bildirimi de olmalı. Yoksa kullanıcı o başlığa bastığında liste
/// "Geçersiz sıralama alanı: …" ile 400 döner (2026-09-08'de products/newest, 2026-09-09'da
/// yorum moderasyonu "text" kolonunda tam bu oldu — bu test onu yakaladı).
///
/// Aynı şekilde başlık filtrelerinin alan adları (<c>filter.field ?? key</c> ve <c>filters[].field</c>)
/// şemada filtrelenebilir alan olarak bildirilmiş olmalı.
///
/// Yeni bir DataGrid sayfası eklenince <see cref="Eslesme"/>'ye satır eklenmezse test PATLAR —
/// böylece denetim kapsam dışına kaçamaz.
/// </summary>
[TestClass]
public sealed class DataGridKeyConsistencyTests
{
    /// <summary>
    /// admin sayfası (repo köküne göre) → sunucu grid şeması dosya(ları).
    /// ⚠ Bir sayfada BİRDEN FAZLA grid olabilir (ör. Bildirim İzleme: stok alarmları + kayıtlı aramalar).
    /// O durumda anahtarlar şemaların BİRLEŞİMİNE karşı denetlenir: hangi kolonun hangi grid'e ait
    /// olduğunu kaynaktan güvenle ayırmak mümkün değil. Birleşim yine de "anahtar hiçbir şemada yok"
    /// hatasını (asıl 400 sebebi) yakalar.
    /// </summary>
    private static readonly Dictionary<string, string[]> Eslesme = new()
    {
        ["admin/src/pages/storefront/NotificationsMonitorPage.tsx"] = new[]
        {
            "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetStockAlertsForAdmin/StockAlertGrid.cs",
            "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetSavedSearchesForAdmin/SavedSearchGrid.cs",
        },
        ["admin/src/pages/orders/OrdersPage.tsx"] = new[] { "src/Modules/Order/ECSPros.Order.Application/Queries/GetOrders/OrderGrid.cs" },
        ["admin/src/pages/orders/InvoicesPage.tsx"] = new[] { "src/Modules/Order/ECSPros.Order.Application/Queries/GetInvoices/InvoiceGrid.cs" },
        ["admin/src/pages/orders/ReturnsPage.tsx"] = new[] { "src/Modules/Order/ECSPros.Order.Application/Queries/GetReturns/ReturnGrid.cs" },
        ["admin/src/pages/orders/QuotesPage.tsx"] = new[] { "src/Modules/Order/ECSPros.Order.Application/Queries/GetQuotes/QuoteGrid.cs" },
        ["admin/src/pages/orders/GiftCardsPage.tsx"] = new[] { "src/Modules/Order/ECSPros.Order.Application/Queries/GetGiftCards/GiftCardGrid.cs" },
        ["admin/src/pages/accounts/AccountGroupsPage.tsx"] = new[] { "src/Modules/Accounts/ECSPros.Accounts.Application/Queries/GetAccountGroups/AccountGroupGrid.cs" },
        ["admin/src/pages/accounts/AccountsPage.tsx"] = new[] { "src/Modules/Accounts/ECSPros.Accounts.Application/Queries/GetCurrentAccounts/CurrentAccountGrid.cs" },
        ["admin/src/pages/catalog/ProductsPage.tsx"] = new[] { "src/Modules/Catalog/ECSPros.Catalog.Application/Queries/GetProducts/ProductGrid.cs" },
        ["admin/src/pages/catalog/AttributeTypesPage.tsx"] = new[] { "src/Modules/Catalog/ECSPros.Catalog.Application/Queries/GetAttributeTypes/AttributeTypeGrid.cs" },
        ["admin/src/pages/catalog/ProductSubmissionsPage.tsx"] = new[] { "src/Modules/Catalog/ECSPros.Catalog.Application/Queries/GetAdminProductSubmissions/ProductSubmissionGrid.cs" },
        ["admin/src/pages/catalog/ProductGroupsPage.tsx"] = new[] { "src/Modules/Catalog/ECSPros.Catalog.Application/Queries/GetProductGroups/ProductGroupGrid.cs" },
        ["admin/src/pages/crm/MembersPage.tsx"] = new[] { "src/Modules/Crm/ECSPros.Crm.Application/Queries/GetMembers/MemberGrid.cs" },
        ["admin/src/pages/cms/CmsPagesPage.tsx"] = new[] { "src/Modules/Cms/ECSPros.Cms.Application/Queries/GetPages/PageGrid.cs" },
        ["admin/src/pages/crm/MemberGroupsPage.tsx"] = new[] { "src/Modules/Crm/ECSPros.Crm.Application/Queries/GetMemberGroups/MemberGroupGrid.cs" },
        ["admin/src/pages/settings/PlatformTypesPage.tsx"] = new[] { "src/Modules/Core/ECSPros.Core.Application/Queries/GetPlatformTypes/PlatformTypeGrid.cs" },
        ["admin/src/pages/settings/IntegrationServicesPage.tsx"] = new[] { "src/Modules/Core/ECSPros.Core.Application/Queries/GetIntegrationServices/IntegrationServiceGrid.cs" },
        ["admin/src/pages/settings/FirmsPage.tsx"] = new[] { "src/Modules/Core/ECSPros.Core.Application/Queries/GetFirms/FirmGrid.cs" },
        ["admin/src/pages/crm/tickets/TicketsPage.tsx"] = new[] { "src/Modules/Crm/ECSPros.Crm.Application/Tickets/Queries/TicketGrid.cs" },
        ["admin/src/pages/inventory/StocksPage.tsx"] = new[] { "src/ECSPros.Api/Grid/StockGrid.cs" },
        ["admin/src/pages/inventory/WarehousesPage.tsx"] = new[] { "src/Modules/Inventory/ECSPros.Inventory.Application/Queries/GetWarehouses/WarehouseGrid.cs" },
        ["admin/src/pages/inventory/TransfersPage.tsx"] = new[] { "src/Modules/Inventory/ECSPros.Inventory.Application/Queries/GetTransfers/TransferGrid.cs" },
        ["admin/src/pages/finance/SupplierInvoicesPage.tsx"] = new[] { "src/Modules/Finance/ECSPros.Finance.Application/Queries/GetSupplierInvoices/SupplierInvoiceGrid.cs" },
        ["admin/src/pages/fulfillment/PickingPlansPage.tsx"] = new[] { "src/Modules/Fulfillment/ECSPros.Fulfillment.Application/Queries/GetPickingPlans/PickingPlanGrid.cs" },
        ["admin/src/pages/integrations/IntegrationLogsPage.tsx"] = new[] { "src/Modules/Integration/ECSPros.Integration.Application/Queries/GetIntegrationLogs/IntegrationLogGrid.cs" },
        ["admin/src/pages/marketing/TrackingPage.tsx"] = new[] { "src/Modules/Integration/ECSPros.Integration.Application/Queries/GetIntegrationLogs/TrackingOutboxGrid.cs" },
        ["admin/src/pages/pos/PosSalesPage.tsx"] = new[] { "src/Modules/Pos/ECSPros.Pos.Application/Queries/GetPosSales/PosSaleGrid.cs" },
        ["admin/src/pages/procurement/ReceiptsPage.tsx"] = new[] { "src/Modules/Procurement/ECSPros.Procurement.Application/Queries/GetReceiptBatches/ReceiptBatchGrid.cs" },
        ["admin/src/pages/procurement/PurchaseOrdersPage.tsx"] = new[] { "src/Modules/Procurement/ECSPros.Procurement.Application/Queries/GetPurchaseOrders/PurchaseOrderGrid.cs" },
        ["admin/src/pages/promotion/CampaignsPage.tsx"] = new[] { "src/Modules/Promotion/ECSPros.Promotion.Application/Queries/GetCampaigns/CampaignGrid.cs" },
        ["admin/src/pages/promotion/CampaignTypesPage.tsx"] = new[] { "src/Modules/Promotion/ECSPros.Promotion.Application/Queries/GetCampaignTypes/CampaignTypeGrid.cs" },
        ["admin/src/pages/promotion/CouponsPage.tsx"] = new[] { "src/Modules/Promotion/ECSPros.Promotion.Application/Queries/GetCoupons/CouponGrid.cs" },
        ["admin/src/pages/requests/RequestsPage.tsx"] = new[] { "src/Modules/Requests/ECSPros.Requests.Application/Queries/GetRequests/RequestGrid.cs" },
        ["admin/src/pages/settings/PermissionCatalogPage.tsx"] = new[] { "src/Modules/Iam/ECSPros.Iam.Application/Yetkilendirme/YetkiKatalogGrid.cs" },
        ["admin/src/pages/settings/PermissionGroupsPage.tsx"] = new[] { "src/Modules/Iam/ECSPros.Iam.Application/Yetkilendirme/YetkiGrubuGrid.cs" },
        ["admin/src/pages/orders/InvoiceSeriesPage.tsx"] = new[] { "src/Modules/Order/ECSPros.Order.Application/Queries/GetInvoiceSeries/InvoiceSeriesGrid.cs" },
        ["admin/src/pages/catalog/CatalogSettingsPage.tsx"] = new[] { "src/Modules/Catalog/ECSPros.Catalog.Application/Queries/GetImageSets/ImageSetGrid.cs" },
        ["admin/src/pages/settings/TranslationsPage.tsx"] = new[] { "src/Modules/Core/ECSPros.Core.Application/Queries/GetUiTranslations/UiTranslationGrid.cs" },
        ["admin/src/pages/storefront/ProductCardPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetCardMessages/CardMessageGrid.cs" },
        ["admin/src/pages/procurement/SortingPage.tsx"] = new[] { "src/Modules/Procurement/ECSPros.Procurement.Application/Queries/GetSortingEntries/SortingEntryGrid.cs" },
        ["admin/src/pages/finance/CommissionPage.tsx"] = new[] { "src/Modules/Accounts/ECSPros.Accounts.Application/Queries/GetSupplierSettlements/SettlementLineGrid.cs" },
        ["admin/src/pages/settings/PermissionLogsPage.tsx"] = new[] { "src/Modules/Iam/ECSPros.Iam.Application/Yetkilendirme/YetkiLogGrid.cs" },
        ["admin/src/pages/storefront/NewsletterSubscribersPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetNewsletterSubscriptions/NewsletterSubscriptionGrid.cs" },
        ["admin/src/pages/settings/AuditLogsPage.tsx"] = new[] { "src/Modules/Iam/ECSPros.Iam.Application/Queries/GetAuditLogs/AuditLogGrid.cs" },
        ["admin/src/pages/settings/UsersPage.tsx"] = new[] { "src/Modules/Iam/ECSPros.Iam.Application/Queries/GetUsers/UserGrid.cs" },
        ["admin/src/pages/storefront/ReviewsModerationPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetReviewsForModeration/ReviewModerationGrid.cs" },
        // Bildirim İzleme sayfasında İKİ grid var; denetim şu an tek eşleme kabul ettiği için
        // stok alarmları eşlendi, kayıtlı aramalar Coklu listesinde ayrıca denetlenir.
        ["admin/src/pages/storefront/ChannelCategoriesPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetChannelCategories/ChannelCategoryGrid.cs" },
        ["admin/src/pages/storefront/ChannelProductsPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetChannelProductsAdmin/ChannelProductGrid.cs" },
        ["admin/src/pages/storefront/CollectionsModerationPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetCollectionsForModeration/CollectionModerationGrid.cs" },
        ["admin/src/pages/storefront/ContactMessagesPage.tsx"] = new[] { "src/Modules/Storefront/ECSPros.Storefront.Application/Queries/GetContactMessages/ContactMessageGrid.cs" },
    };

    /// <summary>
    /// ★ Şemaya KONAMAYAN alanların meşru yolu: handler <c>Schema.ApplyFilters(q, grid, "alan")</c> ile
    /// o alanı ATLAR ve kendisi özel olarak işler (kullanıcıya/servise bağlı hesaplar). Bu test o
    /// bildirimleri kaynaktan toplar ve geçerli sayar — Users <c>role</c>, Tickets <c>readByMe</c>,
    /// Stocks <c>product</c> böyle çalışıyor. Sessiz kaçış listesi YOK: bir alan ne şemada ne de
    /// atlama bildiriminde geçiyorsa test patlar.
    /// </summary>
    private static HashSet<string> OzelIslenenAlanlar(string kok)
    {
        var kume = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
            var metin = File.ReadAllText(f);
            if (!metin.Contains("ApplyFilters(", StringComparison.Ordinal)) continue;
            foreach (Match m in Regex.Matches(metin, "ApplyFilters\\([^;]*?\\)"))
                foreach (Match a in Regex.Matches(m.Value, "\"([^\"]+)\""))
                    kume.Add(a.Groups[1].Value);
        }
        return kume;
    }

    private static DirectoryInfo RepoKok()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "admin"))) d = d.Parent;
        Assert.IsNotNull(d, "Repo kökü bulunamadı.");
        return d!;
    }

    /// <summary>Kolon nesnelerini kabaca ayırır: `key: '...'` başlangıcından sonraki `key: '` işaretine kadar.</summary>
    private static IEnumerable<(string Key, string Blok)> Kolonlar(string tsx)
    {
        foreach (var m in Regex.Matches(tsx, @"key:\s*'([^']+)'").Cast<Match>())
        {
            var sonraki = tsx.IndexOf("key: '", m.Index + m.Length, StringComparison.Ordinal);
            var blok = tsx[m.Index..(sonraki > 0 ? sonraki : Math.Min(tsx.Length, m.Index + 1600))];
            if (!blok.Contains("header:", StringComparison.Ordinal)) continue;   // kolon değil (sekme/filtre tanımı)
            yield return (m.Groups[1].Value, blok);
        }
    }

    [TestMethod]
    public void Istemcideki_her_sortable_kolonun_sunucuda_Sort_karsiligi_var()
    {
        var kok = RepoKok().FullName;
        var hatalar = new List<string>();
        var Muaf = OzelIslenenAlanlar(kok);   // handler'da ApplyFilters(..., "alan") ile atlanıp özel işlenenler

        foreach (var (sayfa, semalar) in Eslesme)
        {
            var tsxYol = Path.Combine(kok, sayfa.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(tsxYol)) { hatalar.Add($"{sayfa}: sayfa yok (eşleşme güncellenmeli)"); continue; }

            // Sayfadaki TÜM grid şemalarının birleşimi (çok grid'li sayfalar için; bkz. Eslesme açıklaması).
            var sorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filtreler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var semaEksik = false;
            foreach (var sema in semalar)
            {
                var semaYol = Path.Combine(kok, sema.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(semaYol)) { hatalar.Add($"{sayfa}: şema yok → {sema}"); semaEksik = true; continue; }
                var cs = File.ReadAllText(semaYol);
                foreach (Match m in Regex.Matches(cs, @"\.Sort\(""([^""]+)""")) sorts.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(cs, @"\.(?:Text|Enum|Number|Bool|Date|Guid)\(""([^""]+)""")) filtreler.Add(m.Groups[1].Value);
            }
            if (semaEksik) continue;

            var tsx = File.ReadAllText(tsxYol);

            foreach (var (key, blok) in Kolonlar(tsx))
            {
                if (Regex.IsMatch(blok, @"\bsortable:\s*true") && !sorts.Contains(key) && !Muaf.Contains(key))
                    hatalar.Add($"{sayfa}: '{key}' kolonu sortable ama şemada .Sort(\"{key}\") YOK → çalışma anında 400");

                // Başlık filtresinin sunucu alanı: filter.field ?? kolon anahtarı
                var alanEsleme = Regex.Match(blok, @"filter:\s*\{[^}]*field:\s*'([^']+)'");
                if (Regex.IsMatch(blok, @"\bfilter:\s*\{"))
                {
                    var alan = alanEsleme.Success ? alanEsleme.Groups[1].Value : key;
                    if (!filtreler.Contains(alan) && !Muaf.Contains(alan))
                        hatalar.Add($"{sayfa}: '{key}' kolonunun filtresi '{alan}' şemada bildirilmemiş");
                }
                // filters[] içindeki ek alanlar
                foreach (var fm in Regex.Matches(blok, @"field:\s*'([^']+)'").Cast<Match>())
                {
                    var alan = fm.Groups[1].Value;
                    if (!filtreler.Contains(alan) && !Muaf.Contains(alan))
                        hatalar.Add($"{sayfa}: ek filtre alanı '{alan}' şemada bildirilmemiş");
                }
            }
        }

        Assert.AreEqual(0, hatalar.Count, "İstemci↔sunucu anahtar uyumsuzlukları:\n" + string.Join("\n", hatalar.Distinct()));
    }

    /// <summary>
    /// YEREL grid sayfaları (useLocalGrid): satır kümesi doğası gereği TAM gelir ve sunucuda
    /// sayfalanmaz — tablo bir liste değil FORM'dur (kanal başına bir satır, entegrasyon başına
    /// barkod aralığı…). Bu sayfalarda sunucu şeması YOKTUR, dolayısıyla anahtar eşlemesi de yok.
    /// Muafiyet SESSİZ olmasın diye burada tek tek sayılır: yeni bir sayfa listeye ancak
    /// gerçekten sayfalanamayan bir form tablosuysa girer; sayfalı ucu olan liste şema kurar.
    /// </summary>
    private static readonly HashSet<string> YerelGridSayfalari = new()
    {
        "admin/src/pages/orders/NumberSeriesPage.tsx",
        "admin/src/pages/marketplaces/MappingPage.tsx",
    };

    [TestMethod]
    public void Yerel_grid_sayfalari_gercekten_useLocalGrid_kullaniyor()
    {
        var kok = RepoKok().FullName;
        foreach (var sayfa in YerelGridSayfalari)
        {
            var yol = Path.Combine(kok, sayfa.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(yol), $"Yerel grid sayfası bulunamadı: {sayfa}");
            var tsx = File.ReadAllText(yol);
            Assert.IsTrue(tsx.Contains("useLocalGrid", StringComparison.Ordinal),
                $"{sayfa} yerel grid muafiyetinde ama useLocalGrid kullanmıyor — sunucu şeması kurup " +
                "Eslesme'ye ekleyin ya da muafiyet listesinden çıkarın.");
        }
    }

    [TestMethod]
    public void DataGrid_kullanan_her_sayfa_denetim_eslesmesinde_kayitli()
    {
        var kok = RepoKok().FullName;
        var sayfalar = Directory.EnumerateFiles(Path.Combine(kok, "admin", "src", "pages"), "*.tsx", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("<DataGrid", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(kok, f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();

        var eksik = sayfalar.Where(s => !Eslesme.ContainsKey(s) && !YerelGridSayfalari.Contains(s)).ToList();
        Assert.AreEqual(0, eksik.Count,
            "Bu DataGrid sayfaları anahtar denetimine kayıtlı değil (Eslesme'ye ekleyin):\n" + string.Join("\n", eksik));
    }
}
