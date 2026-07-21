# API Hesapları (ApiClient) — Tasarım

**Tarih:** 2026-07-20 (güncel: 2026-07-21)
**Durum:** F0 (590ffe1) + iç swagger kapatma (d2846f7) + F1 (db76a63) UYGULANDI.
Tip/scope modeli + iki-yüzey mimarisi + ürün ingestion modeli onaylandı (2026-07-21).
Sırada F2 (RequireScope + partner façade uçları + partner swagger).

## Amaç

API kullanıcısı; site üyesinden (CRM `Member`) ve personelden (IAM `User`) **bağımsız** üçüncü
bir kimlik türü olacak. Makine kimliği insan kimliğiyle aynı havuzda tutulmayacak.

## 0. İki API yüzeyi — iç vs partner façade (TEMEL MİMARİ)

En kritik karar (2026-07-21): dışa açılan API, **mevcut uçların scope'lanması değildir.**
İki **ayrı** yüzey vardır:

| | **İç API** (mevcut ~200 uç) | **Partner API façade** (YENİ, henüz yok) |
|--|------------------------------|-------------------------------------------|
| Kim kullanır | Admin panel (React) + storefront SSR | Tedarikçi / dropshipper / mobil / iç servis |
| Tane boyutu | İnce taneli, "gevezе" — bir stok kartı onlarca çağrı | **Kaba taneli, görev odaklı** — tek çağrı = tam bir iş |
| Rota | `/api/...` | `/api/partner/v1/...` |
| Auth | `[Authorize]` (=AdminOnly) / `RequirePermission` | `RequireScope` |
| Swagger | Yalnız Development (prod'da 404 — d2846f7) | Ayrı doküman, prod'da da açık (F2) |
| Dışarıya | **HİÇ açılmaz** | Tek dış temas noktası |

**Neden façade:** Tedarikçinin beklentisi "ürün kartı için gerekli TÜM bilgiyi tek pakette tek
servise gönder" — panelin 30 çağrılık iş akışını taklit etmek değil. Bu uçlar projenin iç
işleyişinde OLMAYAN, yalnız dışarısı için yazılan orkestrasyon uçlarıdır; içeride mevcut
iç servisleri kendileri çağırır.

**Sınır zaten yarı-hazır (F0 sayesinde):** api_client token'ı `[Authorize]`/`RequirePermission`
iç uçlarının HİÇBİRİNE giremez (member gibi); yalnız `RequireScope` işaretli partner uçlarına
girer. Yani "iç API'yi dışarı açma" derdi yoktur — iç uçlar tanımı gereği api_client'a kapalıdır.
Kalan iş: partner façade'ı *inşa etmek* (F2) + iç swagger'ı gizlemek (✅ d2846f7).

Partner façade uçlarının taslak listesi §3.5'te; tam istek/yanıt sözleşmesi ayrıca konuşulacak.

## 1. Üç kimlik türü

| Kimlik | Entity | Kim | Giriş yolu | Token'da |
|--------|--------|-----|-----------|----------|
| Personel | `iam.User` | Panel kullanıcısı | `/api/auth/login` | `permission` claim'leri |
| Site üyesi | `crm.Member` | Mağaza müşterisi | `/api/store/auth/login` | `type=member` |
| **API hesabı** | **`iam.ApiClient`** | **Uygulama/entegrasyon** | **`/api/auth/token`** | **`type=api_client`, `scope`** |

Üçü de aynı `Jwt:Secret` ile imzalanır ama **`type` claim'i kimlik sınırıdır** ve varsayılan
yetki politikası bu claim'e bakar. Bugünkü açık (üye token'ının düz `[Authorize]` uçlarını
geçmesi) bu ayrımla birlikte kapanır.

## 2. ApiClient entity

