# Pazaryeri Satıcı Paneli — Tasarım Dokümanı

> Durum: **TASARIM ONAYLANDI (2026-07-21)** — uygulama başlamadı.
> Kaynak konuşma: "Finans vs Cari çift tedarikçi sayfası" + "pazaryeri tedarikçilerine panel".
> İlişkili: [`api-hesaplari-tasarimi.md`](api-hesaplari-tasarimi.md) (§8 "Tedarikçi self-servis
> portalı" açık noktası burada kapanıyor), [`cari-cati-gecis-plani.md`](cari-cati-gecis-plani.md).

---

## 0. Problem

Bugün iki ayrı sorun iç içe:

1. **Çift tedarikçi sayfası (teknik borç).** Admin panelinde tedarikçi İKİ yerden listeleniyor
   ama ikisi de **aynı** veriyi (`GET /api/accounts?accountType=supplier`) gösteriyor:
   - *Sistem > Finans > Tedarikçiler* → `admin/src/pages/finance/SuppliersPage.tsx`
     (accountType=supplier **sabit kodlu**, sadeleştirilmiş görünüm)
   - *Cari > Cari Kartlar* → `admin/src/pages/accounts/AccountsPage.tsx` (tip dropdown'lı tam yönetim)

   Sebep tarihsel: cari çatı geçişinde (`RemoveSuppliersUseCurrentAccounts`, 20260507151451)
   ayrı `Supplier` entity'si kaldırıldı; tedarikçi artık yalnızca `AccountType=supplier` olan
   bir cari. Finans sayfası bu geçişten kalma bir kopya.

2. **Tedarikçi tipleri ayrışmıyor + panel yok.** İki tür tedarikçi tamamen farklı yönetim
   ihtiyacına sahip:
   - **Normal tedarikçi** — bizim ürün temin ettiğimiz cari (fatura/ödeme/cari hareket). Bize
     ürün satar, biz karar veririz. Panel/API ihtiyacı yok.
   - **Pazaryeri tedarikçisi (satıcı)** — kendi ürününü *bizim sitemizde* satmak isteyen üçüncü
     taraf. Ürün kartı açar, stok/fiyat yönetir, sipariş alır, cari hesabını izler.

   Ayrım bugün yalnızca `ApiClientType` seviyesinde var (`supplier_managed` / `supplier_merchant`);
   **cari kartın kendisinde yok.** Ve pazaryeri satıcısının bizimle teması yalnızca **makine**
   (Partner API `/api/partner/v1` + admin'in "Tedarikçi Gönderimleri" onayı). Satıcının kendi
   gireceği **insan-yüzlü panel YOK.**

---

## 1. Onaylanan kararlar (2026-07-21)

| # | Karar | Gerekçe |
|---|-------|---------|
| K1 | Satıcı paneli **ayrı bir frontend uygulaması** (`satici/`, örn. `satici.ecspros.com`) | Dış taraf; iç admin panelinden tam izolasyon (F0 kimlik sınırının kardeşi). Kendi login'i, kendi menüsü. |
| K2 | Panele giriş **yeni `SupplierUser` kimliği** ile (4. kimlik türü) | Tedarikçinin bugün yalnız makine kimliği (`ApiClient`) var. İnsan paneli insan kimliği ister; `ApiClient` makine olarak kalır. |
| K3 | Panel **kendi ince-taneli `/api/supplier/*` yüzeyini** konuşur | Partner API bilinçli olarak kaba-taneli/makine sözleşmesi; sabit kalmalı. İnsan paneli zengin okuma/filtre ister → ayrı yüzey. |
| K4 | Tedarikçi tipi **cari kart seviyesinde ayrışır**: `current_accounts.SupplierKind` (`normal` \| `marketplace`) | "Tamamen ayrılmalı" şartı veri seviyesinde karşılanır. Yalnız `marketplace` cariler SupplierUser/panel/API alır. |
| K5 | Tedarikçi başına **çoklu kullanıcı, başta rolsüz** (hepsi tam yetkili) | Basit başla; tedarikçi-içi rol/yetki ayrımı sonraya bırakılır. |
| K6 | Çift tedarikçi sayfası **temizlenir**: Finans > Tedarikçiler kaldırılır, tek kaynak Cari Kartlar | Kopya UI teknik borcu; tek doğruluk kaynağı `accounts`. |

---

## 2. Kimlik modeli — dört kimlik türü

`SupplierUser` mevcut üçe eklenir. Kritik: `ApiClient` ve `SupplierUser` **aynı cari karta**
(`accounts.current_accounts`, `AccountType=supplier`, `SupplierKind=marketplace`) bağlanır →
`owner_id` = cari kart Id ikisinde de aynı. Tedarikçi API'den de panelden de gelse **aynı veri
havuzunu** görür (sahiplik: `Product.SupplierId = owner_id`, hiç değişmeden çalışır).

| Kimlik | Kim | Giriş | Token `type` | Nereye |
|--------|-----|-------|--------------|--------|
| `iam.User` | iç personel | e-posta/şifre | (yok → AdminOnly) | admin panel |
| `crm.Member` | son müşteri | e-posta/şifre | `member` | storefront |
| `iam.ApiClient` | tedarikçi **makinesi** | client_credentials | `api_client` | Partner API `/api/partner/v1` |
| **`SupplierUser`** *(YENİ)* | tedarikçi **insanı** | e-posta/şifre | **`supplier_user`** | **satıcı paneli** → `/api/supplier/*` |

**`SupplierUser` entity (taslak):** `Id`, `CurrentAccountId` (→ owner), `Email` (unique),
`PasswordHash` (BCrypt), `FullName`, `IsActive`, `LastLoginAt`, `MustChangePassword` + BaseEntity.
Hangi modül? → **IAM** (kimlik ailesi orada; `iam` şeması). Oturum yönetimi mevcut kalıp
(`UserSession`/`MemberSession` benzeri `SupplierSession` veya paylaşılan altyapı — S1'de netleşir).

**Yetki sınırı:** yeni `SupplierOnly` policy — `type=supplier_user` claim'i gerektirir; `owner_id`
claim'i cari kart Id'sini taşır. `/api/supplier/*` uçları bu policy + **owner-scope** ile korunur
(her sorgu `WHERE SupplierId = owner_id`). Bu, F0'daki `AdminOnly` ve `MemberOnly`'nin kardeşidir.

---

## 3. Üç frontend, dört API yüzeyi

```
admin/       → /api/...            iç yüzey        AdminOnly        (mevcut)
storefront/  → /api/store/...      üye/anonim      MemberOnly/anon  (mevcut)
partner API  → /api/partner/v1/... makine, kaba    RequireScope     (mevcut, F2)
satici/      → /api/supplier/...   insan, ince     SupplierOnly     (YENİ)
```

`/api/supplier/*` ile `/api/partner/v1/*` **aynı iş verisini farklı granülaritede** sunar:
partner = tek çağrıda ürün paketle (makine); supplier = listele/filtrele/taslak-düzenle (insan).
İkisi de aynı owner-scope ve aynı `ProductSubmission` / staging akışını kullanır — böylece panelden
açılan kart da API'den gelen kart da **aynı onay kapısından** (Kapı 2, admin onayı) geçer.

---

## 4. Panel ekran envanteri (kullanıcı isteği)

> K16 gereği: S3'e geçmeden her ekranın kurgusu ayrıca konuşulacak. Aşağısı kapsam listesidir.

| Ekran | İçerik | Dayandığı akış |
|-------|--------|----------------|
| **Giriş / Panel özeti** | Bekleyen onay, bugünkü sipariş, düşük stok, cari bakiye özeti | `/api/supplier/me` + özet sorgular |
| **Ürünlerim** | Kendi ürün/gönderim listesi (durum: taslak/onay bekliyor/canlı/reddedildi); panelden **kart aç/düzenle**; API ile yüklenenler de burada | `ProductSubmission` + `Product` (owner-scope), Kapı 1 doğrulama |
| **Stok & Fiyat** | Varyant bazında stok (mutlak) ve — tip `marketplace` ise — fiyat düzenleme | `UpsertSupplierStockCommand`; fiyat `pricing.write` mantığı |
| **Siparişlerim** | Kendi ürünlerine düşen sipariş kalemleri; durum izleme; (ileride) kargo/shipment | Order/Fulfillment owner-scope; `OrderItem.SupplierId` snapshot zaten var |
| **Cari Hesabım** | Bakiye + hareket ekstresi (salt-okunur) | `accounts` — `GetAccountTransactions` (cari çatı) |
| **API Hesabım** | Kendi `ApiClient`'ını görüntüle, secret yenile, IP/scope (salt-görüntü) | `api-hesaplari-tasarimi.md` §6 `ApiClientsPanel` mantığının owner-scoped hâli |
| **Firma/Profil** | İletişim, teslimat ayarı, kullanıcılar (K5: çoklu kullanıcı) | `current_accounts` + `SupplierUser` |

---

## 5. Fazlar

> Kural: bir faz bitmeden sonrakine geçme (PROGRESS.md). S0 bağımsız; S1→S2→S3 sıralı.
> Her faz izole port'ta test + commit; migration'lar additive.

| Faz | İş | Not |
|-----|-----|-----|
| **S0** | (a) Çift sayfa temizliği: Finans > Tedarikçiler kaldır, menü → Tedarikçi Faturaları; Cari Kartlar tek kaynak. (b) `current_accounts.SupplierKind` (`normal`\|`marketplace`) alanı + migration (additive, default `normal`) + Cari Detay'da tip seçimi | Küçük, bağımsız; panelden önce yapılabilir. Mevcut tüm tedarikçiler `normal` başlar. |
| **S1** | `SupplierUser` kimliği: entity (cari karta bağlı, `iam` şeması) + migration + `POST /api/supplier/auth/login` (BCrypt) + JWT `type=supplier_user`+`owner_id` + `SupplierOnly` policy + oturum yönetimi | F0/MemberOnly kardeşi. Yalnız `SupplierKind=marketplace` cariye kullanıcı açılabilir. |
| **S2** | `satici/` uygulama iskeleti (Vite+React, admin kalıbı): login + boş layout + `/api/supplier/me` (owner-scoped introspection) + nginx `/satici` sunumu | Auth uçtan uca çalışır; ekran yok. |
| **S3** | Panel ekranları (§4), her biri kendi `/api/supplier/*` ucu ile. **Ekran kurgusu §4'ten önce ayrıca konuşulur (K16).** Alt-adımlar: S3a Ürünlerim, S3b Stok&Fiyat, S3c Siparişlerim, S3d Cari, S3e API Hesabım, S3f Profil/Kullanıcılar | `api-hesaplari-tasarimi.md` bekleyen **F4**'ün genişletilmiş hâli. |

---

## 6. Açık / sonraya bırakılan

- **Tedarikçi-içi rol/yetki** (K5): başta rolsüz; ihtiyaç olunca eklenir.
- **Kargo/shipment satıcı tarafı**: `api-hesaplari-tasarimi.md` **F2b-2d** (dropship sipariş +
  shipment + rezervasyon yönlendirme) ile ortak; `order.write`/`fulfillment.write` tip kararı
  orada bekliyor. Satıcı paneli "Siparişlerim" ekranı önce salt-izleme başlar.
- **Satıcı onboarding** (yeni satıcı başvurusu → cari kart + ilk kullanıcı açma akışı): S3 sonrası.
- **`SupplierUser` şifre sıfırlama / e-posta doğrulama**: e-posta servisi hazır (SMTP DB'den);
  S1'de temel, akış detayı sonraya.
