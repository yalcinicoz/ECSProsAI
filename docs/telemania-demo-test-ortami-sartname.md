# Telemania Demo & Test Ortamı — Uygulama Şartnamesi (AI-executable)

> İnsan-okunur özet: `docs/telemania-demo-test-ortami-plani.md`.
> Bu dosya, bir yapay zekanın / geliştiricinin adım adım uygulayabileceği teknik şartnamedir.
> "⚠️ KARAR" işaretli maddeler uygulamaya başlamadan netleştirilmelidir.

## 0. Onaylanan kararlar (kullanıcı)

- Demo erişimi: `telemania.ecspros.com` (DNS hazır).
- **Ayrı veritabanı** kullanılacak (üretim DB'sine demo ürünü yazılmaz).
- **Kalıcı, yeniden kullanılabilir demo yapısı**; ürün verileri zamanla değiştirilebilir.
- **Ana DB'ye kalıcı olarak** Telemania ürün grupları + grup özellikleri + değer şablonları eklensin;
  seed, yeni oluşturulan her DB'ye bu grupları doldursun.
- Telemania demosu için **pazaryeri mantığı yok**; **tek depo + tek satıcı** yeterli.
- **Tek admin panel** (kod) yeterli; demo için ayrı panel kodu/instance istenmiyor; çalışan sistemler bozulmayacak.
- Ödeme: **PayTR test sistemi**.
- Demo DB'ye **610 ürünün tamamı** doldurulacak.
- Projeye **kalıcı** Amazon, Trendyol, Hepsiburada, N11 entegrasyonları eklensin (demo istenirse kullanabilir).

## 1. ⚠️ KARAR — İzolasyon/uygulama modeli

"Ayrı DB + tek admin panel + demo için ayrı instance istememe" iki farklı teknik yolla karşılanabilir:

### Model 1 (ÖNERİLEN) — Ayrı demo DB + ayrı API servisi, tek panel KODU

- Üretim: `nginx → ecspros (:5000) → ecommerce_db` (bugünkü hali, dokunulmaz).
- Demo:   `nginx → ecspros-demo (:5050) → ecommerce_demo` (aynı derlenmiş binary, farklı appsettings).
- Admin/satıcı paneli: **tek kod tabanı**, `baseURL:'/api'` göreli olduğundan yalnızca iki host'ta servis edilir
  (örn. üretim admin host'u → üretim API; demo admin host'u → demo API). Panel kodu çoğalmaz.
- **Silme:** `drop database ecommerce_demo` + `systemctl stop/disable ecspros-demo` + nginx demo bloklarını kaldır.
- **Neden:** tam izolasyon, üretim app'i/DB'si hiç değişmez, teardown tek komut, risk düşük.
  Tek "fazla" şey ikinci bir systemd birimidir (kullanıcıya görünmez).

### Model 2 — Tek API süreci, iki DB (host/tenant bazlı DB yönlendirme)

- Tek `ecspros` süreci hem `ecommerce_db` hem `ecommerce_demo`'a bağlanır; istek başına host/tenant'a göre DB seçer.
- **Gerektirir:** tüm modüllerin `AddXxxInfrastructure` kayıtları tek `NpgsqlDataSource` yerine
  istek-başı çözülen bir veri kaynağına geçer (çapraz kesen değişiklik, ~11 modül). Üretim
  çalışırken yapılır → yanlış yönlendirme riski üretimi etkiler.
- Admin tek panel + tenant/environment seçici (header/cookie) ile iki DB'ye erişir.

**Uygulanacak:** Model 1. Model 2 yalnızca "tek süreç zorunlu" denirse değerlendirilir.
> ⚠️ KARAR: Model 1 onaylanıyor mu? (Önerilen budur.)

## 2. Çalışma alanları (workstreams)

### W1 — Kalıcı katalog tanımları (ana DB + seed)

**Amaç:** Telemania kozmetik ürün grupları, grup özellikleri ve değer şablonlarını ana DB'ye kalıcı
eklemek; her yeni DB (seed) bunları otomatik alsın.

Kaynak veri: `data/demo/kozmetik/telemania/curated.json` / `products.json` içindeki `category` alanları
(Şampuan, Saç Boyası, Maskara, Yüz Kremi, Saç Kremi, Deodorant, Saç Maskesi, Duş Jeli, Saç Köpüğü,
Saç Spreyi, Cilt Serumu, Fondöten, vs.).

Dokunulacak yerler:
- `src/ECSPros.Api/Extensions/DatabaseSeeder.cs`:
  - `SeedProductGroupsAsync` → her Telemania kategorisi için idempotent `ProductGroup` (Code prefix: `tlm-*`).
  - `SeedAttributeTypesAsync` / `SeedFilterRengiAttributeTypeAsync` → eksik özellik tipleri (örn. cilt tipi, hacim/ml).
  - `SeedProductGroupAttributesAsync` → grup↔özellik bağlantıları + gerekiyorsa `ProductGroupAxisSubAttributes` (değer şablonları).
  - İdempotent kalması şart: mevcut gruplar güncellenir, eksikler eklenir, var olan bozulmaz.
- Ana DB'ye migration **gerekmez** (gruplar veri satırıdır, seed ile yazılır).

Kabul kriterleri:
- Boş bir DB'ye `DatabaseSeeder.SeedAsync` çalışınca `tlm-*` grupları + özellikleri + değer şablonları gelir.
- Mevcut ana DB'de seed tekrar çalışınca veri bozulmaz (idempotent).

### W2 — Demo ortamı (ayrı DB)

**Amaç:** `ecommerce_demo` DB'sini kurup 610 Telemania ürününü (tek depo, tek satıcı) yüklemek.

Adımlar:
1. PostgreSQL'de `ecommerce_demo` DB'sini oluştur (aynı sunucu, ayrı DB).
2. `appsettings.Demo.json` (ConnectionStrings → `ecommerce_demo`; `Store:Hosts: {"telemania.ecspros.com": "telemania"}`; PayTR test; kargo mock; SMS ayarı).
3. `ecspros-demo` systemd birimi (aynı `publish/ECSPros.Api.dll`, `ASPNETCORE_ENVIRONMENT=Demo`, port 5050).
4. Demo DB'ye migration + seed uygula (yeni `tlm-*` grupları W1'den gelir).
5. **Veri import scripti** (`tools/` altına): `products.json`'daki 610 ürünü
   `ProductGroup`(tlm) + `Product` + `ProductVariant` + `ProductVariantImage` + stok olarak yazar;
   `ChannelProduct`/`ChannelCategory` demo platformuna bağlar. `IsSaleOpen=true`, tek depo/tek satıcı.