```csharp
public class ApiClient : BaseEntity
{
    public string Name { get; set; }              // "Mobil Uygulama", "Acme Tedarik Entegrasyonu"
    public string ClientId { get; set; }           // ecs_live_7f3a...  (public, indexed unique)
    public string SecretHash { get; set; }         // BCrypt — düz secret ASLA saklanmaz
    public string SecretHint { get; set; }         // "••••4f2c" — panelde gösterim için

    public string ClientType { get; set; }         // GÜVEN ekseni: internal | first_party | partner
    public string ApiClientTypeCode { get; set; }  // ROL ekseni: supplier_managed | supplier_merchant | first_party | internal (bkz. §3)
    public string? OwnerType { get; set; }         // current_account  (CurrentAccount kalıbı)
    public Guid? OwnerId { get; set; }             // accounts.current_accounts.Id

    // Gönderim modeli (yalnız tedarikçi tiplerinde anlamlı): platform | supplier.
    // supplier ise etkin scope'a order.read + fulfillment.write EKLENİR (§3, Yol B bayrağı).
    public string FulfillmentMode { get; set; } = "platform";

    // Scope'lar hesapta TUTULMAZ — ApiClientTypeCode (+ FulfillmentMode) token üretiminde çözülür (§3).
    public List<string> IpAllowList { get; set; }  // jsonb — boşsa kısıt yok
    public int RateLimitPerMinute { get; set; }

    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}
```

Tablo: `iam.api_clients` (şema adını tekrar eden önek yok).

## 3. Yetki modeli — tip = önceden belirlenmiş scope paketi

Dışa API erişiminde scope'lar **hesap başına serbest seçilmez**. Her API hesabı bir
**API kullanıcı tipi** (`ApiClientType`) ile ilişkilendirilir; scope seti tipte **sabittir**
ve tipten türetilir (**tamamen kilitli** — hesapta düzenlenemez, kullanıcı kararı 2026-07-20).

**İki dik eksen:**
- `ClientType` (§2) = **güven/köken**: istek nereden geliyor, ne kadar güvenilir (internal/first_party/partner).
- `ApiClientTypeCode` = **iş rolü**: yönetilen tedarikçi / pazaryeri tedarikçisi / mobil / iç servis → yetki paketini belirler.

### Scope kataloğu (10 scope)
Panel `Permissions` kataloğundan **bağımsız** bir scope listesi. Gerekçe: yeni bir panel
yetkisi eklemek dışarıya açılan API'nin kapsamını yanlışlıkla genişletmemeli.

```
catalog.read       ürün/kategori okuma
catalog.write      ürün İÇERİĞİ yazma (ad, açıklama, özellik, görsel, kategori) — fiyat HARİÇ
pricing.write      fiyat + satış kuralları (kanal fiyatı, vergi, satışa aç/kapa, kampanya uygunluğu)
stock.read         stok okuma
stock.write        stok bildirme/güncelleme
order.read         sipariş okuma
order.write        sipariş oluşturma/değiştirme
fulfillment.write  "kargoladım" + takip no bildirme (siparişi değiştirmeden)
invoice.read       fatura okuma
account.read       cari ekstre / bakiye okuma
```

**catalog.write ≠ pricing.write** kritik ayrımdır: iki tedarikçi tipini ayıran tek eksen budur.
Yönetilen tedarikçi ürün *içeriğini* yazar ama fiyat/satış kuralı BİZDEDİR; pazaryeri tedarikçisi
fiyat dahil her şeyi kendisi belirler.

### Tip kataloğu — `definition.api_client_types`
Platformca (geliştirici firma) doldurulur; veri aktarımı/eşleme kayıt EKLEYEMEZ
(**definition şeması altın kuralı** — `integration_services` ile aynı). Her tip: kod, ad,
güven ekseni, zorunlu `OwnerType`, sabit **taban** scope seti.

| Kod | Ad | Güven | Zorunlu sahip | Taban scope paketi (sabit) |
|-----|-----|-------|--------------|----------------------------|
| `supplier_managed` | Yönetilen tedarikçi (gelir paylaşımlı) | partner | current_account (supplier) | catalog.read, catalog.write, stock.read, stock.write, invoice.read, account.read |
| `supplier_merchant` | Pazaryeri tedarikçisi (fiyatı o belirler) | partner | current_account (supplier) | + **pricing.write** (yukarıdaki tabana) |
| `first_party` | Mobil / birinci taraf | first_party | — | catalog.read, stock.read, order.read |
| `internal` | İç servis / entegrasyon | internal | — | tüm scope'lar; yalnız loopback (§5) |

**Gönderim bayrağı (Yol B — kullanıcı kararı 2026-07-21):** Tedarikçi tiplerinde "kargoyu kim
gönderiyor" anlaşmaya göre değiştiğinden, bunu tipe değil hesaptaki `FulfillmentMode` alanına
bağladık. `FulfillmentMode=supplier` ise etkin scope = taban paket **+ `order.read` +
`fulfillment.write`**. Bu, "keyfi scope seçme" DEĞİLDİR: kimse tek tek scope işaretlemez;
yalnız tek bir iş gerçeği (Biz/Tedarikçi gönderiyor) işaretlenir, scope sonucu deterministiktir.
Ticari kimlik tipte, lojistik gerçeği bayrakta.

