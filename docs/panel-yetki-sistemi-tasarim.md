# Panel Yetkilendirme Sistemi — Nihai Tasarım (v1, 2026-09-09)

**Kaynaklar:** brief `docs/panel-yetki-sistemi-promtu.md` · değerlendirme `docs/panel-yetki-sistemi-degerlendirme.md`
· kararlar `docs/panel-yetki-sistemi-soru-cevaplari.md` (K1-K8 + iki ek karar — **nihai iş kuralı**).
**Kapsam:** iç panel kullanıcıları (`iam.iam_users`). **Kapsam dışı:** satıcı paneli (`iam.supplier_users`),
API istemcileri (`iam.api_clients` + `ApiScopes`), üye/storefront tarafı. Bu sınır bilinçlidir.
**Bu doküman kod içermez** — mimari, kural, UX, audit, güvenlik, performans ve geçiş kararlarıdır.

---

## 0. Karar özeti (bağlayıcı)

| # | Karar | Sonuç |
|---|---|---|
| K1 | Kapsam birimi | **Kanal** (satış kanalı). Firma ayrı scope değil; kanaldan türetilir, UX'te "firmanın tüm kanalları" toplu seçimi olabilir |
| K2 | Kapsam listeleri filtreler mi | **Evet** — liste, arama, detay, dashboard, sayaç, rapor, export, toplu işlem, API cevabı |
| K3 | Tazelik | **Anında** — verilen yetki hemen kullanılır, kaldırılan yetki hemen kapanır |
| K4 | Katalog | **Kod sahipli**; panel yalnız sunum + tür + kanal-kapsamlı bayrağı + aktif/pasif yönetir, teknik key **değişmez** |
| K5 | Super admin | Kullanıcı üzerinde **ayrı sistem bayrağı**; permission değil. Tam bypass + tam audit |
| K6 | Alan (field) yetkisi | **V1'de var, sınırlı**: maliyet, kâr, müşteri telefonu, müşteri adresi, personel/özel notlar |
| K7 | Geçici yetki | **V1'de yok**; şema ileride engellemeyecek |
| K8 | Geçiş | Legacy/geçiş grubu → gerçek gruplar → istisnalar → geçiş grubunun kaldırılması |
| Ek-1 | Kullanıcı istisnası | Gruplar yalnız **verir**; kullanıcı özel **verir veya kaldırır** ve daha güçlüdür; istisna kanal kapsamlı olabilir |
| Ek-2 | "Tüm kanallar" | Seçildiği anki kanalların **anlık listesi** olarak kaydedilir; gelecekteki kanalları kapsamaz |

---

## A. Genel değerlendirme

Model üç şeyi doğru yapıyor ve bunlar korunmalıdır:

1. **Tek yönlü zihinsel model.** "Yetki var mı, yok mu?" — panel kullanıcısı ALLOW/DENY/INHERIT görmez.
2. **Tek deny noktası.** Yasak yalnızca *kullanıcı* seviyesinde, yalnızca *istisna* olarak vardır. Gruplar
   asla yasaklamaz. Bu, "neden yapamıyor?" sorusunu her zaman tek cümleyle cevaplanabilir kılar.
3. **Kapsamın veriye kadar inmesi (K2).** Yetkilendirmeyi buton gizlemekten ibaret sayan sistemler
   raporda/exportta sızdırır; burada kapsam sorgu seviyesinde uygulanır.

Modelin gerçek maliyeti ve riski üç yerdedir; tasarım bunları açıkça karşılar:

| Risk | Karşılığı |
|---|---|
| K2'nin bedeli listelerde (18 grid, dashboard, export, arama, SignalR) | §M/§O: kapsam filtresi **tek noktadan** (grid çekirdeği + sorgu bağlamı) uygulanır; sayfa sayfa elle filtre yazılmaz |
| K3 "anında" beklentisi bugünkü JWT'ye gömülü permission claim'iyle uyumsuz | §C.4: permission claim'leri token'dan **çıkarılır**, efektif yetki sunucu tarafında sürümlü cache'ten okunur |
| Bugün 514 panel ucundan yalnız 37'sinde kontrol var | §M.3: "kapsanmamış uç" varsayılan olarak **kapalıdır**; kaplama fazı bitene kadar geçiş grubu (K8) açık tutar |

---

## B. Nihai yetki modeli

### B.1 Kavramlar

| Kavram | Ne | Kim yönetir |
|---|---|---|
| **Permission** | Sistemin tanıdığı tek bir yetki. Değişmez teknik key (`orders.cancel`), tür (sayfa/aksiyon/alan), modül+sayfa bağı, kanal-kapsamlı mı, aktif/pasif, görünen ad + açıklama + sıra | **Kod** üretir (key, tür, kanal-kapsamlı varsayılanı); **panel** görünen ad/açıklama/sıra/grup/aktiflik ve — gerekiyorsa — kanal-kapsamlı bayrağını düzenler |
| **Yetki grubu** | Permission + kanal kapsamı listesi taşıyan toplu verme aracı. Otorite değil, kolaylıktır | Panel |
| **Kullanıcı** | Panel kullanıcısı. Üzerinde **süper admin bayrağı** (sistem özelliği) | Panel |
| **Kullanıcı istisnası** | (kullanıcı, permission, kanal kümesi, **ver/kaldır**) tek satır | Panel |
| **Kanal** | Satış kanalı (`core_firm_platforms`). Kapsamın tek birimi (K1) | Sistem |

