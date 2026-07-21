# API Hesapları (ApiClient) — Tasarım

**Tarih:** 2026-07-20
**Durum:** Tasarım onayı bekliyor (ekran kurgusu konuşulacak — K16)

## Amaç

API kullanıcısı; site üyesinden (CRM `Member`) ve personelden (IAM `User`) **bağımsız** üçüncü
bir kimlik türü olacak. Makine kimliği insan kimliğiyle aynı havuzda tutulmayacak.

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
    public string ApiClientTypeCode { get; set; }  // ROL ekseni: supplier | dropshipping | marketplace … (bkz. §3)
    public string? OwnerType { get; set; }         // current_account  (CurrentAccount kalıbı)
    public Guid? OwnerId { get; set; }             // accounts.current_accounts.Id

    // Scope'lar hesapta TUTULMAZ — ApiClientTypeCode'dan token üretiminde çözülür (§3).
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
- `ApiClientTypeCode` = **iş rolü**: tedarikçi, dropshipping, pazaryeri… → yetki paketini belirler.

### Scope kataloğu
Panel `Permissions` kataloğundan **bağımsız** bir scope listesi. Gerekçe: yeni bir panel
yetkisi eklemek dışarıya açılan API'nin kapsamını yanlışlıkla genişletmemeli.

```
catalog.read      stock.read      order.read      invoice.read
catalog.write     stock.write     order.write     invoice.write
```

### Tip kataloğu — `definition.api_client_types`
Platformca (geliştirici firma) doldurulur; veri aktarımı/eşleme kayıt EKLEYEMEZ
(**definition şeması altın kuralı** — `integration_services` ile aynı). Her tip: kod,
ad, sabit scope seti, zorunlu `OwnerType`.

| Kod | Ad | Zorunlu sahip | Scope paketi (sabit) |
|-----|-----|--------------|----------------------|
| `supplier` | Tedarikçi | current_account (supplier) | catalog.read, stock.read, stock.write, order.read |
| `dropshipping` | Dropshipping | current_account (supplier) | catalog.read, stock.read, order.read, order.write, invoice.read |
| `marketplace` | Pazaryeri entegratörü | — / current_account | catalog.read, catalog.write, order.read, stock.read |
| `first_party` | Mobil / birinci taraf | — | catalog.read, order.read, order.write, stock.read |

- Yeni bir **scope** eklendiğinde hangi tiplere gireceği tek yerde (tip tanımı) kararlaştırılır.
- Token üretiminde `scope` claim'i hesabın tipinden çözülür; tip güncellenince tüm o tipteki
  hesaplar yeni token'da yeni sete kavuşur (mevcut 15 dk'lık token'lar doğal olarak biter).
- `RequireScopeAttribute` — `RequirePermissionAttribute` ile aynı kalıp, `scope` claim'ine bakar.
- **Scope "ne yapabilir", owner "hangi veri" demektir.** İkisi ayrı kontrol: `OwnerType=current_account`
  olan bir token'da sorgular otomatik `OwnerId` ile filtrelenir. `stock.write` scope'u olan bir
  tedarikçi yalnız kendi kalemlerini günceller. Tip zaten `OwnerType`'ı zorladığından, tedarikçi
  tipli bir hesap sahipsiz açılamaz.

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
- *Genel*: ad, **API tipi** (dropdown — kataloğdan), sahip cari, durum, geçerlilik
- *Yetkiler*: seçilen tipin scope seti **salt-görüntü** (kilitli — düzenlenemez; değişiklik tip tanımından)
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
| **F0** | Varsayılan `AdminOnly` politikası (`type != member && type != api_client`) | **Mevcut açığı kapatır — diğerlerinden bağımsız, önce yapılabilir** |
| F1 | `definition.api_client_types` kataloğu (seed) + `ApiClient` entity + migration + `/api/auth/token` | Tip = kilitli scope paketi (§3) |
| F2 | `RequireScopeAttribute` + tipten scope çözümü + owner bazlı sorgu filtresi | |
| F3 | Internal hesap (loopback kısıtı) + mobil first_party hesabı | |
| F4 | Panel ekranları (liste, detay, cari sekmesi) | Ekran kurgusu §6 onayından sonra |
| F5 | Rate limit + audit + kullanım ekranı | Rate limit bugün hiç yok |

## 8. Açık noktalar

- Partner hesaplarına ayrı bir dış dokümantasyon (public Swagger) gerekecek mi? Bugünkü Swagger
  production'da korumasız ve tüm iç uçları listeliyor — F4'ten önce kapatılmalı.
- Tedarikçi self-servis portalı düşünülüyor mu? Düşünülüyorsa API hesabı ile portal girişi
  ayrı kalmalı (biri makine, diğeri insan kimliği).
