# Backend İstekleri — Mobildeki Geçici Çözümlerin Kaldırılması

> Bu belge backend ekibine olduğu gibi iletilebilir. Her madde: mobil
> uygulamanın bugün **zorla / dolaylı yoldan** çözdüğü bir durum, backend'in
> yapması istenen değişiklik ve backend düzeltince mobilden **silinecek** kod.
> Amaç: uygulamada hiçbir "tahmin / eşleştirme / ikinci istek" kalmaması —
> veri neyse ekranda o.
>
> Ortam: staging `http://51.178.208.59:5055` (tüm maddeler 2026-09-07'de
> yeniden doğrulandı). Doğrulama betikleri: `tools/probes/probe_audit.mjs`.

Öncelik: **A** = kullanıcı görüyor / yanlış veri riski, **B** = gereksiz
istek/performans, **C** = temizlik.

---

## A1. Sipariş kaleminde ürün görseli ve ürün kodu yok
- **Uç:** `GET /api/store/account/orders/{orderId}` → `items[]`
- **Bugün gelen:** `id, variantId, sku, productName, variantInfo, quantity, unitPrice, discountAmount, taxAmount, total, status`
- **Mobilin zorlaması:** kalemin görselini bulmak için **ürün ADIYLA katalog araması** yapıyor (`orderLineCandidatesProvider`), aynı ad birden çok üründe olduğundan `variantInfo`'daki "Renk: X" metnine göre aday seçiyor, sonra ürün detayını çekip `variantId` ile varyant görselini buluyor. Sipariş başına 2–3 ek istek + tahmin.
- **İstenen:** her kalemde `productCode`, `imageUrl` (varyantın görseli), tercihen `colorValueId`.
- **Silinecek:** `lib/features/orders/order_line_product_provider.dart` (tamamı), `order_detail_screen.dart` `_LineCard` içindeki aday/renk eşleme bloğu, `reviewableVariantMapProvider` (activity_providers.dart).

## A2. Renk adları tutarsız yazılıyor ("Siyah Bej" / "Siyahbej" / "Koyugri")
- **Uç:** ürün detayı `variants[].attributes[]` (`renk`) ve sipariş/sepet `variantInfo`, `optionsText`
- **Bugün gelen:** aynı renk farklı yerlerde farklı yazımla: `Koyu Kahve` ama `Koyugri`, `Siyah Taba` ama `Siyahbeyaz`, `Açık Kiremit` ama `Açıkhaki`.
- **Mobilin zorlaması:** `colorKey()` — küçük harf + boşluk/işaret silerek karşılaştırma (`order_line_product_provider.dart`). 60 varyant rengi tarandı, çakışma yok ama bu bir tahmin.
- **İstenen:** renk adları tek yazımla (katalogda normalize edilsin) ve metin yerine **kimlik** verilsin: sepet/sipariş kalemlerinde `colorValueId` + `sizeValueId`.
- **Silinecek:** `colorKey()`, `colorNameFromVariantInfo()`, product_detail_screen `initialColorName` yolu.

## A3. `renk` özelliği `isColor:false` geliyor (ürün bazlı veri hatası)
- **Uç:** `GET /catalog/products/{code}` → `variants[].attributes[]`
- **Örnek:** P-00022767 (Kul-26086): `{"attributeTypeCode":"renk","isColor":false}`; liste kartında `colors: []`.
- **Sonuç:** renk seçici hiç çıkmıyor, listeden seçilen renk yerine ilk varyant açılıyordu.
- **Mobilin zorlaması:** `ProductVariant.fromJson` — `attributeTypeCode == 'renk'` ise `isColor` bayrağına bakmadan renk sayıyor.
- **İstenen:** renk tipi özelliklerde `isColor:true` garanti edilsin; liste `colors[]` boş gelmesin.
- **Silinecek:** product_detail.dart'taki `|| typeCode == 'renk'` koşulu.

