# Mobil Backend İstek Listesi — Değerlendirme ve Plan (2026-09-09)

Kaynak: Misharitalia mobil ekibinin 2026-09-09 tarihli istek listesi.
Bu belge her maddeyi **koda karşı doğrulayıp** üç kovaya ayırır: *zaten var* · *gerçek hata* · *yeni iş*.

---

## 0. En önemli bulgu — staging bir gün eski binary çalıştırıyordu

| Örnek | Binary tarihi |
|---|---|
| Canlı (`ecspros`, :5000) | **9 Eylül 14:26** |
| **Staging (`ecspros-staging`, :5055 — mobilin test ettiği yer)** | **8 Eylül 23:17** |
| Demo (`ecspros-demo`) | 8 Eylül 23:17 |

Staging canlı veritabanını (`ecommerce_db`) kullanıyor ama **binary'si güncellenmiyordu**; restart'lar
eski derlemeyi yeniden başlatıyordu. Listedeki maddelerin bir kısmı bu yüzden "eksik" görünüyor.
`publish-staging` bugünün derlemesiyle güncellendi → **`sudo systemctl restart ecspros-staging` gerekiyor.**

> **Kalıcı kural:** canlıya yayın yapılırken staging de eşitlenmeli. Aynı DB'yi kullandıkları için
> eski binary yeni migration'larda 500 de üretebilir (bkz. 2026-09-06 olayı).

---

## 1. Sepet — `GET /api/store/cart`

| İstek | Durum | Not |
|---|---|---|
| `shippingFee`, `freeShippingThreshold`, `freeShippingCampaign`, `shippingCampaignFee` | ✅ **ZATEN VAR** (bugün eklendi) | Staging restart'ıyla görünür |
| `shippingFeeResolved`, `shippingFreeReason`, `remainingForFreeShipping` | 🔨 Yeni iş | Kural (`KargoUcretiKurali`) sunucuda hazır; sepette çözülüp döndürülecek — istemcinin kuralı taklit etmesi biter |
| Üst düzey `total` | 🔨 Yeni iş | `subtotal − indirimler + kargo` |
| `subtotal`/`campaignDiscount` satırlarla tutarlı | ⚠️ **Karar gerek** | Bugün `campaignDiscount` = YALNIZ sepet-seviyesi indirim; ürün-bazlı indirim `campaignLineDiscount`'ta. Anlamı değiştirmek web istemcisini de etkiler → yeni alan mı, anlam değişikliği mi? |
| Satırda `unitPrice`, `compareAtPrice`, `compareAtLineTotal` | 🔨 Yeni iş | Liste kartıyla aynı kaynak (`KartFiyatGorunumu`) |
| **"%15 İndirim" sepette ikinci kez uygulanıyor** | 🐞 **GERÇEK HATA — doğrulandı** | Kök neden aşağıda |
| `POST /cart/items` içindeki `price` anlamı | 🐞 Aynı hatanın kaynağı | Mobilin önerisi doğru: sunucu hesaplasın |

### Çifte indirimin kök nedeni (kod okumasıyla doğrulandı)

`POST /cart/items` istemcinin gönderdiği `price` değerini **olduğu gibi** `CartItem.AddedPrice`'a yazıyor
(`StoreCartController.AddItem` → `AddToCartCommand`; tek kontrol `price > 0`). `GET /cart` ise kampanya
motorunu **bu fiyatın üzerine** çalıştırıyor (`GetCartQueryHandler` → `ResolveCartAsync(..., AddedPrice)`).

- Web, listede **kampanya öncesi** fiyatı gönderdiği için sonuç doğru çıkıyor.
- Mobil, B9 sözleşmesine uyup listedeki `price`'ı (kampanyalı 679,99) gönderiyor → kampanya **ikinci kez**
  uygulanıyor (577,99).

Yani hata mobil tarafında değil; **istemci fiyatına güvenen sepet** tasarımında. Çözüm, checkout'ta
2026-07-31'de zaten yapılanın aynısı: **fiyatı sunucu belirlesin, istemciden geleni yok saysın**
(`CheckoutCommand`: *"Toplam SUNUCU fiyatından (istemci UnitPrice yok sayılır)"*). Bu aynı zamanda bir
güvenlik boşluğunu da kapatır: bugün istemci sepete istediği fiyatı yazabiliyor (para kaybı yok, çünkü
checkout yeniden hesaplıyor — ama sepet ekranı yanlış tutar gösteriyor).

