# E-Ticaret Sitesi  
## Reklam, Analytics ve Sosyal Medya Entegrasyonları

### 1. Amaç

Bu dokümanın amacı, bir e-ticaret yönetim panelinde reklam, analiz, dönüşüm takibi, remarketing ve ürün reklamları için gerekli entegrasyon bilgilerinin standartlaştırılmasıdır.

Sistem aşağıdaki platformlarla entegre çalışabilecek şekilde tasarlanmalıdır:

- Google Analytics 4
- Google Tag Manager
- Google Ads
- Google Merchant Center
- Google Search Console
- Meta / Facebook
- Instagram
- TikTok
- Pinterest
- Microsoft Ads
- Microsoft Clarity
- Ürün Feed sistemleri
- Consent / Cookie Management

---

# 2. Yönetim Paneli Menü Yapısı

Önerilen yönetim paneli yapısı:

```text
Pazarlama
├── Genel Ayarlar
├── Google Analytics
├── Google Tag Manager
├── Google Ads
├── Google Merchant Center
├── Google Search Console
├── Meta / Facebook
├── Instagram
├── TikTok
├── Pinterest
├── Microsoft Ads
├── Microsoft Clarity
├── Ürün Feedleri
├── Dönüşüm Olayları
└── Consent / Cookie Yönetimi
```

Her entegrasyon bağımsız olarak aktif/pasif yapılabilmelidir.

---

# 3. Google Analytics 4

Google Analytics 4, ziyaretçi davranışlarının ve e-ticaret dönüşümlerinin ölçülmesi için kullanılacaktır.

### Panelde İstenecek Bilgiler

| Alan | Açıklama | Örnek |
|---|---|---|
| Aktif | Entegrasyonu açar/kapatır | Evet |
| Measurement ID | GA4 Measurement ID | `G-XXXXXXXXXX` |

### Gönderilecek Temel Event'ler

```text
page_view
view_item
view_item_list
search
add_to_cart
remove_from_cart
view_cart
begin_checkout
add_shipping_info
add_payment_info
purchase
sign_up
login
add_to_wishlist
```

---

# 4. Google Tag Manager

Google Tag Manager, üçüncü taraf takip kodlarının merkezi olarak yönetilmesi için kullanılabilir.

### Panelde İstenecek Bilgiler

| Alan | Açıklama | Örnek |
|---|---|---|
| Aktif | GTM entegrasyon durumu | Evet |
| Container ID | Google Tag Manager Container ID | `GTM-XXXXXXX` |

GTM kullanılması durumunda `dataLayer` yapısının e-ticaret event'lerini desteklemesi önerilir.

---

# 5. Google Ads

Google Ads entegrasyonu satış, sepet, checkout ve diğer dönüşümlerin reklam kampanyalarına aktarılmasını sağlar.

### Panelde İstenecek Bilgiler

| Alan | Örnek |
|---|---|
| Aktif | Evet |
| Conversion ID | `AW-123456789` |
| Purchase Conversion Label | `AbCdEf123` |
| Add To Cart Conversion Label | `Xyz123` |
| Begin Checkout Conversion Label | `Checkout123` |

### Önerilen Dönüşümler

- Purchase
- Add To Cart
- Begin Checkout
- Sign Up
- Lead
- Contact

Satın alma dönüşümünde sipariş numarası, para birimi ve sipariş tutarı mutlaka aktarılmalıdır.

---

# 6. Google Merchant Center

Merchant Center, ürünlerin Google Shopping ve diğer Google ürün reklamlarında kullanılmasını sağlar.

### Panelde İstenecek Bilgiler

| Alan | Örnek |
|---|---|
| Aktif | Evet |
| Merchant ID | `123456789` |
| Feed Country | `DE` |
| Feed Language | `de` |
| Currency | `EUR` |

### Ürün Feed URL

Sistem otomatik bir XML feed oluşturabilir:

```text
/feeds/google-shopping.xml
```

Feed içerisinde en az aşağıdaki ürün bilgileri bulunmalıdır:

- Product ID
- Title
- Description
- Product URL
- Image URL
- Availability
- Price
- Sale Price
- Brand
- GTIN / EAN
- MPN
- Condition
- Google Product Category

---

# 7. Google Search Console

Search Console, sitenin Google arama sonuçlarındaki performansının ve indeksleme durumunun takibi için kullanılabilir.

### Panelde İstenecek Bilgiler

| Alan | Açıklama |
|---|---|
| Aktif | Entegrasyon durumu |
| Verification Code | Google doğrulama kodu |

Örnek:

```text
google-site-verification=XXXXXXXXXXXX
```

---

# 8. Meta / Facebook

Meta entegrasyonu Facebook ve Instagram reklamlarının dönüşüm takibi için kullanılacaktır.

İki farklı yöntem desteklenmelidir:

### Browser Side

Meta Pixel

### Server Side

Meta Conversion API

### Panelde İstenecek Bilgiler

| Alan | Açıklama |
|---|---|
| Aktif | Meta entegrasyonu |
| Pixel / Dataset ID | Meta Pixel ID |
| Conversion API | Aktif/Pasif |
| Access Token | CAPI erişim tokeni |
| Test Event Code | Test işlemleri için opsiyonel |

Örnek Pixel ID:

```text
123456789012345
```

### Meta'ya Gönderilecek Event'ler

```text
PageView
ViewContent
Search
AddToCart
AddToWishlist
InitiateCheckout
AddPaymentInfo
Purchase
CompleteRegistration
```

Özellikle `Purchase` event'i mümkünse hem browser hem server tarafından desteklenmelidir.

Aynı işlemin iki kez sayılmasını önlemek için ortak `event_id` kullanılarak deduplication yapılmalıdır.

---

# 9. Instagram

Instagram reklam yönetimi Meta altyapısı üzerinden gerçekleştirilmelidir.

Kullanıcıdan doğrudan Instagram kullanıcı adı ve şifresi istenmemelidir.

Tercih edilen yöntem:

```text
Meta ile Bağlan
```

OAuth üzerinden gerekli Business Account ve reklam hesabı izinleri alınmalıdır.

---

# 10. TikTok

TikTok reklam dönüşümlerinin ölçülmesi için TikTok Pixel ve server-side Events API desteklenebilir.

### Panelde İstenecek Bilgiler

| Alan | Açıklama |
|---|---|
| Aktif | TikTok entegrasyonu |
| Pixel ID | TikTok Pixel |
| Events API | Aktif/Pasif |
| Access Token | Server-side erişim tokeni |

### Event'ler

```text
ViewContent
Search
AddToCart
AddToWishlist
InitiateCheckout
AddPaymentInfo
CompletePayment
CompleteRegistration
```

---

# 11. Pinterest

Pinterest reklam ve dönüşüm takibi kullanılacaksa Pinterest Tag entegrasyonu eklenebilir.

### Panelde İstenecek Bilgiler

| Alan | Açıklama |
|---|---|
| Aktif | Pinterest entegrasyonu |
| Pinterest Tag ID | Takip kodu |
| Conversion API Token | Server-side entegrasyon |

---

# 12. Microsoft Ads

Microsoft/Bing reklamlarının dönüşüm takibi için UET kullanılabilir.

### Panelde İstenecek Bilgiler

| Alan | Örnek |
|---|---|
| Aktif | Evet |
| UET Tag ID | `12345678` |

E-ticaret dönüşümleri ayrıca Microsoft Ads tarafına aktarılabilir.

---

# 13. Microsoft Clarity

Kullanıcı davranışlarının analiz edilmesi için Microsoft Clarity opsiyonel olarak desteklenebilir.

### Panelde İstenecek Bilgiler

| Alan | Örnek |
|---|---|
| Aktif | Evet |
| Project ID | `abcdefghij` |

Clarity özellikle aşağıdaki analizlerde kullanılabilir:

- Session Recording
- Heatmap
- Rage Click
- Dead Click
- Scroll davranışı

---

# 14. Merkezi E-Ticaret Event Sistemi

Her reklam platformu için ayrı ayrı e-ticaret mantığı geliştirmek yerine merkezi bir event sistemi oluşturulması önerilir.

Örneğin:

```text
Ecommerce Event
        │
        ├── Google Analytics
        ├── Google Ads
        ├── Meta Pixel
        ├── Meta Conversion API
        ├── TikTok Pixel
        ├── TikTok Events API
        └── Microsoft Ads
```

Uygulama içerisinde örneğin:

```text
ProductViewed
ProductAddedToCart
CheckoutStarted
PaymentInfoAdded
OrderCompleted
```

gibi internal event'ler oluşturulabilir.

Bu event'ler ilgili platformların event formatlarına dönüştürülür.

---

# 15. Purchase Event Veri Modeli

Satın alma işlemi aşağıdaki temel bilgileri içermelidir:

```json
{
  "event": "purchase",
  "transaction_id": "ORD-202600123",
  "value": 1499.90,
  "currency": "EUR",
  "items": [
    {
      "item_id": "PRODUCT-12345",
      "item_name": "Ürün Adı",
      "price": 1499.90,
      "quantity": 1
    }
  ]
}
```

Ek olarak aşağıdaki bilgiler de desteklenebilir:

```text
coupon
discount
shipping
tax
brand
category
variant
quantity
customer_type
```

---

# 16. Client-Side ve Server-Side Tracking

Modern bir e-ticaret sisteminde yalnızca JavaScript tabanlı tracking'e bağımlı kalınmaması önerilir.

### Client-Side

Tarayıcı üzerinden:

```text
GA4
Google Ads
Meta Pixel
TikTok Pixel
GTM
```

### Server-Side

Backend üzerinden:

```text
Meta Conversion API
TikTok Events API
Google server-side çözümleri
```

Özellikle gerçek satış bilgisinin backend tarafından doğrulanarak gönderilmesi veri kalitesini artırır.

---

# 17. Güvenlik

Aşağıdaki bilgiler **Secret** kabul edilmelidir:

```text
Meta Access Token
TikTok Access Token
API Secret
Client Secret
Refresh Token
OAuth Token
```

Bu bilgiler kesinlikle frontend JavaScript koduna gönderilmemelidir.

Backend/veritabanı tarafında güvenli ve tercihen şifrelenmiş olarak saklanmalıdır.

Yönetim panelinde:

```text
Access Token
••••••••••••••••••••••
```

şeklinde maskelenmelidir.

Log kayıtlarına secret/token yazılmamalıdır.

---

# 18. Kullanıcı Adı ve Şifre İstenmemeli

Aşağıdaki bilgilerin panel tarafından istenmesi önerilmez:

```text
Google kullanıcı adı
Google şifresi

Facebook kullanıcı adı
Facebook şifresi

Instagram kullanıcı adı
Instagram şifresi

TikTok kullanıcı adı
TikTok şifresi
```

Bunun yerine OAuth kullanılmalıdır.

Örneğin:

```text
[ Google ile Bağlan ]

[ Meta ile Bağlan ]

[ TikTok ile Bağlan ]
```

Kullanıcı ilgili platforma yönlendirilerek gerekli yetkileri verir.

---

# 19. Tracking Entegrasyonu ile Reklam Yönetiminin Ayrılması

Burada önemli bir mimari ayrım yapılmalıdır.

Sadece aşağıdaki bilgilerin girilmesi:

```text
G-XXXXXXXX
GTM-XXXXXX
AW-XXXXXX
Meta Pixel ID
TikTok Pixel ID
```

**reklam kampanyalarını yönetme yetkisi sağlamaz.**

Bunlar ağırlıklı olarak tracking ve dönüşüm ölçümleme içindir.

Panel içerisinden:

- Kampanya oluşturma
- Kampanya durdurma
- Reklam bütçesi değiştirme
- Reklam grubu oluşturma
- Ürün reklamı oluşturma
- Kampanya performansını çekme
- ROAS görüntüleme
- Reklam harcamalarını görüntüleme

gibi işlemler yapılacaksa ilgili platformların API'lerine OAuth üzerinden bağlanılması gerekir.

---

# 20. Consent / Cookie Yönetimi

Almanya ve Avrupa Birliği hedefli e-ticaret projelerinde GDPR/ePrivacy gereksinimleri nedeniyle kullanıcı izin yönetimi ayrıca ele alınmalıdır.

Panelde örneğin:

```text
Consent Management
├── Analytics
├── Advertising
├── Personalization
└── Functional
```

Google tarafında Consent Mode ile ilişkili temel sinyaller:

```text
analytics_storage
ad_storage
ad_user_data
ad_personalization
```

Tracking sisteminin kullanıcının verdiği izin durumuna göre çalışması sağlanmalıdır.

---

# 21. Önerilen Yönetim Paneli Formu

```text
Google Analytics
──────────────────────────────
Aktif                    [✓]

Measurement ID
[G-XXXXXXXXXX]

Enhanced Ecommerce       [✓]

Durum
● Bağlantı aktif
```

Meta örneği:

```text
Meta / Facebook
──────────────────────────────
Aktif                    [✓]

Pixel ID
[123456789012345]

Conversion API           [✓]

Access Token
[••••••••••••••••••••]

Server Side Tracking     [✓]

Browser Tracking         [✓]

Durum
● Aktif
```

---

# 22. Entegrasyon Durum Kontrolü

Her entegrasyon için panelde durum gösterilmesi önerilir:

```text
Google Analytics       ● Aktif
Google Tag Manager     ● Aktif
Google Ads             ● Aktif
Google Merchant        ● Aktif
Meta Pixel             ● Aktif
Meta CAPI              ● Aktif
TikTok                  ○ Pasif
Microsoft Ads          ○ Pasif
```

Mümkün olan entegrasyonlarda ayrıca:

```text
Son başarılı event
Son hata
Son senkronizasyon
Bağlantı durumu
```

bilgileri tutulmalıdır.

---

# 23. Önerilen Nihai Mimari

```text
                   E-TİCARET
                       │
                       ▼
              Ecommerce Event Bus
                       │
          ┌────────────┼────────────┐
          │            │            │
          ▼            ▼            ▼
        GA4         Google Ads     Meta
                                   │
                             Pixel + CAPI
          │            │            │
          ▼            ▼            ▼
       TikTok       Microsoft    Analytics
     Pixel + API       Ads
```

Bu yaklaşım sayesinde uygulamanın sipariş, ürün ve sepet sistemi reklam platformlarından bağımsız kalır.

Yeni bir reklam platformu eklendiğinde e-ticaret altyapısının değiştirilmesi yerine yeni bir entegrasyon adapter'ı eklenir.

---

# 24. Sonuç

Profesyonel bir e-ticaret reklam ve analytics altyapısında üç farklı katman ayrı düşünülmelidir:

**1. Tracking**

```text
GA4
GTM
Meta Pixel
TikTok Pixel
Microsoft UET
```

**2. Server-Side Conversion Tracking**

```text
Meta Conversion API
TikTok Events API
Google dönüşüm altyapıları
```

**3. Reklam Yönetimi**

```text
Google Ads API
Meta Marketing API
TikTok Marketing API
Microsoft Advertising API
```

Tracking entegrasyonlarında ID ve kod girişleri yeterli olabilir.

Ancak yönetim panelinden reklam kampanyalarının gerçekten yönetilmesi hedefleniyorsa kullanıcı adı/şifre almak yerine **OAuth tabanlı hesap bağlantısı ve resmi reklam API'leri** kullanılmalıdır.

Bu yapı güvenlik, ölçeklenebilirlik, dönüşüm doğruluğu ve gelecekte yeni reklam platformlarının eklenebilmesi açısından önerilen mimaridir.