- Etkin scope = `type.BaseScopes` ∪ (`FulfillmentMode==supplier` ? {order.read, fulfillment.write} : {}).
- Yeni bir **scope** eklendiğinde hangi tiplere gireceği tek yerde (tip tanımı) kararlaştırılır.
- Token üretiminde `scope` claim'i tipten (+ bayraktan) çözülür; tip güncellenince tüm o tipteki
  hesaplar yeni token'da yeni sete kavuşur (mevcut 15 dk'lık token'lar doğal olarak biter).
- `RequireScopeAttribute` — `RequirePermissionAttribute` ile aynı kalıp, `scope` claim'ine bakar.
- **Scope "ne yapabilir", owner "hangi veri" demektir.** İkisi ayrı kontrol: `OwnerType=current_account`
  olan bir token'da sorgular otomatik `OwnerId` ile filtrelenir. `stock.write` scope'u olan bir
  tedarikçi yalnız kendi kalemlerini günceller. Tip zaten `OwnerType`'ı zorladığından, tedarikçi
  tipli bir hesap sahipsiz açılamaz.

> **Kapsam notu (2026-07-21):** Dış/partner tarafında şimdilik yalnız bu 2 tedarikçi tipi var;
> **dış pazaryeri entegratörü tipi YOK** (Trendyol/Hepsiburada'ya *biz* push ederiz — bu bir
> `internal` iş, partner değil). `first_party` (mobil) ve `internal` bizim kendi istemcilerimiz.

## 3.5. Partner façade uçları (taslak — sözleşme ayrıca konuşulacak)

`/api/partner/v1/...` altında, ayrı swagger dokümanıyla. Kaba taneli, görev odaklı, versiyonlu.
Her uç `RequireScope` ile korunur; owner filtresi (tedarikçi cari) veriyi otomatik daraltır.

| Uç | İş | Scope |
|----|-----|-------|
| `POST /api/partner/v1/products` | Ürün kartını **tek pakette** oluştur/güncelle (içerik+varyant+stok, ops. fiyat) | catalog.write (+pricing.write) |
| `GET /api/partner/v1/products[/{code}]` | Ürün bilgisi sorgula | catalog.read |
| `PUT /api/partner/v1/products/{code}/stock` | Stok bildir/güncelle | stock.write |
| `POST /api/partner/v1/orders` | Sipariş **tek servisle** ilet (dropship) | order.write |
| `GET /api/partner/v1/orders[/{id}]` | Sipariş/durum sorgula | order.read |
| `POST /api/partner/v1/orders/{id}/shipment` | "Kargoladım + takip no" bildir | fulfillment.write |
| `GET /api/partner/v1/invoices` | Fatura listesi | invoice.read |
| `GET /api/partner/v1/account/statement` | Cari ekstre / bakiye | account.read |

~10-12 uç. Her biri içeride mevcut iç servis/handler'ları orkestrasyonla çağırır; dışarıya
kararlı, sürümlü bir sözleşme sunar.

## 3.6. Ürün kartı ingestion modeli (kararlar 2026-07-21)

Façade **canlı katalogu doğrudan yazmaz** → bir **staging/submission** alanı yazar; onay
sonrası yayınlanır. Böylece dış gönderim ile canlı katalog ayrışır.

**1) Onaya DÜŞEN: yalnız ürün KARTI içeriği** (ad, açıklama, grup, özellikler, varyant yapısı,
görseller). Kullanıcı kararı: *her ürün onaya düşer* (her iki tip).
- Yeni ürün → `pending` submission.
- Canlı ürünün içerik düzenlemesi → **pending revizyon**; canlı sürüm onaya kadar AYNEN yayında kalır.
- Panel: **"Tedarikçi Gönderimleri"** ekranı (incele → canlıyla karşılaştır → onayla/reddet). K16 gereği bu ekran F2/F4 kapsamında.

**2) Onaya DÜŞMEYEN (direkt): stok + (Tip 2) fiyat.** Operasyonel/sık değişen veri; zaten onaylı
kendi ürününün kalemine anında yazılır (`PUT /products/{externalCode}/stock`). İlk stok submission
içinde gelir; sonraki güncellemeler direkt.