---

## 2. Ürün detayı — `GET /api/store/catalog/products/{code}`

| İstek | Durum | Not |
|---|---|---|
| Ürün düzeyinde `price` / `compareAtPrice` | ✅ **ZATEN VAR** (A10 + B9) | Staging'de yoktu |
| `descriptionI18n` | ✅ **ZATEN VAR** | Staging'de yoktu |
| Liste ↔ detay `compareAtPrice` farkı (P-00021624: 799,99 ↔ 599,99) | ✅ **ÇÖZÜLDÜ — sebep cache DEĞİL, KURAL FARKIYDI** | Liste ürünün varyantlarındaki **en yüksek** çizili fiyatı alıyordu; detay yalnız **en ucuz varyantın** çizili fiyatına bakıyordu. O varyantta referans yoksa detay indirimi hiç göstermiyor, başka varyant seçilince farklı değer çıkıyordu. Detay (ve site SSR detayı) artık listeyle aynı kuralı kullanıyor |
| `variants[].compareAtPrice` her varyantta dolu | ✅ **UYGULANDI (MK4)** | Varyantın kendi kanal çizili fiyatı; yoksa ürün düzeyi referans kopyalanır — yalnız o varyantın satış fiyatından büyükse |
| `variants[].variantInfo` ("Renk: Krem, Beden: S") | ✅ **UYGULANDI** | Metin artık TEK kuraldan (`VaryantSecenekMetni`): sepet `optionsText` ve detay `variantInfo` aynı. İç filtre ekseni (`filtre_rengi`) metinden çıkarıldı ("Beden: 44, Filtre Rengi: Lacivert, Renk: Lacivert" üretiliyordu), sıra renk → beden |
| `attributes[]` içinde "Kategori Grubu" + "Stok Durumu" | ✅ **UYGULANDI** | `kategori_grubu` ve `stok_durumu` satırları eklendi (tr/en). Stok satırı yoksa "Tükendi" yazılmaz — ürünün satış durumu esas alınır |

---

## 3. `POST /api/store/checkout/preview`

| İstek | Durum |
|---|---|
| Yeni ön izleme ucu | ✅ **UYGULANDI (M2, 2026-09-09)** — `POST /api/store/checkout/preview`; kalemler SEPETTEN okunur, istemci fiyat göndermez. Aritmetik `SiparisTutarKurali`'nda TEK yerde: checkout da aynı kuralı çağırır, iki yol ayrışamaz. İzole doğrulama: ön izleme 993,90 → sipariş 993,90; kapıda ödemede 1.043,90 → 1.043,90 |
| `items[].unitPrice` sunucuda doğrulansın | ✅ **ZATEN VAR** (F4, 2026-07-31) — istemci fiyatı yok sayılıyor |
| `couponDiscount` sunucuda doğrulansın | ✅ **ZATEN VAR** (9.4, 2026-08-27) — kod yeniden doğrulanıyor, tutar sunucuda hesaplanıyor |

Ön izleme ucu doğru istek: bugün mobil, ödeme ekranındaki tutarı kendi hesaplamak zorunda; checkout
sunucuda başka bir tutar bulursa kullanıcı sürprizle karşılaşıyor. Aynı hesap tek yerden çağrılacak.

---

## 4. Durum kodları → etiket

Bugün store uçları **ham kod** döndürüyor (`pending`, `shipped`…); etiket/renk eşlemesi yalnız admin
panelinde var. Mobilin "tek uç" tercihi doğru: her yanıta etiket gömmek sözleşmeyi şişirir ve
istemciler arasında tutarsızlaşır.

