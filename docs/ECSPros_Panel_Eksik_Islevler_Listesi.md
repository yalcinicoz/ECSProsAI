# ECSPros — Panel Eksik İşlevler Listesi

**Tarih:** 10 Eylül 2026 · **Sürüm:** 1.1 (doğrulama sonrası düzeltildi)
**Kapsam:** Yalnız `admin` paneli. Eski panelin **Rapor** ve **Bayi** grupları kapsam dışı.

**Kaynaklar:**
- Eski panel: `ECSGYE.Solution/EKARE.AdminUI` (commit `dacd688a`, 2026-09-07) + `juludedb.dfadminmenu` menü tablosu + `wwwroot/htmls` + `wwwroot/modals`
- Yeni panel: `ECSProsAI` (commit `b2bc874a`, 2026-09-10), `admin/src` + `src/`

**İncelenen hacim:** eski panelde 66 menü sayfası + 14 kısayol modalı; yeni panelde 68 menü kalemi / 110 rota / 122 sayfa bileşeni.

---

## LİSTE 1 — Yeni panelde açılmış ama tamamlanmamış

### B) Sayfası yazılmış ama panele hiç bağlanmamış — 4

| Sayfa | Durum |
|---|---|
| Filtre Renkleri | `catalog/FilterColorsPage.tsx` — rota yok, menü yok, hiçbir dosyadan çağrılmıyor |
| Filtre Ön Ayarları | `catalog/FilterPresetsPage.tsx` — aynı durum |
| Menü Yönetimi + Menü Detay | `cms/MenusPage.tsx`, `cms/MenuDetailPage.tsx` — `/navigation/menus` rotası var ama `/storefront/menu-placement`'a yönlendiriyor; sayfalar ölü kod |
| Placeholder bileşeni | `pages/PlaceholderPage.tsx` — "Bu sayfa henüz geliştirilme aşamasında" bileşeni duruyor, hiçbir yerden çağrılmıyor |

### C) Menüde satırı yok ama erişilebilir — eksik DEĞİL

Bu sayfalar sidebar'da kendi satırı olmadığı için ilk taramada "menüde yok" görünüyor, ancak hepsi erişilebilir. Listeye **alınmadı**:

| Sayfa | Rota | Nasıl erişiliyor |
|---|---|---|
| POS Kasalar | `/pos/registers` | Menüdeki **POS** kalemine `activePatterns` ile bağlı |
| Paketleme İstasyonları | `/fulfillment/packing-stations` | Menüdeki **Masa İzleme** kalemine `activePatterns` ile bağlı |
| Roller | `/settings/roles` | Menüdeki **Kullanıcılar** kalemine bağlı |
| Diller | `/settings/languages` | Menüdeki **Kullanıcılar** kalemine bağlı |
| Lookup Tipleri | `/settings/lookup-types` | Menüdeki **Kullanıcılar** kalemine bağlı |
| Denetim Logları | `/settings/audit-logs` | Menüdeki **Kullanıcılar** kalemine bağlı |
| Pazaryeri Eşleştirme | `/marketplaces/eslestirme` | Dashboard, Pazaryerleri ve Kanal Ürünleri ekranlarından link |
| Talep Ayarları | `/crm/tickets/settings` | Müşteri İlişkileri ekranındaki "Ayarlar" butonu |
| Sayfa Yayın Geçmişi | `/storefront/pages/history` | Vitrin Yönetimi ekranındaki buton |
| Tedarikçiler | `/finance/suppliers` | Sayfa değil — `/accounts?accountType=supplier` adresine yönlendirme |

> **Yöntem notu:** menü eşleşmesi yapılırken `NavItem.to` alanının yanında **`NavItem.activePatterns`** alanı da okunmalıdır. Yalnız `to` üzerinden yapılan karşılaştırma yukarıdaki 6 sayfayı yanlışlıkla "menüde yok" gösterir.

### D) Backend hazır, panel ekranı yok — 1

| İşlev | Durum |
|---|---|
| Manken | `Mannequin` entity + `UpdateMannequin` / `DeleteMannequin` komutları + `ProductImageController` var; yönetim ekranı yok |

**Liste 1 toplamı: 6 kalem.**

---

## LİSTE 2 — Eski panelden yeni panele taşınmayanlar

Toplam **41 kalem**: 35 menü sayfası + 6 kısayol modalı.
*Kısmi* = işlev yeni panelde var ama eski paneldeki kapsamı karşılamıyor.

