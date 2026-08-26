# Telemania Demo / Test Ortamı — Plan (inceleme dokümanı)

> Bu doküman, karar vericilerin okuyup onaylaması için yazılmıştır. Onaydan sonra
> "bir yapay zekanın uygulayabileceği" adım-adım teknik şartnameye dönüştürülecektir.

## 1. Amaç

Kozmetik müşteri adayına gösterilecek **Telemania** demosunu, aynı zamanda **uçtan uca
çalışan bir test ortamı** olarak kurmak. Test ortamı şunları sağlamalı:

- Vitrine `telemania.ecspros.com` adresinden erişilebilsin (DNS hazır).
- Kredi kartı ödemesi, kargo ve SMS akışları **gerçek kurumsal hesap bilgisi olmadan** test edilebilsin.
- Demo site ve test verileri **tek komutla / kolayca silinebilsin**.
- Üretimdeki mevcut mağaza (mishar / misharitalia) **hiç etkilenmesin**.

## 2. Bugün sistemde ne var (doğrulanmış durum)

- Tek sunucu, üretim hattı: `nginx → API (:5000, systemd "ecspros") → PostgreSQL (ecommerce_db)`.
- Çoklu kiracı mimarisi hazır: `Firm → FirmPlatform`; vitrin hangi platformun gösterileceğini
  **host adına göre** çözer (`Store:Hosts`), tema da platform ayarlarından gelir.
- **Ödeme:** PayTR entegrasyonu **yalnız test modu** olarak kodlanmış; `TestMode` ayarı
  eksikse güvenli varsayılan `true` (yanlışlıkla gerçek kart çekilmez). Kimlik bilgileri
  veritabanında şifreli tutulur. Test kartları PayTR'in test panelinden sağlanır.
- **SMS:** Gerçek sağlayıcı (Ges Telekom / TT Mesaj) + yedek "log'a yaz" servisi mevcut.
  Ayarlar tanımlı değilse SMS gönderilmez, site yine de çalışır (güvenli davranış).
- **Kargo:** Taşıyıcı entegrasyonları şu an **stub** (gerçek API çağrısı yok), bildirim
  işçisi varsayılan kapalı; kargo kodu/barkod üretimi ve paket/sipariş akışı kodda mevcut.
  Yani kargo "sahte taşıyıcı" ile test edilmeye hazır durumda.

## 3. İzolasyon ve "kolayca silme" — iki seçenek

Bu, en kritik mimari karardır. İki yol var:

### Seçenek A — Aynı veritabanında ayrı "telemania" kiracısı (hafif)

- Mevcut çoklu-kiracı yapısı kullanılır: yeni `Firm` + `FirmPlatform` ("telemania") açılır.
- Ürünler **ortak katalog tablolarına** girer; vitrinde görünürlük `ChannelProduct` ile
  platform bazında ayrıştırılır. Çapraz sızmayı önlemek için "hariç tutma" satırları eklenir.
- **Silme:** demo verisini etiketleyip (ör. ürünlere `demo-telemania` etiketi + ayrı platform),
  bir temizlik scripti ile silmek.
- **Artı:** ek altyapı yok; tek app çalışmaya devam eder.
- **Eksi:** ürün verisi fiziksel olarak üretim kataloğuyla aynı tablolarda durur; temizlik
  scripti dikkatli yazılmalıdır; üretim DB'sine yazılır.

### Seçenek B — Ayrı demo veritabanı + ayrı demo uygulama örneği (önerilen)

- Aynı sunucuda ikinci bir PostgreSQL veritabanı (`ecommerce_demo`) ve ikinci bir uygulama
  örneği (systemd `ecspros-demo`, port `:5050`) açılır. Üretim app'i ve DB'si hiç değişmez.
- `telemania.ecspros.com` (ve demo admin/satıcı panelleri) bu demo örneğine yönlendirilir.
- **Silme:** demo veritabanını silmek (`drop database`) + demo servisini durdurmak + nginx
  satırını kaldırmak. Üretimle hiçbir veri paylaşılmadığı için **tek komutla, risksiz** temizlik.
- **Artı:** tam izolasyon, üretim kirlenmez, temizlik çok basit ve güvenli.
- **Eksi:** ikinci bir uygulama örneği (biraz daha RAM/CPU) ve kurulum adımı gerekir.