🔨 Yeni iş: `GET /api/store/lookups` — kod → `{label, color}` sözlüğü, **versiyonlu** (istemci cache'ler).
Sipariş `timeline`, `canCancel` / `canReturn` / `canReview` bayrakları ve `payment-options`'ın
`methods:[{code,label,fee}]` biçimi ayrı kalemler.

### ✅ UYGULANDI (2026-09-09) — sözleşme ve alınan karar

Tek kural dosyası: **`Shared.Contracts/DurumEtiketleri.cs`** (M2'deki `SiparisTutarKurali`, M3'teki
`VaryantSecenekMetni` kalıbı). Etiket metni artık dört yüzeyde tek yerden gelir: `lookups` ucu, store/mobil
uçlarının satır alanları, sitenin SSR Hesabım sayfası ve panelin Excel export'u.

**★ Uygulama sırasında çıkan gerçek: İKİ ETİKET SETİ var, bilinçli olarak ayrı tutuldu.**
Panel ile sitenin bugünkü metinleri ÖRTÜŞMÜYOR ve örtüşmemeli:

| Kod | Panel (operasyon) | Vitrin (müşteri / mobil) |
|---|---|---|
| `pending` | Bekleyen | **Sipariş Alındı** |
| `confirmed` | Onaylı | **Sipariş Alındı** |
| `processing` | İşlemde | Hazırlanıyor |
| `shipped` | Kargoda | Kargoda |
| `delivered` | Teslim | Teslim Edildi |
| `cancelled` | İptal | İptal Edildi |

Müşteri iç onay adımını görmez (`pending`/`confirmed` tek etikete iner), personel ise ikisini ayırmak
zorundadır. Bu yüzden dosyada `DurumEtiketleri.Panel.*` ve `DurumEtiketleri.Vitrin.*` ayrı; store/mobil
uçları **her zaman Vitrin**'i döner. Ayrımın kazara silinmesini `DurumEtiketleriTests` engelliyor.

**Uçlar:**

- `GET /api/store/lookups` (anonim) — `{version, families{…}, timelines{…}}`. `version` katalog
  içeriğinin SHA256'sından türer (elle sayı artırma yok), `ETag` olarak da döner: istemci
  `If-None-Match` ile **304** alır. `Cache-Control: public, max-age=3600`.
  Aileler: `orderStatus`, `paymentStatus`, `paymentMethod`, `returnStatus`, `reviewStatus`, `questionStatus`.
- Sipariş liste/detay + `orders/track`: `statusLabel`, `statusColor` (hex), `statusVariant`
  (success/warning/danger/neutral/info), `paymentStatusLabel`, `paymentMethodLabel`.
- Sipariş detay/liste: `canCancel` / `canReturn` / `canReview` — komutların dayattığı kuralın aynısı
  (iptal `pending`+`confirmed`, iade ve yorum yalnız `delivered`). Misafir takibinde bayrak YOK (üyelik ister).
- Sipariş detay + `orders/track`: `timeline: [{code,label,done,current}]`, 4 adım.
  **İptal edilen siparişte `timeline` BOŞ dizidir** (tasarımda iptal akışı yok, site de şeridi gizler) —
  istemci şeridi çizmez, durumu `statusLabel`'dan gösterir.
- İade liste/detay: `statusLabel` + iade akışı `timeline` (reddedilen iadede boş).
- `reviews/mine`, `questions/mine`: `statusLabel`/`statusColor`/`statusVariant`.
- `GET /payment-options`: `methods: [{code,label,description,fee,maxOrderTotal}]`.
  ⚠ Eski düz kod dizisi **`methodCodes`** adıyla korundu (geriye dönük güvenlik; bu ucu web kullanmıyor).

**Sözlük mü satır alanı mı?** İkisi de var. Mobil "tek uç" tercih etti ve `lookups` o uç; satırdaki
`statusLabel` ise istemcinin sözlüğü henüz çekmediği ilk açılış/çevrimdışı durum için. İkisi aynı kaynaktan
üretildiği için ayrışamazlar.

---

## 5. `discountPercent`

🔨 Yeni iş, ucuz: `KartFiyatGorunumu`'ndaki `price`/`compareAtPrice`'tan türetilir; tek yerde hesaplanıp
liste/arama/detay/favori/gezilen/koleksiyon/sepet satırına eklenir.

---

## 6. Küçük maddeler

| İstek | Durum | Not |
|---|---|---|
| `cms/pages/by-slug/sss` → 404 | 🐞 **Doğrulandı, kolay** | Gerçek slug `sik-sorulan-sorular` (kod `kurumsal-sss`). Alias eklenecek |
| Misafir `viewed-products` → 403 | ⚠️ **Karar gerek** | Uç `[Authorize(MemberOnly)]`. Cihaz token'ına açmak = gezinme geçmişini üyeliksiz saklamak (B2 "misafir gezinme" maddesiyle aynı karar) |
| `payment/paytr/init` zarfı | ✅ **ZATEN VAR** — `{success, data:{html}}` (kök `html` geriye dönük uyumluluk için duruyor) | Staging'de yoktu |
| `reviews/reviewable` nesne dönsün | ✅ **ZATEN VAR** — `{productCode, productName, imageUrl, variantInfo, orderNumber, …}` | Staging'de yoktu |
| Eski fiyat alanlarının kaldırılması (`minPrice`, `basePrice`, `productId`, `campaignPrice`) | ⚠️ **Karar gerek** | B9'da "mobil geçince kaldırılacak" denmişti; web SSR ve panel de okuyor olabilir — kaldırma tarihi belirlenmeli |
| Adreslerde `cityId`/`districtId`/`neighborhoodId` dolu olsun | 🐞 **Gerçek eksik** | Canlı: 97 adresin 97'sinde şehir, 96'sında ilçe, **67'sinde mahalle** var. Alanlar yanıtta var; **web adres formu mahalleyi zorunlu tutmuyor**. Düzeltme formda + eski kayıtlar için eşleştirme |

---

## Önerilen sıra

| Faz | İçerik | Neden bu sırada |
|---|---|---|
| **M0** | Staging'i canlıyla eşitle (yapıldı, restart bekliyor) | Listenin bir kısmı bununla kapanır; ölçüm zemini düzelir |
| **M1** | Sepet fiyat sözleşmesi: sunucu fiyatı + çifte indirimin kapanması + `total` + çözülmüş kargo + satır fiyat alanları | Tek gerçek hata burada; para/tutar görünen yer |
| **M2** ✅ | *(2026-09-09 uygulandı)* `POST /api/store/checkout/preview` + tutar aritmetiğinin `SiparisTutarKurali`'na çıkarılması | M1'in hesabını yeniden kullanır |
| **M3** ✅ | *(2026-09-09 uygulandı)* Ürün detayı: `variantInfo`, öznitelik satırları, liste↔detay `compareAtPrice` tutarlılığı, varyant çizili fiyatı (MK4), sepette de ürün düzeyi referans | Bağımsız |
| **M4** ✅ | *(2026-09-09 uygulandı)* `GET /api/store/lookups` (versiyonlu, ETag'li) + sipariş/iade `statusLabel`+`statusColor`+`timeline`+`canCancel`/`canReturn`/`canReview` + `payment-options` etiketli `methods[]` + `reviews/mine`·`questions/mine` etiketleri | Sözleşme genişlemesi |
| **M5** | `discountPercent` + küçük maddeler (sss alias, adres mahalle) | Ucuz kapanışlar |

---

## Kullanıcı kararları (2026-09-09) — bağlayıcı

| # | Konu | Karar |
|---|---|---|
| **MK1** | `campaignDiscount` anlamı | **Değişmiyor.** Toplam indirim için sepete **yeni `totalDiscount`** alanı eklenir (`campaignDiscount` + satır kampanyaları + kupon). Web istemcisi kırılmaz, mobil yeni alanı kullanır |
| **MK2** | Misafir `viewed-products` | **Açılır** — cihaz token'ıyla erişilir; üye olunca geçmiş üyeye devredilir |
| **MK3** | Eski fiyat alanları (`minPrice`, `basePrice`, `productId`, `campaignPrice`) | **Şimdilik kalır**, "kaldırılacak" olarak işaretlenir; mobil yeni sürüm yayınlayıp web tarandıktan sonra kaldırılır |
| **MK4** | `variants[].compareAtPrice` | Varyantın **kendi** kanal çizili fiyatı; yoksa **ürün düzeyindeki referans kopyalanır** → her varyantta dolu |