### B.2 Permission türleri (K6 ile sınırlı)

| Tür | Anlamı | Zorlama noktası |
|---|---|---|
| **Sayfa** | Ekrana erişim (`orders.view`) | Menüde görünürlük + sayfanın veri uçları |
| **Aksiyon** | Sayfadaki gerçek iş (`orders.cancel`, `orders.change_address`) | Uç + işlemden hemen önce |
| **Alan** | Hassas veriyi görme (`orders.view_cost`) | **Sorgu/DTO üretimi** (gönderilmez) + export kolonu |

"Butonu gör" / "butonu kullan" ayrımı **yoktur** (brief §9): tek permission hem görünürlüğü hem işlemi belirler.

**V1 alan yetkileri (K6 — tam liste, genişletme ayrı karar):**
`common.view_cost` (maliyet), `common.view_margin` (kâr/kâr oranı), `customer.view_phone`,
`customer.view_address`, `common.view_internal_notes` (personel/özel notlar).
Bunlar **çapraz** yetkilerdir: bir sayfaya değil, aynı veriyi gösteren tüm yüzeylere uygulanır
(sipariş detayı, üye kartı, listeler, export, yazdırma).

### B.3 Kapsam (kanal) semantiği

- Her **verme** kaydı (grup veya kullanıcı) kanal-kapsamlı permission için bir **kanal kümesi** taşır.
- Kanal-kapsamlı **olmayan** permission'da kapsam alanı yoktur; panelde kanal seçici hiç görünmez
  (ör. `definition.manage`, `iam.users.manage`, ürün grubu/özellik tanımları).
- **"Tüm kanallar" (Ek-2)** bir kural değil, bir **kısayoldur**: kaydederken o anki kanal id'leri yazılır.
  Yeni kanal açıldığında kimse otomatik erişmez (default deny).
- Kanal kapsamı yalnız "yapabilir mi"yi değil, **hangi veriyi görür**i belirler (K2).

### B.4 Süper admin (K5)

Permission değil, **kullanıcı üzerinde bayrak**. Tüm kontrolleri bypass eder (sayfa, aksiyon, alan, kanal).
Bugünkü `RequirePermissionAttribute` içindeki ölü `"*"` permission dalı **kaldırılır**; bypass tek yerde,
bayrak üzerinden olur. (Bugünkü kurulumda super_admin rolü 11 permission'ı tek tek aldığı için koda yeni
permission eklendiğinde seed'e kadar süper admin de kilitleniyordu — bayrak bunu kökten çözer.)

---

## C. Efektif yetki hesaplama

### C.1 Algoritma (tek cümlelik hâli: *gruplar toplar, kullanıcı son sözü söyler*)

```
EtkinKanallar(kullanıcı, permission):
  1. kullanıcı.süperAdmin ise           → TÜM kanallar (ve tüm alanlar) — kontrol yok
  2. permission pasif ya da katalogda yok → BOŞ  (default deny)
  3. verilen  := ⋃ (kullanıcının üye olduğu grupların bu permission için kanal kümeleri)
  4. verilen  := verilen ⋃ (kullanıcı istisnası "ver" kanal kümesi)
  5. kaldırılan := (kullanıcı istisnası "kaldır" kanal kümesi)
  6. etkin := verilen − kaldırılan
  7. kanal-kapsamsız permission'da: etkin = (verilen ve kaldırılmamış) → doğru/yanlış
```

- **Sıra bağlayıcıdır:** kaldırma her zaman en sonda uygulanır → "kullanıcı özel kararı grup sonucundan
  güçlüdür" (Ek-1) kuralı tek satırda garanti edilir.
- **Yetki var mı?** kanal-kapsamlı permission'da `etkin ≠ ∅`; belirli bir kanalda iş yapılacaksa
  `istenenKanal ∈ etkin`.
- **Sonuç iki bilgi taşır:** *izin* ve *kanal kümesi*. Liste sorguları (K2) ikinci bilgiyi kullanır.

### C.2 Üç katman (brief §26 — değişmeden korunur)

| Katman | Ne yapar | Yetersizliği |
|---|---|---|
| 1. Menü/UI | Yetkisiz sayfa/buton/alan gösterilmez | Güvenlik değil, konfor |
| 2. Uç (sayfa/API) | Her istekte yeniden kontrol | URL elle yazılırsa burada durur |
| 3. İşlem | Kritik işlemden hemen önce yeniden kontrol + **kanal kontrolü** (kayıt hangi kanalın?) | Asıl güvence |

3. katmanda kural: **kaydın kanalı** kullanıcının o permission için etkin kanal kümesinde olmalıdır.
Değilse cevap, kaydın varlığını sızdırmamak için **404** (liste zaten göstermiyordu); yetkisi olan
kanalda ama aksiyon yetkisi yoksa **403**.

### C.3 Alan (field) yetkisi nasıl uygulanır

- Alan yetkisi olmayan kullanıcıya veri **üretilmez**: sorgu projeksiyonunda alan doldurulmaz, DTO'da
  `null` gider (alan tamamen kaldırılmaz — istemci sözleşmesi bozulmasın).
- **Export** aynı kuraldan geçer: yetkisiz alan kolon listesinden düşürülür (bugünkü `GridExportWriter`
  kolon seçimi buna uygun bir kanca).
- Yazdırma/PDF (fatura, toplama listesi) ve e-posta şablonları da aynı kurala tabidir.

### C.4 K3 "anında etkili" — teknik çözüm

**Karar: permission claim'leri JWT'den çıkarılır.** Bugün token 60 dk yaşıyor ve içinde permission
listesi taşıyor; bu, K3 ile bağdaşmaz.

| Katman | Çözüm |
|---|---|
| Token | Yalnız kimlik (`sub`, `email`, `full_name`) + `sa` (süper admin) + `pv` (yetki sürümü). Permission listesi **yok** |
| Sunucu | `IEffectivePermissions` servisi: kullanıcı başına efektif yetki (permission → kanal kümesi) — **L1** süreç-içi bellek (10-30 sn) + **L2** Redis (kullanıcı anahtarı, 5 dk) |
| Değişiklik anı | Yetki/grup/istisna/süper admin değişince: kullanıcının (veya gruptaki tüm kullanıcıların) cache anahtarı silinir **ve** `pv` artırılır |
| İstek anı | Kontrol cache'ten okunur (L1 hit'te sıfır ağ); token'daki `pv` güncelinden küçükse panele "yetkilerin değişti" sinyali gider, arayüz menüyü tazeler |
| Redis yoksa | DB'den okunur (mevcut `NoOpCacheService` kalıbı) — **fail-safe: kapalı değil, yavaş**; ama yetki **asla** cache yokluğu nedeniyle açık varsayılmaz |

