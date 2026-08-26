# Telemania Demo — W3 Test Modları (ödeme / kargo / SMS)

> Bu doküman, demo ortamında **gerçek kurumsal hesap bilgisi olmadan** ödeme, kargo ve
> SMS akışlarının nasıl test edileceğini anlatır. Kodun mevcut durumuyla birebir doğrulanmıştır.

## Özet — hazır olan ve gereken

| Alan | Durum | Gereken dış hesap? |
|------|-------|--------------------|
| Ödeme (kredi kartı) | Kod **yalnız test modu** (`testMode` varsayılan `true`) | **Evet — ücretsiz PayTR TEST hesabı** |
| Kargo | Adapter'lar **stub**, bildirim işçisi kapalı — gerçek API çağrısı yok | Hayır (mock yeterli) |
| SMS | **Log modu** varsayılan (içerik log'a yazılır, telefona gitmez) | Hayır (log yeterli); gerçek SMS için test sağlayıcı |

---

## 1. Ödeme — PayTR test sistemi

### Mevcut durum (kod)
- `PayTrDirectService` yalnız **test modu** için yazılmış.
- Ayarlar `DbPaymentSettingsProvider` ile `core_firm_platform_integrations` içindeki
  `ServiceType=payment` kaydından çözülür; `testMode` ayarı **yoksa güvenli varsayılan `true`**
  (yanlışlıkla gerçek kart çekilmez).
- Kimlikler (`merchantId`/`merchantKey`/`merchantSalt`) DB'de şifreli tutulur; **kart verisi hiç saklanmaz.**

### Test için gereken (tek dış bağımlılık)
1. PayTR'de **ücretsiz test hesabı** açın (paytr.com → geliştirici/test paneli). Test ortamı
   size **test merchant id / key / salt** ve **test kartları** verir. Bu "gerçek kurumsal POS"
   değildir; yalnızca sandbox'tır.
2. Demo admin paneli → **Firma detayı → Entegrasyonlar** ekranından `PayTR` entegrasyonunu açın:
   - `merchantId`, `merchantKey`, `merchantSalt` (test değerleri) → **credentials** bölümüne.
   - `Test Modu` → **açık** (true).
3. Vitrinde ödeme adımında PayTR test kartlarıyla (başarılı / 3D / başarısız) deneyin.

> Kimlik bilgileri şifreli `Credentials` kolonuna yazıldığından, bu kaydı **panel üzerinden**
> oluşturmak gerekir (raw SQL ile şifreli kolon yazılamaz). PayTR test kimlikleri gelene kadar
> ödeme "yapılandırılmamış" sayılır ve checkout güvenli şekilde düşer.

---

## 2. Kargo — mock taşıyıcı (gerçek API yok)

### Mevcut durum (kod)
- 8 kargo firması seed'li: `aras, hepsijet, kolaygelsin, mng(DHL), ptt, surat, ups, yurtici`.
- Taşıyıcı adapter'ları **stub**; `CargoNotifyWorker` **varsayılan kapalı** → gerçek kargo
  API'sine istek gitmez, kuyruk yalnızca birikir.
- Kargo kodu/barkod üretimi `CargoCodeService` ile yapılır (`free` stratejili taşıyıcıda
  takip numarası yerel üretilir — aralık tahsisi gerekmez).

### Test akışı
1. Vitrinde bir sipariş oluştur → onayla → fulfillment ekranlarından (OP1–OP5) topla/paketle.
2. Paket kapanışında `Shipment` oluşur; takip numarası `free` stratejiyle yerel üretilir.
3. "Kargoya verildi" / "teslim edildi" durumları gerçek taşıyıcıya bağlanmadan denenebilir.

> İstenirse demo için açıkça **"Test Kargo"** adlı ayrı bir mock taşıyıcı tanımlanabilir
> (takip linki örnek, `free` strateji). Bu, gerçek firmalarla karışmaması için şıktır —
> `definition.integration_services`'a bir satırdır.

---

## 3. SMS — log modu + gerçek SMS

### Mevcut durum (kod)
- Gerçek sağlayıcı `GesTelekomSmsService` (TT Mesaj REST), yedek `LogSmsService` var.
- DB'de `gestelekom` (ServiceType=sms) tanımlı. **Ayar (FirmPlatformIntegration) yoksa**
  SMS gönderilmez, içerik **log'a yazılır** — site SMS'siz de çalışır (güvenli davranış).

### Test
1. **Log modu (varsayılan, sıfır bağımlılık):** OTP/akış tetiklenince mesaj `journalctl`'da
   `[SMS] To: ... | Message: ...` satırı olarak görünür. Mekanizma böyle test edilir.
2. **Telefona gerçek SMS (opsiyonel):** bir SMS test sağlayıcı hesabı açın (öneri: NetGSM
   ya da Ges Telekom demo/deneme hesabı), numaranızı whitelist'e alın. Kimlik bilgilerini
   panelden `gestelekom` (veya yeni sağlayıcı) entegrasyonuna girin — `GesTelekomSmsService`
   zaten hazır; NetGSM için küçük bir `NetGsmSmsService` eklenmesi gerekir.

---

## Test senaryosu (uçtan uca)

1. Vitrinde ürün sepete at → checkout → **ödeme** (PayTR test kartı).
2. Siparişi onayla → **kargo** (mock) → kargoya ver → teslim et.
3. Kayıt sırasında **SMS/OTP** (log modu) → kod log'dan alınıp doğrulanır.

Bu üç akış, hiçbir gerçek kurumsal POS/kargo/SMS sözleşmesi olmadan demo DB üzerinde çalışır.