### Stok Kartları — 3

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Kombin Panel | `/kombin-panel` | Yok — yeni tarafta yalnız ürün detayında kombin referansı var, panel yok |
| Ürün Özel Açıklamaları | `/urun-ozel-aciklamalari` | Yok |
| Ürün Yorum Ekle | `/urun-yorum-ekle` | **Kısmi** — Yorum Moderasyonu var, manuel yorum ekleme yok |

### CRM — 5

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Üründen Sipariş Sorgula | `/urunden-siparis-sorgula` | Yok |
| Kargo Firmaları Sipariş | `/kargo-firmalari-siparis` | Yok |
| MT Kalite Değerlendirme | `/mt-kalite-degerlendirme` | Yok |
| Müşteriye Ödemeler | `/musteriye-odemeler` | Yok |
| Sipariş Üye Notları | `/siparis-uye-notlari` | **Kısmi** — not alanları Sipariş Detay'da var (`customerNotes`, `internalNotes`), toplu not listesi ekranı yok |

### Depo — 11

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Rafa Ürün Yerleştir | `/rafa-urun-yerlestir` | Yok |
| Rafa Toplu Ürün Yerleştir | `/rafa-toplu-urun-yerlestir` | Yok |
| İadeden Rafa Ürün Yerleştir | `/iadeden-rafa-urun-yerlestir` | Yok |
| Raftan Rafa Ürün Transfer | `/raftan-rafa-urun-transfer` | Yok — Stok Hareketleri var ama raf seviyesi yok |
| Raf Sayım | `/raf-sayim` | Yok |
| Raf Ürün Listesi | `/raf-urun-listesi` | Yok |
| Mağaza Depodan Reyona Tek Ürün | `/magaza-depodan-reyona` | Yok |
| Mağaza Reyondan Ürün Çıkar | `/magaza-reyondan-urun-cikar?code=MR` | Yok |
| Ayakkabı Reyondan Ürün Çıkar | `/magaza-reyondan-urun-cikar?code=AR` | Yok |
| Güngören Reyondan Ürün Çıkar | `/magaza-reyondan-urun-cikar?code=GR` | Yok |
| Stüdyo Panel | `/studyo-panel` | **Kısmi** — depo tipi ve transfer tipi olarak `studio` var, yönetim ekranı yok |

> **Dikkat:** Yeni tarafta `WarehouseLocation` entity'si mevcut, yani raf kavramı domain'de duruyor — ancak **raf seviyesinde tek bir operasyon ekranı yok.** Listenin en büyük ve en homojen bloğu bu.

### Dizayn — 6

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Manken Listesi | `/manken-listesi` | Yok — backend hazır (bkz. Liste 1/D) |
| Site Yönetimi | `/site-yonetimi` | Yok |
| Site URL Yönetimi | `/site-url-yonetimi` | Yok |
| Site Bileşen Yönetimi | `/site-bilesen-yonetimi` | Yok |
| Script Yönetimi | `/script-yonetimi` | Yok |
| Optimizasyon İşlemleri | `/optimizasyon-islemleri` | Yok |

### Fatura — 1

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Set Koli Sipariş Sorgula | `/set-koli-siparis-sorgula` | Yok |

### Pazar Yerleri — 1

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Trendyol BuyBox | `/trendyol-buybox` | Yok |

### Sipariş — 4

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| Sipariş Ara Birleştirme İzleme | `/siparis-ara-birlestirme-izleme` | Yok |
| Sipariş Ara Birleştirme Kontrol | `/siparis-ara-birlestirme-kontrol` | Yok |
| Kullanıcı Toplama Yönetimi | `/kullanici-toplama-yonetimi` | Yok |
| Özel Sipariş Toplama Oluştur | `/siparis-ozel-toplama-olustur` | **Kısmi** — Toplama Planlama var, "özel toplama" ayrımı yok |

### Yönetim — 4

| Eski sayfa | Eski URL | Durum |
|---|---|---|
| 301 Yönlendirmeleri | `/ucyuzbir-yonlendirmeleri` | Yok |
| Hediye Çarkı | `/kampanya-cark-yonetimi` | Yok |
| MT Kalite Soruları | `/mt-kalite-soru` | Yok |
| Kargo Tutarları | `/kargo-tutarlari` | **Kısmi** — yeni modelde kanal başına tek sabit kargo bedeli + ücretsiz kargo eşiği var (`settings/ChannelsPage.tsx`); eski bölge/kademe bazlı tutar tablosu yok |

### Kısayol modalları (`wwwroot/modals`) — 6