## A4. Checkout sonrası sepet boşalmıyor
- **Uç:** `POST /api/store/checkout` (başarılı) → ardından `GET /cart`
- **Kanıt:** 2026-09-07 test hesabı, MIS0000059 (kapıda nakit, iptal edildi): sipariş sonrası sepette aynı kalem duruyordu. Üye sepeti üyeye bağlı (`sessionId`'den bağımsız), bu yüzden mobilin "yeni oturum" yöntemi üyede işe yaramıyor.
- **Mobilin zorlaması:** `CartController.clearAfterOrder` — siparişe giren kalemleri `DELETE /cart/{id}/items/{itemId}` ile tek tek siliyor.
- **İstenen:** checkout başarılıysa (kapıda: anında; kartta: ödeme onayında) `cartId` kalemlerini sunucu temizlesin / sepeti "converted" işaretlesin.
- **Silinecek:** `clearAfterOrder` içindeki silme döngüsü (yalnız `startFreshSession` kalır).

## A5. Ürün slug → kod çevrimi yok (deep link)
- **Uç:** sitedeki ürün linkleri slug'lı (`/urun/kadin-...-1475253`); mobil API yalnız `P-xxxxx` kodu kabul ediyor. Liste item'ında `slug` alanı da yok.
- **Mobilin zorlaması:** yok — slug'lı ürün linki **arama yedeğine düşüyor** (yanlış/eksik sonuç riski). Kategori slug'ları için client'ta 202 kayıtlık slug→id indeksi kuruluyor (`slugCategoryIndexProvider`).
- **İstenen:** `GET /catalog/products/{code-or-slug}` slug'ı da kabul etsin **veya** `GET /catalog/products/by-slug?slug=`; liste item'ına `slug` eklensin. Kategori uçları da slug ile açılabilsin.
- **Silinecek:** `slugCategoryIndexProvider` (kategori), ürün listesi ekranındaki slug→arama yedeği.

## A6. Sepet, sipariş ve iade akışlarında `firmPlatformId` bazen gövdede, bazen query'de
- **Uçlar:** `POST /cart/items`, `POST /checkout`, `POST /favorites`, `POST /stock-alerts`, `POST /reviews`, `POST /questions`, `POST /collections` → **gövdede** zorunlu; `GET`'ler ve `DELETE /collections/{id}` → **query**'de zorunlu (yoksa 400 "Koleksiyon bulunamadı").
- **Mobilin zorlaması:** interceptor her isteğe query ekliyor **ve** her POST gövdesine elle `firmPlatformId` yazılıyor.
- **İstenen:** tek kural — ya her yerde query/başlık (tercihen `X-Firm-Platform` başlığı) ya da hiçbirinde (device token zaten kanalı biliyor).
- **Silinecek:** tüm repository'lerdeki `'firmPlatformId': _api.firmPlatformIdProvider?.call()` satırları.

## A7. Sepet yokken `GET /cart` `data`'sız yanıt
- **Bugün gelen:** `{"success":true}` (data alanı hiç yok; eskiden 404'tü, 2026-09-04'te değişti).
- **Mobilin zorlaması:** `getCart` null `data`'yı boş sepet sayıyor; genel zarf kuralı ("data her zaman var") bozuluyor.
- **İstenen:** boş sepet için `{"success":true,"data":{"items":[],"subtotal":0,...}}`.
- **Silinecek:** cart_repository.dart'taki null/404 toleransı.

## A8. Arama ucu tükenmiş renkleri elemiyor (kategori ucu eliyor)
- **Uçlar:** `GET /catalog/products?search=` vs `GET /catalog/channel-categories/{id}/products`
- **Kanıt:** `search=hırka` → 48 sonucun 35'inde en az bir renk `inStock:false`; aynı filtreyle kategori ucu bunları eliyor.
- **Mobilin zorlaması:** kartta "Tükendi" overlay'i (`ProductColor.inStock`).
- **İstenen:** genel/arama ucu da kategori ucuyla aynı stok kuralını uygulasın.
- **Silinecek:** overlay güvenlik ağı olarak kalabilir; sorgu düzeyinde tutarlılık istenen.

## A9. Ödeme yöntemleri ucu yok
- **Uç:** `GET /api/store/payment-options` → **404**
- **Mobilin zorlaması:** yöntemler ve kapıda ödeme bedeli/limiti uygulamaya gömülü config'ten (`payment.methods/codFee/codLimit`); panel değişikliği mobile ancak checkout'ta 400 olarak yansıyor.
- **İstenen:** `{"success":true,"data":{"methods":["kart","kapida-nakit","kapida-kart"],"codFee":50,"codLimit":3000}}` (mobil parser bu şekle hazır ve testli).
- **Silinecek:** `checkout_models.dart` fallback zinciri, config'teki `payment.*` gösterim değerleri.

## A10. Ürün detayı indirimli fiyat / çizgili fiyat / açıklama / grup kimliği vermiyor
- **Uç:** `GET /catalog/products/{code}` → alanlar yalnız `id, code, nameI18n, isActive, variants(basePrice), attributes, productGroupNameI18n`
- **Mobilin zorlaması:** indirim bilgisi listeden taşınan "önizleme" ile gösteriliyor; deep link'te önizleme olmadığından **indirim görünmüyor, ham basePrice çıkıyor**. Açıklama alanı hiç yok; "benzer ürünler" `productGroupId` olmadığı için yapılamıyor.
- **İstenen:** detaya `minPrice`, `compareAtPrice` (varyant bazında da), `descriptionI18n`, `productGroupId`, `slug`.
- **Silinecek:** product_detail_screen `_priceInfo` önizleme önceliği; favori/koleksiyon kartlarında `compareAtPrice: null` sabitleri.

## A11. `reviews/reviewable` düz kod listesi
- **Uç:** `GET /api/store/reviews/reviewable` → `["P-00012830","P-00016853"]`
- **Mobilin zorlaması:** her kod için ürün detayı çekip ad/görsel tamamlıyor (`_PendingRow`); sipariş satırındaki "Değerlendir" için varyant→kod eşlemesi (A1 ile aynı kaynak).
- **İstenen:** `[{productCode, productName, imageUrl, variantInfo, orderNumber?}]`.
- **Silinecek:** `ReviewableProduct.code` ctor yolu, `_PendingRow` katalog tamamlama.

## B1. "Yalnız kod dönen" listeler → mobilde N+1 ürün detayı çağrısı
Aynı desen dört uçta; her satır için ayrı `GET /catalog/products/{code}`:
| Uç | Bugün gelen | İstenen ek alanlar | Mobil tamamlayıcı (silinecek) |
|---|---|---|---|
| `GET /favorites` | `{productCode, colorValueId}` | `productName, imageUrl (renge göre), minPrice, compareAtPrice` | `favoriteCardsProvider` (favorites_providers.dart) |
| `GET /viewed-products` | `{productCode, viewedAt}` | `productName, imageUrl, minPrice` | `_ViewedRow` (viewed_products_screen.dart) |
| `GET /reviews/mine` | `{id, productCode, rating, text, status, createdAt}` | `productName, imageUrl` | `_ReviewCard` katalog tamamlama (my_reviews_screen.dart) |
| `GET /questions/mine` | `{id, productCode, question, status, ...}` | `productName, imageUrl` | my_questions_screen.dart katalog tamamlama |
| `GET /collections` → `itemCodes[]` | yalnız kodlar | `items[{productCode, productName, imageUrl, minPrice}]` | `collectionCardsProvider` |

## B2. Mobil gezinmeler kaydedilmiyor
- **Uç:** `POST /api/store/viewed-products` → **405**; üye JWT'li detay GET'i kayıt düşürmüyor; `POST /events product_viewed` 200 dönüyor ama listeye yazmıyor. Misafir (device token) `GET /viewed-products` → 403.
- **Mobilin zorlaması:** ürün detayı açılınca `POST /viewed-products {firmPlatformId, productCode}` gönderiyor, 405'i sessizce yutuyor ("Önceden Gezdiklerim" mobilden hiç dolmuyor).
- **İstenen:** bu POST'u açın (ya da `product_viewed` event'i listeye yazsın); misafir için device token ile okuma.
- **Silinecek:** `recordView` içindeki hata yutma (normal hata akışına döner).