6. Görseller: `media/images/products/telemania/{id}/NN.jpg` (W4'teki indirici yeniden kullanılır).

Kabul kriterleri:
- `telemania.ecspros.com` üzerinde 610 ürün (kozmetik konsept) listelenir; üretim sitesi etkilenmez.
- Import scripti **tekrar çalıştırılabilir** (idempotent; ürün verisi değişince yeniden çalışır).

### W3 — Test modları (gerçek kurumsal bilgi olmadan)

- **Ödeme:** PayTR test modu. `DbPaymentSettingsProvider` + `core_firm_platform_integrations`
  içinde demo platformuna PayTR **test** merchant id/key/salt + `Settings.testMode=true` yazılır.
  Test kartları PayTR test panelinden gelir. (Kod zaten yalnız test modu destekliyor.)
- **Kargo:** mock taşıyıcı — `CargoNotify` akışı gerçek API yerine sahte takip no + durum üretir.
  Mevcut stub adapter yapısı kullanılır; demo platformuna bir "test kargo firması" tanımlanır.
- **SMS:** varsayılan **log modu** (SMS içeriği log'a yazılır). Telefona gerçek SMS için
  bir test sağlayıcı hesabı (whitelist) eklenir — bkz. §5 KARAR.

Kabul kriterleri:
- Kredi kartı ödemesi test kartlarıyla uçtan uca denenebilir; gerçek POS bilgisi istenmez.
- Kargo akışı gerçek kargo hesabı olmadan tamamlanır.
- SMS/OTP akışı telefona gerçek SMS olmadan da test edilebilir (log modu).

### W4 — Pazaryeri entegrasyonları (kalıcı, ayrı kapsam)

**Amaç:** Amazon, Trendyol, Hepsiburada, N11 için `IMarketplaceAdapter` uygulamaları eklemek
(mevcut sözleşme: `SyncProductAsync` ürün gönderimi, `UpdateStockAsync` stok, `FetchOrdersAsync` sipariş çekimi).

Durum: Yeni sistemde yalnız `TrendyolMarketplaceAdapter` + `TrendyolSellerClient` var.
Referanslar (legacy `ECSGYE.Solution`): `ECSGYE.TrendyolLibrary`, `ECSGYE.HepsiBurada`, `N11Entegrasyon`.
**Amazon için legacy referans yok** — sıfırdan veya satıcı API dokümanından yapılır.

Kapsam önerisi (fazlı):
1. Ortak adapter çatısı: kimlik çözümü + rate-limit + batch/issue entegrasyonu (mevcut MarketplaceSendService deseni).
2. Hepsiburada + N11 adapter'ları (legacy referanstan).
3. Trendyol adapter'ının tamamlanması (mevcut kısmi).
4. Amazon adapter'ı (yeni).

> ⚠️ KARAR: Öncelik ve kapsam — (a) hangi yönler şart: ürün gönderimi mi, sipariş çekimi mi, stok/fiyat senkronu mu? (b) hangi pazaryeri önce? Bu iş, demo'dan bağımsız bir ürün özelliğidir; demoyu bloklamaz.

### W5 — Yeniden kullanılabilirlik + teardown

- Demo verisi, platform kodu `telemania` + `tlm-*` grup kodu + ürün etiketi `demo-telemania` ile işaretlenir.
- `tools/demo-teardown.sh` (veya .NET aracı): demo servisini durdur, `ecommerce_demo`'u sil,
  `media/images/products/telemania/` klasörünü sil, nginx demo bloklarını kaldır.
- `tools/demo-import.py` (W2'deki import): yeniden çalıştırılabilir, ürün verisi değişince tekrar koşar.

Kabul kriterleri:
- Tek komutla demo tamamen kaldırılır; üretim hiç etkilenmez.
- Aynı scriptlerle demo yeniden kurulabilir.

## 3. Erişim noktaları (hedef)

- Vitrin: `https://telemania.ecspros.com` → demo API (:5050).
- Admin panel: tek kod tabanı; demo için ayrı host (örn. `admin-telemania.ecspros.com` → demo API) veya aynı panelde ortam seçici.
- Satıcı paneli: istenirse demo host'unda.
- Üretim (`new.ecspros.com`, `www.misharitalia.com`): değişmez.

## 4. Uygulama sırası

1. W1 (kalıcı katalog tanımları — ana DB seed'i).
2. W2 (demo DB + import + görseller) + W3 (test modları) → demo canlı.
3. W5 (teardown/rebuild scriptleri).
4. W4 (pazaryeri adapter'ları) — ayrı, uzun soluklu; demo'dan sonra.

## 5. ⚠️ KARAR — kalan açık maddeler

1. §1 Model 1 (ayrı demo API servisi) onayı.
2. SMS: log modu yeterli mi, yoksa telefona gerçek SMS için test sağlayıcı mı (hangisi)?
3. W4 öncelik/kapsam: hangi pazaryerleri, hangi yönler, hangi sırayla?
4. Demo admin/satıcı paneli için erişim yöntemi: ayrı host mu, aynı panelde ortam seçici mi?