| Modal | Durum |
|---|---|
| Depo Eksik Ürün Temizle | Yok |
| ERP Stok Kartı Güncelle | Yok |
| Fatura Yeniden Yazdır | Yok |
| Manken Ölçüleri Gir | Yok |
| Sipariş Ayır | Yok |
| Tüm Siparişlere Ürün Ata | Yok |

Taşındığı görülen modallar: ERP Toplu Fiyat Güncelle · Kargo Takip · Raf Barkod Yazdır · Sipariş Detay Getir · Sipariş Oluştur · Müşteri İlişkileri Yeni Kayıt · MT Kalite Yeni Kayıt.

---

## Ek — İsmi değişerek taşınanlar (taşınmadı listesine ALINMADI)

| Eski panel | Yeni panel |
|---|---|
| Ürün Panel | Ürün Kartları (`/catalog/products`) |
| Ürün Gruplari | Ürün Grupları (`/catalog/product-groups`) |
| Ürün Görselleri / Ürün Görselleri Düzenle | Ürün Detay → Görseller + Toplu Resim Yükleme |
| Ürün Videoları Düzenle | Ürün Detay → Görseller → Videolar (`ProductImagesTab`) |
| **Ürün Beden Özellikleri** | **Ürün Detay → Alt Özellikler / Ürün Grubu → ölçüsel özellikler** (örn. "Beden 38 → Paça Boyu: 74 cm") |
| Müşteri Yönetimi | Üyeler (`/crm/members`) |
| Müşteri İlişkileri Yönetimi | Müşteri İlişkileri (`/crm/tickets`) |
| Sipariş Listesi | Siparişler (`/orders`) |
| Ürün Transfer Listesi | Stok Hareketleri (`/inventory/transfers`) |
| Ürün Topla / Sipariş Topla | Ürün Toplama (`/fulfillment/my-picking`) |
| Sipariş Toplama Planlama | Toplama Planlama (`/fulfillment/picking-plans`) |
| Sipariş Toplama İzleme | Masa İzleme (`/fulfillment/desks`) |
| Masaya Katıl | Masa (`/fulfillment/desk/:deskId`) |
| Kargo Şirketi Değiştir | Kargo Yönlendirme (`/fulfillment/cargo-reroute`) |
| İade / Değişim Kabul Et | İadeler (`/orders/returns`) |
| Sipariş Oluştur | POS Satış (`/pos/sales`) |
| Ürün Kabul Ürün Barkod Listesi | Mal Kabul (`/procurement/receipts`) |
| Menü Tasarımı | Menü Yerleşimi (`/storefront/menu-placement`) |
| Sayfa Listesi + Statik Sayfa Yönetimi | Sayfalar (`/cms/pages`) |
| Dizayn Listesi | Vitrin Yönetimi (`/storefront/pages`) |
| Pazaryeri Panel | Pazaryerleri (`/marketplaces`) |
| Kampanya Yönetimi | Kampanyalar (`/promotion/campaigns`) |
| İndirim Kodları | Kuponlar (`/promotion/coupons`) |
| Kargo Bölge Yönetimi | Kargo Bölgeleri (`/orders/cargo-zones`) |
| Kullanıcı Listesi | Kullanıcılar (`/settings/users`) |
| Kullanıcı Yetkilendirme + Yetki Grupları | Yetki Grupları / Yetki İçerikleri |
| Ürün Yorumları | Yorum Moderasyonu (`/storefront/reviews`) |

---

## Doğrulanamayan tek kalem

**Fatura OBM Temizle** modalı — yeni tarafta `fulfillment/DeskPage.tsx` ve `DesksMonitorPage.tsx` içinde benzer bir işlev görünüyor ancak aynı işlev olduğu teyit edilemedi. "Taşınmadı" listesine **alınmadı**; ayrıca doğrulanmalı.

---

## v1.0 → v1.1 değişiklikleri

| Değişiklik | Sebep |
|---|---|
| Liste 1/C (10 sayfa) "eksik" listesinden çıkarıldı | `NavItem.activePatterns` alanı ilk taramada okunmamıştı; 6 sayfa menü kalemlerine bu alanla bağlı, 3'ü sayfa içi butonla erişiliyor, 1'i zaten yönlendirme |
| Ürün Beden Özellikleri "taşınmadı"dan çıkarıldı | Ürün Detay → Alt Özellikler ve Ürün Grubu ölçüsel özellikleri olarak taşınmış |
| Sipariş Üye Notları "yok" → **kısmi** | Not alanları Sipariş Detay'da mevcut, eksik olan liste ekranı |
| Kargo Tutarları "yok" → **kısmi** | Kanal bazlı sabit kargo bedeli + ücretsiz kargo eşiği mevcut, eski tutar tablosu yok |