Yan sonuç: yetki kaldırma oturumu düşürmez, sadece yetkiyi kapatır. "Oturumları sonlandır" ayrı ve
bilinçli bir aksiyondur (§O.5).

---

## D. Çakışma kuralları

Aşağıdaki tabloda "kanal" kolonu kanal-kapsamlı permission içindir; kapsamsızlarda küme yerine var/yok okunur.

| # | Durum | Sonuç | Neden |
|---|---|---|---|
| 1 | İki gruptan aynı permission | Kanal kümeleri **birleşir** | Gruplar toplar |
| 2 | Bir gruptan geliyor, diğerinden gelmiyor | Gelen grup kazanır (**verilir**) | Vermemek yasak değildir |
| 3 | Kullanıcıya özel **ver** | Gruplara **eklenir** | Kullanıcı özel de bir kaynaktır |
| 4 | Kullanıcıya özel **kaldır** | Sonuçtan **düşülür** (kanal kümesi kadar) | Kullanıcı kararı en güçlüdür |
| 5 | Kullanıcı gruptan çıkarılır | Yalnız o grubun kattığı kanallar düşer; başka gruptan/özel verilenler **kalır** | Kaynak bazlı hesap |
| 6 | Gruba sonradan permission eklenir | Gruptaki herkes kazanır; **ama** o kullanıcıda "kaldır" istisnası varsa yine kullanamaz | İstisna kalıcıdır (brief §24) |
| 7 | Gruptan permission kaldırılır | O grubun kattığı kanallar düşer; kullanıcı özel "ver" varsa **kalır** | — |
| 8 | Aynı permission farklı kanallarda farklı durumda | Küme aritmetiği: `(∪ verilen) − kaldırılan` | Kanal başına değerlendirme |
| 9 | Yeni kanal eklenir | Hiç kimse otomatik almaz (Ek-2) | Default deny |
| 10 | Süper admin | Tüm kurallar bypass; hesap yapılmaz | K5 |
| 11 | Permission pasife alınır | Herkeste kapanır; kayıtlar ve audit korunur | Katalog kuralı |
| 12 | Kullanıcı pasif/silinmiş | Yetki hesabı yapılmaz, giriş reddedilir | — |

**"Kaldır" istisnasının kapsamı:** panelde kullanıcı ya "tümünde kaldır" ya da "şu kanallarda kaldır"
der; ikisi de aynı satırda kanal kümesi olarak saklanır ("tümü" = kayıt anındaki kanalların listesi, Ek-2).

---

## E. Yetki grubu mantığı: kaynak mı, üretici mi?

İki yaklaşım vardır:

| | **Kaynak (referans) — ÖNERİLEN** | Üretici (materyalize/kopyalayan) |
|---|---|---|
| Nasıl | Grup üyeliği durur; efektif yetki her okumada birleşimle hesaplanır (cache'lenir) | Gruba eklenince permission'lar kullanıcıya **kopyalanır** |
| "Nereden geliyor?" | Doğal olarak cevaplanır (kaynak = grup adı) | Kopyada kaynak kaybolur, ayrıca izlenmesi gerekir |
| Grup değişince | Anında herkese yansır | Toplu yeniden yazma gerekir (kaçırılan kullanıcı = sessiz hata) |
| Aynı yetki iki gruptan | Doğal | Silme sırasında "diğer grup da veriyor mu?" hesabı gerekir — brief'in korktuğu durum |
| Maliyet | Okumada birleşim (cache ile önemsiz: 49 kullanıcı, onlarca permission) | Yazmada karmaşa |

**Seçim: kaynak (referans) modeli.** Kullanıcının zihnindeki "yetki insert edilir/silinir" hissi UX'te
korunur (özel yetki satırı gerçekten insert/delete'tir); grup yetkileri ise **hesaplanır**. Materyalizasyon
yalnız performans için bir **cache**tir, gerçeğin kaynağı değildir (§C.4).

---

## F. Panel UX — dört ekran

Ana navigasyonda **Yetkilendirme** başlığı altında dört ekran; beşinci ekran açılmaz.

### F.1 Yetki İçerikleri (katalog)
- **Amaç:** sistemin tanıdığı yetkilerin insan diline çevrilmesi. *Yeni permission üretme ekranı değildir* (K4).
- **Bölümler:** modül → sayfa ağacı; her satırda görünen ad, teknik key (soluk, kopyalanabilir), tür
  (sayfa/aksiyon/alan) rozeti, "kanal kapsamlı" rozeti, kullanım sayacı (kaç grup / kaç kullanıcı).
- **Aksiyonlar:** görünen adı/açıklamayı düzenle, modül-sayfa grubunu ve sırasını değiştir, aktif/pasif yap.
- **Uyarı durumları:** *"Kodda karşılığı kaldı mı?"* — katalog senkronunda kodda bulunmayan kayıt
  otomatik **pasif** olur ve satırda "uygulamada karşılığı yok" rozeti çıkar (audit geçmişi korunur).
- **Permission taslağı fikri (K4'te sorulan): ÖNERİLMİYOR.** Atanamayan bir kayıt, panelde "verdim ama
  çalışmıyor" hissinin başka bir biçimidir ve ekstra durum makinesi getirir. Yerine: bu ekranda
  **"Yeni yetki iste"** notu (serbest metin + istek sahibi + tarih) tutulur; geliştirici kodda tanımlayınca
  senkron kaydı gerçek permission olarak getirir ve not kapanır. Kayıt = talep, yetki = kod.

### F.2 Yetki Grupları
- **Liste:** grup adı, açıklama, kullanıcı sayısı, permission sayısı, kanal özeti ("3/5 kanal", "kanal kapsamsız").
- **Detay:** solda modül/sayfa ağacı, sağda o sayfanın yetkileri; her yetkinin yanında **kanal hapı**
  (`Kanallar: 3/5`) — tıklayınca kanal seçici açılır. Modül başlığından **tümünü seç/kaldır**, arama,
  **grubu çoğalt** (şablon gibi).
- **Kanal seçici:** kanal listesi + "Tümünü seç" (anlık liste, Ek-2) + firmaya göre toplu seçim (K1 notu).
- Grup detayında **deny yoktur**: bir yetki ya işaretlidir ya değildir.

### F.3 Kullanıcı Yetkileri
- **Üst:** kullanıcı kimliği, süper admin rozeti (varsa), **Yetki Grupları** (ekle/çıkar).
- **Sekme 1 — Efektif Yetkiler** (§H).
- **Sekme 2 — Özel Yetkiler:** kullanıcıya doğrudan verilen ve kullanıcıdan kaldırılan istisnalar; her
  satırda yetki adı, kanallar, "verildi/kaldırıldı" rozeti, kim-ne zaman.
- **Sekme 3 — Simülasyon** (§I). Ayrı ekran açılmaz.

### F.4 Yetki Logları
§J. Filtreler: tarih aralığı, işlemi yapan, hedef kullanıcı, hedef grup, permission, işlem tipi, kanal.

### F.5 Kısayol
Kullanıcı listesinde satır sonunda **"Yetkileri"** aksiyonu → doğrudan F.3. (Brief §19.)

---

## G. Yetki verme akışı (tık sayısı hedefleri)

| Senaryo | Adım | Hedef |
|---|---|---|
| Normal kullanıcı | Kullanıcı → **Gruba Ekle** → grubu seç → Kaydet | **3 tık** |
| Yeni kullanıcı | Bilgiler → gruplar → (opsiyonel özel yetki) → Oluştur | Tek sihirbaz, 3 adım |
| Özel yetki verme | **+ Özel Yetki** → sayfa/modül → yetki → kanallar → Kaydet | **4 adım** |
| Özel istisna (kaldırma) | Efektif Yetkiler'de satırın sonundaki **"Bu kullanıcıda kapat"** → kanal seç → onayla | **3 tık** |
| Kanal daraltma | Efektif satırındaki kanal hapı → kanalları seç → Kaydet | 3 tık |

Kural: özel yetki ekranı **yüzlerce permission'ı** aynı anda göstermez; önce sayfa/modül seçilir.
Kanal seçimi ana listede hap (`3/5`), detayda liste olarak gösterilir (brief §16).

---

## H. Efektif Yetki ekranı — "neden yapabiliyor / neden yapamıyor?"

Satır düzeni (permission başına tek satır):

```
Siparişi İptal Et                    Gülseli, OlurButik            [Kaynak: Sipariş Yöneticileri]
Adres Değiştir                       Gülseli                       [Kaynak: Müşteri Hizmetleri + Kullanıcıya özel]
Maliyet Gör                          —                             [Kapalı: kullanıcı istisnasıyla kaldırıldı]
İade Onayla                          —                             [Kapalı: hiçbir grupta yok]
```

- **Kaynak rozeti** birden çok olabilir (iki grup + özel).
- **Kapalı** satırlar da gösterilir (filtre: "yalnız açık olanlar"), çünkü asıl destek sorusu "neden
  yapamıyor?"tur. Kapalı nedeni üç şıktan biridir: *hiçbir kaynaktan gelmiyor* / *istisnayla kaldırıldı* /
  *permission pasif*.
- Kanal kolonu, kaynak bazlı dağılımı da açar: "Gülseli (Sipariş Yöneticileri), OlurButik (Kullanıcıya özel)".
- Her satırda iki hızlı aksiyon: **kanalları düzenle**, **bu kullanıcıda kapat/aç**.

---

## I. Kullanıcı simülasyonu ("Bu kullanıcı panelde ne görüyor?")

- **Salt okunur.** Kullanıcı hesabına giriş (impersonation) **yoktur** — oturum açmadan, yalnız hesap sonucu
  gösterilir. Böylece simülasyon sırasında yanlışlıkla işlem yapılamaz.
- Gösterdikleri: (1) görebildiği **menü ağacı**, (2) seçilen sayfada göreceği **aksiyon butonları**,
  (3) göremeyeceği **alanlar**, (4) erişebildiği **kanallar**.
- Simülasyon **kendi permission'ını ister** (`iam.permissions.simulate`) ve **loglanır** — kimin kimi
  simüle ettiği görünür; aksi hâlde "kim neyi görüyor" bilgisi sızar.
- Kaynak: efektif yetki servisi (§C) — ayrı bir hesap yolu yazılmaz, aksi hâlde simülasyon ile gerçek
  davranış zamanla ayrışır.

---

## J. Audit

### J.1 Loglanacak olaylar
Grup: oluşturma, ad/açıklama değişikliği, silme · Gruba permission ekleme/çıkarma · Grup permission'ının
**kanal kapsamı değişikliği** · Kullanıcıyı gruba ekleme/çıkarma · Kullanıcıya özel yetki verme/kaldırma ·
İstisnanın kanal kapsamı değişikliği · Permission içeriği düzenleme (ad/açıklama/tür/kanal bayrağı/aktiflik) ·
**Süper admin verme/kaldırma** (ayrı ve vurgulu tip) · Simülasyon çalıştırma · Yetkisiz erişim denemesi
(403/404 üreten kritik uçlar; gürültü olmaması için örneklenerek).

> **Uygulandı (2026-09-09):** ret kaydı ailesi `yetki.reddedildi`. Dört nokta yazar: `RequirePermission`
> (403), `KanalKapsamiKontrol` (404), toplu işlemde `KapsamDogrulama` (404), `NotificationHub.Subscribe`.
> Örnekleme: aynı *(kullanıcı + ret türü + yetki + uç)* anahtarı **10 dakikalık pencerede bir kez** yazılır;
> aradaki denemeler sayılır ve bir sonraki kayda taşınır. Sözlük 5.000 anahtarla sınırlıdır. Kayıt
> yazılamazsa **ret yine uygulanır** (fail-safe). Anonim istek (401) yazılmaz.

### J.2 Kayıt içeriği
`ne zaman · kim (aktör) · hedef (kullanıcı/grup/permission) · işlem tipi · önce → sonra (kanal kümeleri dahil) · kaynak (panel ekranı) · IP/oturum`.

### J.3 Panelde okunuş
Ham JSON değil, **cümle**:
> **08.09.2026 17:42** — Ekrem, **Ahmet**'e "Siparişi İptal Et" yetkisini verdi.
> Kanallar: Gülseli, OlurButik  ·  *Önce: Gülseli · Sonra: Gülseli, OlurButik*

Satır tıklanınca teknik ayrıntı (key, id'ler, ham fark) açılır — arayüzde varsayılan değil.

### J.4 Değiştirilemezlik
Yetki logları **yalnız eklenir**: uygulama hiçbir yerde bu kayıtlar için update/delete yolu sunmaz;
veritabanı kullanıcısının bu tabloda `UPDATE`/`DELETE` yetkisi olmaz. Saklama süresi sınırsızdır;
arşiv gerekirse yıllık bölümleme ile taşınır. Uygulama loglarından **ayrı** tutulur (bugünkü
`iam.iam_audit_logs` uygun taban, ancak yetki olayları için ayrı `EntityType` ailesi ve okunabilir
alanlarla).

---

## K. Süper admin güvenliği

| Kural | Gerekçe |
|---|---|
| Süper admin = kullanıcı bayrağı; permission listesinde görünmez | K5 |
| Yalnız süper admin, süper admin atayabilir/kaldırabilir | Yetki eskalasyonu kapanır |
| **Kullanıcı kendi süper adminliğini kaldıramaz** (en sade güvenli kural) | Kazara kendini kilitleme ve "son yönetici" durumu tek kuralla önlenir; kaldırma her zaman *başka* bir süper admin ister |
| Sistemde **en az bir** süper admin kalmalı — son kaydın kaldırılması reddedilir | Kilitlenmeye karşı taban |
| Süper admin sayısı ve listesi panelde görünür (Kullanıcılar ekranında rozet + sayaç) | Sessiz çoğalmayı önler |
| Süper admin verme/kaldırma ayrı audit tipi + logda vurgulu gösterim | K5 |
| Süper admin bypass'ı **audit'i bypass etmez** | K5 |
| Süper admin sayısı 1'e düşerse panelde uyarı şeridi | Operasyonel süreklilik |

---

## L. Geçici yetki

**V1'de yoktur (K7).** Değerlendirme: 49 kullanıcılık bir panelde süre takibi, süre dolunca efektif yetki
tazeleme ve ayrı audit; getirisinden büyüktür. İleride engellenmesin diye tek hazırlık yeterlidir:
verme kayıtlarında **bitiş zamanı alanı yer tutar**, hesaplama sırasında "bitişi geçmiş kayıt yok sayılır"
kuralı baştan yazılır ve UI'da hiç gösterilmez. Böylece özellik açıldığında model değişmez, yalnız ekran eklenir.

---

## M. Yeni kanal / yeni permission / kaplama senaryoları

### M.1 Yeni satış kanalı
Default deny: kimse otomatik erişmez (Ek-2). Panelde tek kolaylık: kanal oluşturulduktan sonra
**"Bu kanala erişimi olacak gruplar"** adımı önerilir (isteğe bağlı, atlanabilir). Sessiz otomatik
genişleme yoktur.

### M.2 Yeni permission
Kodda tanımlanır → açılışta katalog senkronu ekler → **kimseye otomatik atanmaz** → panelde "yeni yetki"
rozetiyle görünür, süper admin gruplara dağıtır. Görünen ad değişebilir, **key değişmez**; key değişimi
gerekirse yeni permission açılır, eskisi pasife alınır (audit tarihçesi kopmaz).

### M.3 Kaplanmamış uçlar (bugünün gerçeği)
514 panel ucundan 37'sinde kontrol var. Kural: **permission'a bağlanmamış uç, yetkilendirme devreye
girdiğinde kapalı sayılır.** Kaplama tamamlanana kadar kapıyı geçiş grubu (K8) açık tutar; böylece
"unutulan uç açık kalır" riski yerine "unutulan uç kapanır ve fark edilir" davranışı olur.

### M.4 Permission silme
Silme yok, **pasife alma** var (brief §31, mevcut `IsActive` alanı uygun): pasif permission yeni
atanamaz, panelde normal listede görünmez, mevcut atamalar etkisizleşir, audit geçmişi korunur.

---

## N. Sisteme eklenmemesi gerekenler (bilinçli hayır)

| Eklenmesin | Neden |
|---|---|
| Çok seviyeli ALLOW/DENY/INHERIT/FORCE | Tek deny noktası (kullanıcı istisnası) yeterli; gerisi hata ayıklanamaz hâle getirir |
| Rol/grup hiyerarşisi (grup içinde grup) | "Nereden geliyor?" cevabı zincire dönüşür; union zaten yeter |
| Kural motoru (ABAC: "kendi oluşturduğu siparişler", "çalışma saatleri") | V1 ihtiyacı değil; sorgu filtrelerini öngörülemez kılar |
| Permission taslağı (atanamayan kayıt) | §F.1 — talep notu daha sade |
| Her kolon/girdi için alan permission'ı | Permission enflasyonu; K6 listesi sabit tutulur |
| Kullanıcı hesabına giriş (impersonation) | Simülasyon yeterli; işlem sorumluluğu bulanıklaşır |
| Yetki değişikliğinde onay akışı (maker-checker) | 49 kullanıcılık panelde sürtünme; audit + süper admin kuralları yeterli |
| Kaydedilmiş "yetki şablonları" ayrı kavramı | Grup zaten şablondur; "grubu çoğalt" yeterli |
| Kanal dışında ikinci kapsam (depo, marka, firma) | K1: tek kapsam birimi |

---

## O. Gözden kaçan / üretimde önemli olacak noktalar

1. **Kapsam filtresinin tek noktadan uygulanması.** 18 grid şeması ortak `GridSchema<T>` + `GridRequest`
   üzerinden çalışıyor ve `OrderGrid` gibi şemalarda `firmPlatformId` alanı zaten var. Kanal kısıtı
   **istek bağlamına** (kullanıcının etkin kanal kümesi) konup grid çekirdeğinde zorlanmalı; sayfa sayfa
   `Where` yazılırsa biri mutlaka unutulur. Grid dışı yüzeyler (dashboard sayaçları, arama, rapor
   sorguları, `status-counts`, export) aynı bağlamı kullanmalı.
2. **SignalR.** `FulfillmentHub` / `NotificationHub` / `DashboardHub` üzerinden giden canlı bildirimler de
   kanal kapsamına tabidir; aksi hâlde liste filtrelense bile bildirim sızar. Bugün panelin tek hub
   bağlantısı kalıbı var (QuestionAlerts) — kapsam oraya da girmeli.
3. **404 / 403 ayrımı.** Kapsam dışı kayıt → 404 (varlığı sızmasın), kapsam içi ama aksiyon yetkisi yok →
   403. Bu ayrım baştan yazılmalı; sonradan değiştirmek istemci davranışını bozar.
4. **Yetki verme yetkisi ve eskalasyon.** "Kullanıcı yetkilendirebilen" kullanıcı, **sahip olmadığı bir
   yetkiyi başkasına verememelidir** (aksi hâlde kendini dolaylı yükseltir: kendine grup açar). Öneri:
   yetkilendirme ekranları süper admin + `iam.permissions.manage` sahiplerine açık; ikinci gruptaki
   kullanıcı yalnız **kendi sahip olduğu** permission ve kanalları dağıtabilir.
5. **Oturum yönetimi.** Yetki kaldırıldığında oturum düşmez (§C.4). "Kullanıcının oturumlarını sonlandır"
   ayrı bir aksiyon ve ayrı bir yetki olmalı; işten ayrılan personel akışı bunu ister.
6. **Kullanıcı pasife alma ≠ yetki kaldırma.** Pasif kullanıcı giriş yapamaz; yetkileri korunur (geri
   dönerse aynı yerden devam). Silme yerine pasifleştirme, audit bütünlüğü için tercih edilmeli.
7. **Alan yetkisinin gizli yüzeyleri.** Excel export, PDF/yazdırma (fatura, toplama listesi, kargo etiketi),
   e-posta/SMS şablonları, mobil/dış API cevapları ve **cache'lenmiş DTO'lar**. Alan yetkisi cache
   anahtarının parçası olmalı; yoksa yetkili kullanıcının cache'i yetkisize servis edilir.
8. **Rapor ve toplu işlem.** "Seçilenleri onayla/kargola" gibi toplu aksiyonlarda kapsam **her kayıt için**
   yeniden doğrulanmalı (istemciden gelen id listesi güvenilmez).
9. **Kanal kapsamının veri yazma tarafı.** Kullanıcı yetkisi olmayan kanala kayıt **oluşturamamalı**
   (ör. o kanala sipariş/kampanya/sayfa). Okuma kadar yazma tarafı da kapsanmalı.
10. **Geçiş grubunun görünürlüğü.** K8'deki legacy grup kalıcılaşma eğilimindedir; panelde "geçici"
    rozetiyle ve kaç kullanıcının içinde olduğu sayacıyla gösterilmeli, kapanış işi takvimlenmeli.
11. **Kullanıcının kendi yetkisini görmesi.** Destek yükünü azaltan küçük ekran: "Yetkilerim" (salt okunur,
    kendi efektif yetkileri + kanalları). Ucuz ve etkilidir.
12. **Boş durum davranışı.** Hiç yetkisi olmayan kullanıcı panele girdiğinde boş menüyle karşılaşır;
    "Yöneticinizle görüşün" yönlendirmesi olan bir karşılama ekranı gerekir (aksi hâlde "panel bozuldu" algısı).

---

## Ek — Uygulama sırası (kod fazları)

Tasarım kararları yukarıdadır; uygulama bu sırayla yapılırsa her faz kendi başına canlıya çıkabilir.

| Faz | İçerik | Neden bu sırada |
|---|---|---|
| **Y0** ✅ | *(2026-09-09 uygulandı — PROGRESS'e bakınız)* Katalog + model: kod sahipli permission kataloğu ve açılış senkronu, süper admin **bayrağı** (ölü `"*"` dalının kaldırılması), grup/istisna kayıt yapısı, kanal kümesi saklama | Diğer her şey buna dayanır |
| **Y1** ✅ | *(2026-09-09 uygulandı)* Efektif yetki servisi + K3 tazeliği: token'dan permission claim'lerinin çıkarılması, L1/L2 cache, değişimde invalidation, `Yetkilerim` ucu | Kontrolün tek kaynağı; panel menüsü de buradan beslenir |
| **Y2** ✅ | *(2026-09-09 uygulandı — kaplama testi düzeneği + K8 geçiş grubu birlikte)* Uç kaplaması: 514 panel ucunun sayfa/aksiyon yetkilerine bağlanması (riskli olanlardan başlayarak), 404/403 ayrımı | Asıl güvenlik kazancı |
| **Y3** | Kanal kapsamının **veriye** inmesi: istek bağlamı + grid çekirdeği + dashboard/sayaç/arama/export/toplu işlem/SignalR | K2'nin gerçek maliyeti; en dikkatli faz |
| **Y4** | Panel ekranları: Yetki Grupları → Kullanıcı Yetkileri (efektif + kaynak) → Yetki İçerikleri | Yönetilebilirlik |
| **Y5** | Audit: yetki olaylarının okunabilir yazımı + Yetki Logları ekranı + değiştirilemezlik | Y4 ile birlikte anlam kazanır |
| **Y6** | Alan yetkileri (K6 listesi): sorgu projeksiyonu + export kolonları + yazdırma yüzeyleri | Sınırlı küme, ayrı tur |
| **Y7** | Simülasyon + "Yetkilerim" ekranı | En son; öncekiler doğru değilse yanıltır |
| **Y8** ✅ | *(2026-09-09 uygulandı — araçlar)* K8 geçişi: departman grubu şablonları, grup kopyalama, grup kaldırma (üye varsa red), geçiş panosu, kaldırılan geçiş grubunun açılışta DİRİLTİLMEMESİ, yetkisiz kullanıcı karşılama ekranı | Canlı kesintisiz geçiş |

### Y8 notu — araç hazır, kullanıcı ataması iş kararıdır

Y8'in kod tarafı bitti; kalan adım (hangi personel hangi departman grubuna girecek) **iş kararıdır**
ve panelden yapılır. Canlı ölçüm (2026-09-09): 8 aktif kullanıcının **tamamı** `super_admin` DB
grubunda, yani fiilen tam erişimde; 4'ünde ayrıca süper admin bayrağı var. `gecis_tam_erisim`
grubu hiç kullanılmadı (0 üye) — çünkü geçişte herkesin zaten bir rolü vardı.

Bu yüzden Y8'in canlıdaki gerçek işi "geçiş grubunu boşaltmak" değil, **`super_admin` grubundaki
bayraksız kullanıcıları gerçek departman gruplarına taşımaktır**; default-deny'ın kazancı ancak o
zaman ortaya çıkar. Sıra: (1) Şablondan Kur ile departman grupları, (2) kullanıcıyı yeni gruba ekle,
(3) `super_admin` grubundan çıkar, (4) eksik yetkiyi kullanıcı istisnasıyla tamamla,
(5) boşalan geçici grupları Kaldır.

**Faz dışı bırakılanlar (bilinçli):** geçici yetki (K7), impersonation, onay akışı, kanal dışı ikinci kapsam.

## Ek — Uygulama sırasında verilen ek kararlar (2026-09-09)

| Konu | Karar | Gerekçe |
|---|---|---|
| Satın alma / mal kabul ekranlarında birim fiyat | **Alan yetkisine bağlanmadı** | Fiyat, o ekranın konusudur; ekran zaten `procurement.manage` ile korunur. Maskelemek ekranı işlevsiz kılardı |
| Sayım / teslim (depo personeli) ekranında birim maliyet | **`common.view_cost` istenir** | Depo personelinin maliyeti görmesi K6'nın hedeflediği sızıntı; ekranın işlevi maliyet değil sayımdır |
| Kâr / marj (`common.view_margin`) | Katalogda var, **zorlama noktası yok** | Bugün hiçbir DTO marj döndürmüyor; marj raporu yazıldığında bu yetkiye bağlanacak |
| Yazdırma sayfaları (`/yazdir/fatura`, etiket) | **KARAR: yetki sisteminin DIŞINDA kalır** (kullanıcı kararı, 2026-09-09) | Sayfalar OP2 (2026-08-09) kararıyla `[AllowAnonymous]`: masa tabletindeki iframe JWT taşıyamıyor, GUID "anahtar" sayılıyor. Kullanıcı kimliği olmadığı için alan yetkisi çalıştırılamaz; kullanıcı bu yüzeyi bilerek yetkiden bağımsız bırakmayı seçti. Kabul edilen risk: GUID'i eline geçiren, o siparişin müşteri ad/adresini görebilir |
| Toplama planı listesi | Kanal filtresine tabi **değil** | Planlar depo operasyonudur, kanal kolonu taşımaz; ancak plana kapsam dışı sipariş alınamaz (kayıt-bazlı doğrulama) |
| Pazaryeri eşlemeleri | Kanal kapsamı **uygulanmadı** | Eşleme servisi tüm sorgularında `FirmPlatformId == null` filtreler: veri firma genelidir, kanal bazlı değildir |

**Kapatılan madde (2026-09-09 kullanıcı kararı):** `/yazdir/*` sayfaları kimlik doğrulaması olmadan,
yalnız GUID bilgisiyle erişilebilir ve müşteri adı/adresi içerir. Kullanıcı bu yüzeyin **yetki
sisteminin dışında kalmasına** karar verdi (yazdırma iş akışı iframe/tablet üzerinden yürüyor,
kimlik taşınamıyor). Kabul edilen risk açıkça kayıtlıdır: yazdırma bağlantısındaki GUID'i eline
geçiren kişi o belgedeki müşteri bilgisini görebilir. Alan yetkileri bu sayfalara UYGULANMAZ;
ileride istenirse imzalı kısa ömürlü bağlantı seçeneği açık kalır.

---

## Ek — Bu tasarımla kapanan açık sorular

| Soru | Cevap |
|---|---|
| Permission taslağı gerekli mi? (K4) | **Hayır** — yerine katalog ekranında "yeni yetki iste" notu (§F.1) |
| Kullanıcı kendi süper adminliğini kaldırabilir mi? (K5) | **Hayır**; kaldırma her zaman başka bir süper admin ister + en az bir süper admin kuralı (§K) |
| Kaldırma istisnası kanal bazlı mı? (Ek-1) | **Evet**, kanal kümesi taşır; "tümü" kayıt anındaki kanalların listesidir (§D) |
| Grup materyalize mi edilir? (E) | **Hayır**, referans modeli; materyalizasyon yalnız cache (§E, §C.4) |
| Yetki değişimi oturumu düşürür mü? (K3) | **Hayır**; yetki anında kapanır, oturum ayrı aksiyonla sonlandırılır (§C.4, §O.5) |
| Kapsam dışı kayda doğrudan link | **404** (varlık sızmasın); kapsam içi ama aksiyon yoksa **403** (§C.2) |