## B3. Stok alarmını geri alma ucu yok
- **Denenenler (2026-09-07):** `DELETE /stock-alerts/{alertId}` 404, `DELETE /stock-alerts?variantId=` 405, `POST /stock-alerts/unsubscribe` 404.
- **Mobilin zorlaması:** "Haber vereceğiz" düğmesi dokunulamaz; kullanıcı vazgeçemiyor.
- **İstenen:** `DELETE /stock-alerts/{alertId}` (POST zaten `alertId` döndürüyor).

## B4. Webden kaydedilen adreslerde il/ilçe/mahalle kimlikleri boş
- **Uç:** `GET /account/addresses` — web formundan gelen kayıtlarda `cityId/districtId/neighborhoodId` boş, yalnız adlar dolu (mobil/test kayıtlarında tam).
- **Mobilin zorlaması:** `_resolveGeoIds` — adları geo uçlarından Türkçe-duyarsız eşleştirip kimlik buluyor (adres başına 3 istek + tahmin).
- **İstenen:** adres kaydında kimlikler her zaman yazılsın (web formu düzeltilsin / mevcut kayıtlar migrasyonla doldurulsun).
- **Silinecek:** checkout_screen `_resolveGeoIds` ve kargo kartındaki "adresi düzenle" uyarısı.

