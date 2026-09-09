# Panel Yetkilendirme Sistemi — Kaynak Belge Değerlendirmesi (v1, 2026-09-09)

Kaynak istek: `docs/panel-yetki-sistemi-promtu.md` (tasarım brief'i; kod/SQL/entity istemiyor, mimari + UX çıktısı istiyor).
Bu doküman **uygulama planı değildir**: brief'in güçlü/zayıf yönlerini, iç çelişkilerini, eksiklerini ve
**mevcut kod tabanıyla arasındaki gerçek mesafeyi** ölçerek raporlar. Karar bekleyen maddeler §7'de.

Durum: **DEĞERLENDİRME — K1-K8 kararları bekleniyor.** Kararlar verilmeden tasarım çıktısı (brief'in A-O başlıkları)
üretilmemeli; çünkü K1/K2/K3 modelin şeklini değiştirir.

---

## 0. Bir cümlelik sonuç

Brief yön olarak doğru ve uygulanabilir; **iki düzeltme** (§4.1 kullanıcı istisnasının "deny" olduğunun kabulü,
§4.2 permission kataloğunun kod sahipli olması), **bir kapsam genişletmesi** (§5.1 kanal kapsamının liste/rapor/export
tarafı) ve **bir gerçeklik payı** (§2 — bugün panelde fiilî zorlama yok, korunacak bir sistem yok) eklenirse
tasarım çıktısı üretmeye hazırdır.

---

## 1. Brief'in kapsadığı ve kapsamadığı

| Kapsıyor | Kapsamıyor (sınır açıkça yazılmalı) |
|---|---|
| İç panel kullanıcıları (`iam.iam_users`) | Satıcı paneli kullanıcıları (`iam.supplier_users`) — ayrı oturum/yetki dünyası |
| Sayfa / aksiyon / alan yetkisi | API istemcileri (`iam.api_clients` + `ApiScopes` kilitli paketler) |
| Satış kanalı kapsamı | Üye (storefront) tarafı — `MemberOnly` politikası, tamamen ayrı |
| Audit, simülasyon, geçici yetki | Firma katmanı (`User.FirmId` bugün var, brief'te hiç geçmiyor) |

Bu sınır yazılmazsa üç ayrı yetki sistemi zamanla birbirine karışır.

---

## 2. Mevcut durum envanteri (2026-09-09, kod + canlı DB ölçümü)

| Ölçüm | Değer | Kaynak |
|---|---|---|
| Panel/iç endpoint sayısı | **514** aksiyon (Store/Partner/Supplier hariç) | `src/ECSPros.Api/Controllers` |
| Bunlardan yetki kontrolü olan | **37** (`[RequirePermission]`) → **~%7** | `RequirePermissionAttribute` |
| Tanımlı permission | **11** | `Shared.Kernel/Authorization/Permissions.cs` = `iam.iam_permissions` |
| Rol | **3** — `super_admin` (11), `platform_admin` (11), `firm_admin` (7) | `iam.iam_role_permissions` |
| Kullanıcı | **49** aktif | `iam.iam_users` |
| Rol ataması | **4** — hepsi `super_admin` | `iam.iam_user_roles` |
| Kullanıcıya özel yetki | **0 satır** | `iam.iam_user_permissions` |
| DB tabanlı menü | **0 satır** (tablo boş, ölü) | `iam.iam_admin_menus` |
| Panel menüsü | sabit dosya; **8 kalemde** `permission` alanı | `admin/src/components/layout/sidebarNavigation.ts` |
| Audit log | **361** satır; yetki değişikliği yazan **kod yolu yok** | `iam.iam_audit_logs` |
| Aktif satış kanalı | **5** — `julude`, `mishar`, `olutbutik`, `trendyol_alyena` (firma `eldi`), `tozlu` (firma `misaroglu`) | `core.core_firm_platforms` |

### 2.1 Envanterin üç kritik sonucu

1. **Bugün panel fiilen "giriş yapan her şeyi yapar" durumundadır.** 45 kullanıcının hiç rolü yok, ama
   endpoint'lerin %93'ü düz `[Authorize]` olduğu için hepsini çağırabiliyorlar. Brief'in "eski mantığı
   koruyacağız" cümlesi teknik olarak karşılıksız: **korunacak bir uygulama yok, yeni sistem kurulacak.**
   Bu, önceliği de değiştirir: önce *zorlama* (endpoint + liste filtresi), sonra *yönetim UX'i*.
2. **§5'teki kullanıcı istisnası şemada var, davranışta yok.** `UserPermission.GrantType` alanı
   `grant|revoke` değerlerini taşıyor, ama `LoginCommandHandler` ve `RefreshTokenCommandHandler`
   yalnız `GrantType == "grant"` satırlarını rollerle **union**'lıyor → `revoke` satırı hiçbir şeyi kaldırmaz.
   Aynı şekilde `UserPermission.FirmId` alanı hiçbir sorguda okunmuyor.
3. **super_admin bypass'ı bugün gerçek bypass değil.** `RequirePermissionAttribute` içinde `permission == "*"`
   dalı var ama DB'de `*` diye bir permission **yok** → ölü kod. super_admin geçiyor çünkü 11 permission'ın
   hepsi tek tek atanmış. Sonuç: **koda yeni bir permission eklendiğinde, seed çalışana kadar super_admin de
   kilitlenir.** Brief §12'nin "ayrı, belirgin bir sistem özelliği olsun" beklentisi bu yüzden teknik olarak da haklı.

---

## 3. Brief'in güçlü yönleri (korunmalı)

- **Default deny** ve tek yönlü zihinsel model (§2, §32).
- **allow/deny/inherit/force katmanlarının reddi** (§2) — bu tür sistemlerin en sık pişmanlık kaynağı.
- **Grupların union'lanması, grubun "vermemesi"nin yasak sayılmaması** (§6).
- **CRUD değil gerçek işlev yetkileri** (§7) — `orders.cancel`, `orders.view_cost` gibi.
- **"Butonu gör" / "butonu kullan" ayrımının reddi** (§9) — permission şişmesini baştan engeller.
- **Üç katmanlı zorlama** (§26) ve "menü gizlemek güvenlik değildir" (§13).
- **Field verisinin backend'de hiç üretilmemesi** (§8, §25).
- **Yetkinin kaynağını gösterme** (§23) ve efektif yetki ekranı (§17) — destek yükünü asıl düşüren şey budur.
- **Silme yerine pasifleştirme** (§31) — `Permission.IsActive` alanı zaten var, uyumlu.
- **Audit'in insan diliyle okunması** (§21) — "PermissionUpdated + JSON" reddi doğru.

---

## 4. Brief'in iç çelişkisi ve riskli varsayımı

### 4.1 §5 aslında bir DENY katmanıdır — adı konmalı
§2/§6/§32 "deny yok, kayıt varsa var" derken §5 "gruptan gelen yetki kullanıcı özelinde kaldırılabilsin" diyor.
Bu tanımı gereği deny'dir. Gizlenirse ilk üretim vakasında **"Ahmet neden yapamıyor?"** sorusu cevapsız kalır.

Önerilen dürüst kurgu — basitliği bozmaz:
- **Tek deny ilkesi, tek seviye:** yalnız *kullanıcı* ezmesi deny üretebilir; **gruplar asla**.
- Kayıt: `kullanıcı_ezmesi(user, permission, kapsam, mod: ver | kaldır)`.
- Değerlendirme sırası: `roller/gruplar (union)` → `kullanıcı ver` → `kullanıcı kaldır` (en son, en güçlü).
- Kapsamlı dünyada deny de **kapsamlı** olmalı: "Julude'de kaldır" ile "tamamen kaldır" farklı kayıtlardır.
  Aksi hâlde §24'teki "kullanıcı istisnası grup değişikliğinden sonra korunur" kuralı kanal bazında belirsiz kalır.

### 4.2 §14 "panelden yeni permission tanımlama" — hayalet yetki riski
Panelden üretilen permission satırı hiçbir şeyi korumaz; bir ucu koruyan şey **koddaki kontroldür**.
Panelden "Sipariş İptal Et" adında yetki açılıp hiçbir endpoint'e bağlanmazsa yönetici verdiğini sanır,
sistem uygulamaz — bu **yetkisizlikten daha tehlikelidir** (yanlış güven).

Önerilen model:
- **Katalog kod sahiplidir:** geliştirici tanımlar, uygulama açılışında katalog DB'ye senkronlanır
  (bugünkü `Permissions.cs` + seed kalıbının devamı).
- **Panel yalnız sunumu düzenler:** görünen ad, açıklama, modül/sayfa gruplaması, sıralama, aktif-pasif.
- Panelde her satırda "kod tarafından tanımlı" rozeti; teknik key hiç gösterilmese de **değişmez**.
- Kodda karşılığı kalmayan permission → **otomatik pasif** + panelde "uygulamada karşılığı yok" uyarısı
  (audit geçmişi korunur, §31 ile uyumlu).

### 4.3 §22 geçici yetki — v1'de gerek yok
49 kullanıcılık bir panelde süre takibi, süre dolunca oturum/token tazeleme ve ayrı audit maliyeti,
getirisinden büyüktür. Kayıt şemasında `GecerlilikBitisi` alanı **rezerve edilip UI'sız bırakılabilir**;
ihtiyaç gerçekleşirse tek ekranla açılır.

---

## 5. Brief'te hiç geçmeyen, üretimde asıl maliyeti oluşturacak konular

### 5.1 Kapsam "yapabilir mi" değil, "hangi satırları görür" demektir  ⚠ en büyük kalem
§10 kanal kapsamını yetki kontrolü gibi anlatıyor. Asıl iş listelerdedir: kullanıcı yalnız Julude'yi görüyorsa
**sipariş listesi, arama, durum sayaçları, dashboard, DataGrid Excel export'u ve raporlar** da kanal filtreli
olmalıdır. Endpoint'e izin verip listeyi filtrelememek doğrudan sızıntıdır. Tahmini iş yükünün ~%70'i buradadır
ve bugünkü 17 DataGrid sayfasının handler'larına dokunur.

### 5.2 Yetki değişikliği ne zaman etkili olur?
Bugün permission'lar **JWT içine gömülü** (`JwtTokenService`), access token ömrü **60 dakika**. Yetki kaldırıldığında
kullanıcı 60 dakika daha yapmaya devam eder. §2'nin "kaldırılırsa kullanamaz" cümlesi bir karar gerektirir:
(a) istek başına DB/Redis'ten efektif yetki, (b) token'a `perm_version` claim'i + kullanıcı bazlı sürüm sayacı,
(c) kısa ömürlü access token. Brief'te bu konu hiç yok.

### 5.3 Field yetkisinin export tarafı
"Maliyet Gör" yoksa Excel export'ta da olmamalıdır; bugün `GridExportWriter` şemadaki tüm kolonları verir.
Alan yetkisi tanımlanırken **kolon anahtarıyla** eşleşmeli, yoksa panel gizler / export sızdırır.

### 5.4 super_admin çevresi
"En az bir super_admin kalmalı", "kullanıcı kendi super_admin'ini kaldıramaz", "super_admin atama/kaldırma
audit'te ayrı ve belirgin", "super_admin sayısı panelde görünür" kuralları brief'te yok.

### 5.5 Oturum yönetimi yetkinin parçasıdır
Yetki kaldırıldı ama kullanıcının açık oturumu var → "oturumları sonlandır" aksiyonu (ve onun yetkisi) gerekir.
`iam.iam_user_sessions` bugün mevcut, panelde karşılığı yok.

### 5.6 Kanalsız yetkiler
`definition.manage`, ürün grubu/özellik tanımı gibi platform işleri **kanal bağımsızdır**. Her permission'a kanal
listesi iliştirmek bunları anlamsızlaştırır. Permission tanımında **"kanal kapsamlı mı"** bayrağı şarttır
(§14'te bahsi var, sonuçları işlenmemiş): bayrak kapalıysa panelde kanal seçici hiç görünmemeli.

### 5.7 Firma katmanı
Kanallar iki ayrı firmaya ait (`eldi`, `misaroglu`) ve `User.FirmId` zaten var. Kapsamın **kanal** mı,
**firma + kanal** mı olduğu kararı modelin şeklini değiştirir (K1). Ayrıca `trendyol_alyena` bir mağaza değil
**pazaryeri** kanalıdır; kapsam listesi bu farkı taşıyabilmelidir.

### 5.8 Geçiş senaryosu
Default deny'ye sadık kalıp 45 rolsüz kullanıcıyı sıfır yetkiyle bırakmak, sistem açıldığı gün paneli durdurur.
Geçiş stratejisi brief'te yok (K8).

---

## 6. Ekranlar (§28) — dört ekran yeterli, ama içerik dağılımı farklı olmalı

| Ekran | Brief'teki rolü | Önerilen rol |
|---|---|---|
| Yetki İçerikleri | Yeni permission **tanımlama** | Kod kataloğunun **sunum/etiket** yönetimi (ad, açıklama, gruplama, aktif-pasif) + "uygulamada karşılığı yok" uyarıları |
| Yetki Grupları | Grup + permission seçimi | Aynı; UX kolaylıkları (modül başlığından toplu seçim, arama, grubu çoğaltma) korunmalı |
| Kullanıcı Yetkileri | Efektif + özel yetkiler | Aynı; **simülasyon (§20) burada bir sekme** olmalı, ayrı ekran değil |
| Yetki Logları | Audit | Aynı; filtreler §21'deki gibi |

Ek notlar:
- Simülasyon **yetki isteyen ve loglanan** bir işlem olmalı; aksi hâlde "kim neyi görebiliyor" bilgisi sızar.
- §19'daki kullanıcı listesinden "Yetkileri" kısayolu doğru; ayrı ekran açmaz.
- Kanal seçimi §16'daki gibi "3/5" özet + detayda liste doğru; **"tüm kanallar" seçeneği kaydedilirken
  o anki kanalların listesi olarak çözülmeli**, "gelecekte eklenecekler dahil" anlamına gelmemeli (§11 default deny).

---

## 7. Karar bekleyen sorular (K1-K8)

| # | Soru | Not / öneri |
|---|---|---|
| K1 | Kapsam birimi **kanal** mı, **firma + kanal** mı? | Modelin şeklini belirler; 2 firma / 5 kanal mevcut |
| K2 | Kanal kapsamı **listeleri de** filtreleyecek mi? | Evet demek ~15 liste sorgusu + export demek (§5.1) |
| K3 | Yetki değişikliği **anında** mı etkili olsun? | Hayır ise "en fazla 60 dk gecikir" yazılı kabul olmalı (§5.2) |
| K4 | Permission kataloğu **kod sahipli** mi? | Öneri: **evet** (§4.2) |
| K5 | super_admin = kullanıcı üzerinde **ayrı bayrak** mı? | Öneri: **evet**; bugünkü kilitlenme riski ortadan kalkar (§2.1-3) |
| K6 | Alan (field) yetkisi ilk sürüme girsin mi? | Öneri: maliyet / kâr / telefon / adres / personel notu ile **sınırlı** başlanmalı |
| K7 | Geçici yetki v1'de olsun mu? | Öneri: **hayır**, alan rezerve edilir (§4.3) |
| K8 | Mevcut 45 rolsüz kullanıcı geçişte ne olacak? | Sıfır yetki (panel durur) mı, geçiş rolü + daraltma mı? |

---

## 8. Önerilen sıra (uygulama kararı verilirse)

Brief'in kendisi faz önermiyor; ölçüm sonucuna göre mantıklı sıra:

1. **Zorlama iskeleti** — permission kataloğunun kod sahipli hâli, super_admin sistem bayrağı,
   efektif yetki hesabı (roller ∪ kullanıcı ver − kullanıcı kaldır) ve tazelik kararı (K3).
2. **Endpoint kapsaması** — 514 aksiyonun sayfa/aksiyon permission'larına bağlanması (en riskli olanlardan başlayarak).
3. **Kanal kapsamı** — önce yetki kontrolünde, hemen ardından **liste/sayaç/export filtresinde** (K2).
4. **Panel ekranları** — Yetki Grupları → Kullanıcı Yetkileri (efektif + kaynak) → Yetki İçerikleri.
5. **Audit** — yetki olaylarının insan diliyle yazımı ve Yetki Logları ekranı.
6. **Alan yetkileri** — sınırlı küme (K6), backend'de veri kesme + export kolon eşleşmesi.
7. **Simülasyon** — en son; öncekiler doğru çalışmadan simülasyon yanıltıcıdır.

---

## 9. Kaynak brief'e eklenmesi önerilen maddeler (özet)

1. §5'in bir **deny ilkesi** olduğu açıkça yazılsın; deny yalnız kullanıcı seviyesinde ve **kapsam bazında** olsun.
2. §14 **kod sahipli katalog** olarak yeniden yazılsın; panel yalnız sunum düzenlesin.
3. §10'a **liste/sayaç/rapor/export filtresi** dahil edilsin (kapsamın asıl anlamı).
4. Yeni bölüm: **yetki tazeliği** (token'a gömülü claim'ler ve 60 dk gecikme kararı).
5. §12'ye **son super_admin koruması**, kendi yetkisini düşürememe, atama olayının ayrı audit sınıfı eklensin.
6. §14'e **"kanal kapsamlı mı"** bayrağı ve kanalsız permission davranışı eklensin.
7. Yeni bölüm: **kapsam sınırı** — satıcı paneli ve API istemcileri bu tasarımın dışındadır.
8. Yeni bölüm: **geçiş planı** (45 rolsüz kullanıcı).
9. §22 (geçici yetki) "v2" olarak işaretlensin.
10. §20 (simülasyon) ayrı ekran değil, Kullanıcı Yetkileri sekmesi; yetki isteyen ve loglanan işlem olsun.

---

## 10. Ölçüm komutları (tekrar üretilebilirlik)

```
# endpoint sayısı (panel/iç)                → 514
# yetki kontrolü olan aksiyon                → grep -r "\[RequirePermission" src | wc -l
# permission / rol / kullanıcı / özel yetki  → iam.iam_permissions, iam_roles, iam_users, iam_user_permissions
# kanallar                                   → core.core_firm_platforms (IsActive)
```