**3) Grup bazlı doğrulama:** `group` kodu geçerli varyant eksenlerini + izinli özellikleri belirler.
Keşif uçları `GET /groups`, `GET /groups/{code}` (catalog.read) tedarikçiye geçerli kodları verir.
Kategori/grup **bizim kod listemizden** seçilir (kullanıcı kararı); storefront kategori yerleşimi
onay anında bizce teyit edilir.

**4) Tip'e göre fiyat:** Tip 1 (Yönetilen) → `price` yok sayılır (biz koyarız). Tip 2 (Pazaryeri)
→ `price` zorunlu. `catalog.write` içerik, `pricing.write` fiyattır (§3).

**5) Upsert & idempotency:** `externalCode` (tedarikçinin kendi kodu) anahtardır; aynı kodla tekrar
POST → mevcut submission/ürünü günceller. Owner filtresi: submission + ürünler tedarikçinin
`OwnerId`'sine bağlı, başka tedarikçinin kodunu göremez/ezemez.

**Yanıt:** `{ submissionId, externalCode, status: "pending|approved|rejected", productCode: null|"P-..." }`

İstek gövdesinin **tam alan sözleşmesi** (varyant/özellik/görsel şemasının kesin biçimi) ayrıca
netleştirilecek.

## 3.7. Sahiplik modeli (owner) — kullanıcı kararı 2026-07-21

**Yeni alan/tablo YOK.** Sahiplik zaten mevcut `Product.SupplierId` (Guid?) + `SupplierProductCode`
alanlarında; bunlar canlıda sipariş→paket bölme akışında kullanılıyor (OrderItem.SupplierId'ye
snapshot'lanır, paketler tedarikçiye bölünür). Model:

- **`Product.SupplierId` = sahip** → `accounts.current_accounts.Id` (AccountType=supplier). Bu
  `ApiClient.OwnerId` ile **aynı** şeyi işaret eder; owner-scope filtresi doğrudan
  `WHERE Product.SupplierId = token.owner_id`.
- **Granülerlik: ürün seviyesi, TEK sahip (1 ürün : 1 tedarikçi).** Aynı fiziksel ürünü iki
  tedarikçi verirse iki ayrı Product kaydı olur (kendi fiyat/stok). Paylaşılan katalog / çoklu
  teklif (offer katmanı) YOK.
- **externalCode = `SupplierProductCode`.** Upsert/idempotency anahtarı = **(SupplierId,
  SupplierProductCode)** unique. Bir tedarikçi başka tedarikçinin kodunu göremez/ezemez.
- **Mevcut ~28.6K ürün `SupplierId=null`** = bizim/platform; partner token'ları (owner-scoped)
  **asla görmez** (migrasyon gerekmez).
- **FK yok** (modüler monolit — Catalog→Accounts gevşek Guid); F2b'de yalnız index eklenir:
  `SupplierId` index + `(SupplierId, SupplierProductCode)` filtered unique.
- Her iki tedarikçi tipinde de SupplierId = o tedarikçi (yönetilen tedarikçide fiyat/kural bizde
  ama ürün yine onun; owner-scope onu kendi ürünlerine sınırlar).

## 4. Token akışı — OAuth2 client_credentials

```
POST /api/auth/token
{ "clientId": "ecs_live_7f3a...", "clientSecret": "..." }
→ { "accessToken": "<jwt>", "expiresIn": 900, "tokenType": "Bearer" }
```

- Ömür **15 dk**, refresh token **yok** (istemci gerektiğinde yeniden ister).
- Doğrulama sırası: ClientId bulundu mu → aktif mi → süresi geçmiş mi → IP allowlist → BCrypt secret.
- Başarısız denemeler audit'e yazılır; bu uç rate limit'in ilk uygulanacağı yer.

## 5. İç "süper" hesap — dışarıya kapalı

`ClientType=internal` hesap uygulama içi kullanım içindir ve dışarıdan **erişilemez**:

1. `/api/auth/token` ucu `internal` tipli bir client için yalnız loopback/özel ağ IP'sinden
   token verir (`RemoteIpAddress` kontrolü, nginx `X-Forwarded-For` değil — spoof edilebilir).
2. nginx tarafında `location /api/internal/ { deny all; }`.
3. Secret repoda/appsettings'te durmaz; ilk açılışta üretilip Data Protection ile saklanır
   (key ring `~/.ecspros/dp-keys` — yedeğe dahil).

Mobil uygulama `first_party` tipiyle ayrı bir hesap alır; internal hesabı **kullanmaz**
(mobil istemci secret'ı cihazda taşır, sızması halinde kapsam dar kalmalı).

## 6. Ekran kurgusu

**a) Ayarlar > API Hesapları (liste)**
Sütunlar: Ad · Tip · Sahip (cari adı) · Scope sayısı · Son kullanım · Durum.
Satır tıklanabilir → detay (liste satırı kuralı).

**b) API Hesabı Detayı** — sekmeler:
- *Genel*: ad, **API tipi** (dropdown — kataloğdan), sahip cari, **Gönderen** (Biz/Tedarikçi —
  yalnız tedarikçi tiplerinde görünür, `FulfillmentMode`), durum, geçerlilik