## B5. Liste item şeması iki uçta farklı
- `GET /catalog/products` → `id` + `minPrice`; `GET /channel-categories/{id}/products` → `productId` + `basePrice`.
- **Mobilin zorlaması:** `ProductCard.fromJson` iki ada da toleranslı (`id ← productId`, `minPrice ← basePrice`).
- **İstenen:** iki uç aynı DTO'yu döndürsün.

## B6. `paytr/init` yanıt zarfı standart dışı
- **Bugün gelen:** `{success, html}` (`data` alanı yok).
- **Mobilin zorlaması:** yalnız bu uç için `ApiClient.postRaw` (zarf açmayan özel yol).
- **İstenen:** `{success:true, data:{html}}`.
- **Silinecek:** `postRaw`.

## B7. CMS sayfa slug'ları site linkleriyle birebir değil
- **Örnek:** site `/gizlilik-ve-guvenlik` ↔ CMS `gizlilik-guvenlik` ↔ sayfa `kurumsal-...`; `sss` ↔ `sik-sorulan-sorular`.
- **Mobilin zorlaması:** `cmsKey()` normalizasyonu (tire/“ve”/“kurumsal-” temizleme + özel eşler).
- **İstenen:** CMS sayfalarında `slug` alanı sitedeki URL ile aynı olsun (veya `aliases[]`).
- **Silinecek:** `cms_repository.dart` `cmsKey()`.

## B8. Ana sayfa yerleşim adı dökümanla uyuşmuyor
- Döküman `home`, canlı `homepage` (`GET /pages/homepage`). Arama sayfası için yerleşim yok (`pages/search` → "Geçersiz yerleşim") — popüler ürünler için kaynak yok.
- **İstenen:** dökümanı düzeltin; `pages/search` yerleşimi açılırsa mobil mevcut blok motoruyla panelden yönetilir hale gelir.


## C2. Misafir sipariş takibi ucu yok
- `orders/track`, `orders/lookup` → 404. Misafir, sipariş no + e-posta ile siparişini göremiyor.

## C3. Sadakat
- `GET /account/loyalty` → 400 "Sadakat hesabı bulunamadı" (test hesaplarında). Ekran yapılmadı; uç davranışı netleşince eklenir.

## C4. Play Integrity / App Attest (prod)
- Prod'da gerçek cihaz doğrulaması yapılandırılmadı; paket adı **com.misharix.misharitalia** (GCP servis hesabı + Play Console "Uygulama bütünlüğü" bu ada bağlanmalı). Staging'de DevBypass ile çalışılıyor.

---

## Backend düzeltince mobilde yapılacaklar (kontrol listesi)
- [ ] A1+A11 → `order_line_product_provider.dart` sil, `reviewableVariantMapProvider` sil, `_LineCard` sadeleştir.
- [ ] A2 → `colorKey/colorNameFromVariantInfo` sil; rota `?color=` parametresini kaldır (yalnız `?variant=` kalır).
- [ ] A3 → `|| typeCode == 'renk'` kaldır.
- [ ] A4 → `clearAfterOrder` silme döngüsünü kaldır.
- [ ] A5 → slug indeksini kaldır, deep link doğrudan detaya.
- [ ] A6 → repository'lerdeki gövde `firmPlatformId` satırlarını kaldır.
- [ ] A7 → `getCart` null toleransını kaldır.
- [ ] A9 → `payment.*` config fallback'ini kaldır.
- [ ] A10 → önizleme fiyat önceliğini kaldır, açıklama + benzer ürünler bölümlerini aç.
- [ ] B1 → beş ekrandaki katalog tamamlama kodlarını kaldır.
- [ ] B4 → `_resolveGeoIds` kaldır.
- [ ] B6 → `postRaw` kaldır.
- [ ] B7 → `cmsKey` kaldır.