**Öneri:** "kolayca yok edilebilir" ve "üretim karışmasın" şartları net olduğundan
**Seçenek B** önerilir. Tek sunucu kalır, sadece ayrı DB + ayrı servis olur.

## 4. Entegrasyonların test modları

### 4.1 Kredi kartı ödemesi (PayTR)

- PayTR **test modu** kullanılır (kod hazır). Gerçek kurumsal POS bilgisi gerekmez.
- PayTR'in ücretsiz **test hesabı** (test merchant id/key/salt) + **test kartları** ile
  "ödeme başarılı / başarısız / 3D" senaryoları gerçek ekrandan denenir.
- Alternatif (sıfır dış bağımlılık): tamamen **mock ödeme** — kart formu gösterilir, ödeme
  anında başarılı sayılır, PayTR'e hiç çağrı gitmez. Demo/tanıtım için yeterli; gerçek
  akış testi için PayTR test hesabı daha iyidir.

**Karar gerekir:** PayTR test hesabı mı açılacak, yoksa mock ödeme mi kullanılacak?

### 4.2 Kargo

- **Mock taşıyıcı** kullanılır: sahte takip numarası üretir, paketi "hazırlanıyor → kargoda →
  teslim edildi" durumlarından geçirir. Gerçek kargo API'si çağrılmaz.
- Mevcut stub yapısı buna uygun; yalnızca demo için bir "test taşıyıcısı" tanımlanır.
- İstenirse ileride PTT/DHL/MNG **test ortamları** gerçekçilik için eklenebilir (kimlik ister,
  şimdilik şart değil).

### 4.3 SMS

İki kademe:

1. **Log modu (sıfır bağımlılık):** SMS içeriği ekrana/log'a yazılır; gerçek gönderim yok.
   OTP/akış mekanizması bu şekilde test edilebilir (kod geliyor ama telefona değil).
2. **Kendi telefonuna gerçek SMS:** bir SMS sağlayıcısının **test hesabı** kullanılır ve
   sadece sizin numaranız (whitelist) gönderim yapabilir. Örneğin Ges Telekom test hesabı
   ya da test modu destekleyen başka bir sağlayıcı.

**Karar gerekir:** SMS testi için "log modu" yeterli mi, yoksa telefonunuza gerçek SMS
gelecek bir test sağlayıcı hesabı mı açılsın? (Hangi sağlayıcı?)

## 5. Erişim noktaları (hedef)

- **Vitrin:** `https://telemania.ecspros.com` — Telemania kozmetik konseptli ana sayfa + katalog.
- **Admin paneli:** demo örneğine bağlı, demo verisiyle çalışır.
- **Satıcı paneli:** istenirse demo örneğine bağlı ayrı giriş.
- Üretim (`new.ecspros.com`, `www.misharitalia.com`) bu ortamdan tamamen bağımsız kalır.

## 6. Aşama planı (üst düzey)

1. **Altyapı:** demo DB + `ecspros-demo` servisi + nginx (`telemania.ecspros.com`) + DNS teyidi.
2. **Veri:** 610 Telemania ürününün görselleri `media/` altına indirilir; katalog import edilir.
3. **Vitrin:** kozmetik konseptine uygun ana sayfa (tema renkleri, kategori menüsü, ürün kartları).
4. **Test modları:** ödeme (PayTR test / mock), kargo (mock taşıyıcı), SMS (log / test sağlayıcı).
5. **Doğrulama + teardown:** uçtan uca test senaryoları + "her şeyi sil" komutu/scripti.

## 7. Onayınızı gerektiren açık kararlar

1. **İzolasyon:** Seçenek A (aynı DB'de kiracı) mı, Seçenek B (ayrı DB + ayrı örnek, önerilen) mi?
2. **Ödeme:** PayTR test hesabı mı, yoksa mock ödeme mi?
3. **SMS:** log modu yeterli mi, yoksa telefonunuza gerçek SMS için test sağlayıcı mı? (hangisi?)
4. **Katalog kapsamı:** vitrinde yalnız kozmetik/kişisel bakım mı, yoksa 610 ürünün tamamı mı?
5. **Paneller:** demo'da admin paneli + satıcı paneli de aktif olsun mu? (hangi test kullanıcıları?)