- *Yetkiler*: tip taban scope'ları + (gönderim bayrağından gelen order.read/fulfillment.write)
  **salt-görüntü** (kilitli — düzenlenemez; değişiklik tip tanımından veya Gönderen alanından)
- *Güvenlik*: IP allowlist, dakikalık limit, secret yenile
- *Kullanım*: son istekler, hata sayısı, audit kayıtları

**c) Cari Detay > "API Erişimi" sekmesi**
`AccountDetailPage` içinde, aynı `<ApiClientsPanel accountId=... />` bileşeni. Tedarikçinin
hesapları burada görülür, eklenir, yetkisi değiştirilir, iptal edilir.

**Secret gösterimi:** düz secret yalnız **bir kez**, oluşturma/yenileme anında modalde gösterilir
("kopyaladınız mı?" onayı ile). Sonrasında her yerde `••••4f2c`. Yenileme eskisini anında geçersiz kılar.

## 7. Fazlar

| Faz | İş | Not |
|-----|-----|-----|
| **F0** ✅ | Varsayılan `AdminOnly` politikası (`type != member && type != api_client`) | **TAMAM (2026-07-21, commit 590ffe1) — üye/api_client iç uçları geçemez** |
| **Fx** ✅ | İç swagger prod'da kapatıldı (yalnız Development) | **TAMAM (2026-07-21, commit d2846f7) — iç yüzey artık listelenmiyor** |
| **F1** ✅ | `definition.api_client_types` kataloğu — 4 tip seed (§3) + `ApiClient` entity (`FulfillmentMode` dahil) + migration + `/api/auth/token` | **TAMAM (2026-07-21, commit db76a63) — tip=kilitli scope; catalog.write≠pricing.write** |
| **F2a** ✅ | `RequireScopeAttribute` + `ApiClientOnly` policy + **partner swagger doc** (prod'da açık, iç doküman dev-only) + `PartnerController` (`GET /me`, `GET /groups` keşif) | **TAMAM (2026-07-21, commit 5fd112a) — façade iskeleti + ilk keşif ucu** |
| **F2b** | Partner **ürün ingestion** uçları (§3.5/§3.6): `POST /products` (submission/staging + onay), `PUT /products/{code}/stock`, `POST /orders`, shipment vb. + owner filtresi + ürün→tedarikçi sahiplik modeli | **Asıl iş — gövde sözleşmesi + sahiplik modeli netleşecek** |
| F3 | Internal hesap (loopback kısıtı) + mobil first_party hesabı | |
| F4 | Panel ekranları (liste, detay, cari sekmesi) | Ekran kurgusu §6 onayından sonra |
| F5 | Rate limit + audit + kullanım ekranı | Rate limit bugün hiç yok |

## 8. Açık noktalar

- **Partner swagger dokümanı** (F2): `/api/partner/v1/*` uçlarını listeleyen, prod'da açık, ayrı
  bir Swagger doc. İç yüzey Development dışında gizli kalır (d2846f7).
- Tedarikçi self-servis portalı düşünülüyor mu? Düşünülüyorsa API hesabı ile portal girişi
  ayrı kalmalı (biri makine, diğeri insan kimliği).
- **nginx `location /swagger`** proxy'si duruyor; app 404 verdiği için maruziyet kapalı ama
  istenirse `deny all` ile ağ katmanında da kapatılabilir (ekstra sertleştirme).
