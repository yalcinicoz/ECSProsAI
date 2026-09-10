# ECSProsAI — AI Raporlama Planı: Değerlendirme ve İş Planı

**Kaynak dokümanlar**
- `ECSProsAI_AI_Raporlama_Plani.pdf` (Sürüm 1.0, 10 Eylül 2026)
- `ECSProsAI_Rapor_Plani_Konsolide_Degerlendirme.md` (Claude + Gemini + ChatGPT birleşik incelemesi)

**Değerlendirme tarihi:** 10 Eylül 2026
**Statü:** Değerlendirme + öneri listesi. Uygulama, migration veya yayın içermez.

---

## 1. Kapsam ve yöntem

Bu doküman iki kaynağı birlikte değerlendirir:

1. AI raporlama planının kendisi (PDF, 6 sayfa).
2. Üç ayrı yapay zekânın aynı plan üzerindeki incelemelerinin birleştirildiği konsolide doküman.

Değerlendirme yalnızca okuma ve analizdir; projede veya veritabanında hiçbir değişiklik yapılmamıştır.

---

## 2. Belirtilen düzeltme (değerlendirmeye yansıtılan)

Kaynak PDF'in 5. bölümündeki "Aktarım gecikmesi ve mükerrer toplamlar" maddesinde, kullanıcı tarafından şu içerik **dikkate alınmamıştır**:

> "Aktarım gecikmesi" başlığı ve "V3/legacy senkronlarının güncelliği rapora yansıtılmalı. Aktarılmamış kayıt, gerçek sıfır olarak yorumlanmamalı." cümlesi.

Maddenin doğru hali şudur ve değerlendirme yalnızca bu hali esas alır:

> **Mükerrer toplamlar**
> Sipariş, kalem, ödeme ve iade ilişkilerinde çoklanan join toplamlarını önleyen hesaplama kuralları gerekir. Fiziksel, rezerve ve kullanılabilir stok ayrı ölçüler olmalıdır.

Konsolide dokümanda, çıkarılan "Aktarım gecikmesi / V3-legacy güncelliği" içeriğine dayanan hiçbir madde yoktur. Bu nedenle düzeltme, konsolide değerlendirmenin hiçbir sonucunu değiştirmez; iki doküman bu açıdan bağımsızdır.

---

## 3. Kaynak PDF değerlendirmesi

### 3.1 Güçlü ve doğru kurulmuş yönler

- **Doğru güvenlik omurgası.** "AI yorumlar, uygulama yetkiyi denetler ve rakamı hesaplar; serbest SQL çalıştırılmaz; model erişemediği veriyi uyduramaz." Bu, bu tür sistemlerde en kritik karardır ve belge bunu baştan netleştirir.
- **Katmanlı ve gerçekçi yetki modeli.** Konu yetkisi / satır kapsamı / alan yetkisi / işlem yetkisi / "yeniden kontrol" ayrımı. "Yetkisiz alanla filtreleme de engellenir" ve "rapor açma, yeniden çalıştırma, indirmede güncel yetki uygulanır" maddeleri doğrudur.
- **"Tarif" ile "arşiv çıktısı" ayrımı.** Kayıtlı raporun AI'a yeniden yorumlatılmadan çalışması ve dışa aktarmanın ayrı izne ve denetim kaydına bağlanması yerindedir.
- **Dürüst statü bölümü.** "Tamamlanmış özellik değildir; canlı veri doğrulanmamıştır" denmesi belgenin güvenilirliğini artırır.
- **Kabul kapıları ve zorunlu test örnekleri somut.** İptal, kısmi iade, çoklu ödeme, para birimi; yetki aşımı; sahte talimat; gerçek sıfır ile hata ayrımı gibi doğrulanabilir testler tanımlı.
- **"Mükerrer toplamlar" kuralı proje bağlamına gerçekten oturuyor.** Order modülünde sipariş + kalem + (çoklu) ödeme + iade + fatura ilişkileri mevcut; fan-out join kaynaklı çift sayım riski somut. Fiziksel/rezerve/kullanılabilir stok ayrımı, Inventory tarafındaki rezervasyon akışıyla (confirm → rezerve, ship → düş, return → geri) birebir örtüşüyor.

### 3.2 Tespit edilen boşluklar ve riskler

- **Ölçü sözlüğü "şart" deniyor ama şeması yok.** "Ortak ölçü sözlüğü" deniyor; makine-okunur tek kaynak (`metrics` / `dimensions` / `filters`) tarif edilmiyor. Prompt, validator ve sorgu üreticisinin aynı tanımdan beslenmesi zorunluluğu belgede yok.
- **Kur politikası yalnızca "kalan karar" listesinde.** Hangi tarihin kuru, varsayılan raporlama para birimi belirsiz. Toplamların yanlış çıkmasına yol açacak en kritik veri-doğruluğu kararıdır.
- **Drill-down yok.** Toplamdan ham satıra inmek hiç konuşulmamış; satır/alan yetkisinin en zor sınavı orada verilir.
- **KVKK/aktarım tek cümle.** "Sağlayıcı, veri saklama ve gönderilecek alanlar uygulama öncesi ayrıca kararlaştırılmalıdır" yeterli değil; aydınlatma, veri işleyen sözleşmesi, prompt log saklama süresi eksik.
- **Doğruluk ölçümü yok.** "Kullanıcı ne dedi, model ne anladı" hiçbir sayısal kabul kapısıyla ölçülmüyor.
- **OLTP koruması yok.** Kuyruk ve limit var ama ağır raporun ana DB'yi bozmasına karşı bir karar yok. (Not: belge "veri hacmi canlıdan doğrulanmamıştır" der; bu karar hacim kanıtına bağlanmalı, koşulsuz zorunluluk gibi sunulmamalı.)
- **Rapor sahipliği devri yok.** Personel ayrılınca raporlara ve paylaşımlara ne olacağı tanımsız.
- **Faz 1 kapısı riskli.** "Satış + stok + maliyet + kâr tanımlarının iş birimi onayı" tek kapıda toplanmış; maliyet/kâr tanımı aylarca tartışılabilir ve proje hiç ekran göstermeden tıkanabilir.

---

## 4. Konsolide değerlendirme dokümanı

### 4.1 Kaliteli yönler

- **Sentez isabetli.** Üç incelemenin bağımsız yakaladığı üç boşluk (döviz politikası, netleştirme UX'i, semantik katman şeması) gerçekten de en tartışmasız eksiklerdir; önceliklendirme doğru.
- **"İtirazlar" bölümü dokümanın en değerli kısmı.** Özellikle drill-down'ın yetki boyutuyla birlikte ele alınması (4.1) ve kapsam daraltma (4.2: v1 = satış + stok) gerekçeli ve yüksek katma değerli itirazlardır.
- **Somut ve ölçülebilir öneriler.** Eval seti `≥%90`, `sessiz yanlış varsayım = 0`, özet doğrulaması "her sayı sonuç kümesinde birebir bulunmalı" uygulanabilir kurallardır.
- **Faz-faz eşleme pratik.** "Mimari açık kapı" (zamanlanmış rapor/alarm) ayrımı yerindedir.

### 4.2 Zayıf ve tutarsız yönler

- **"19 madde" sayısı tutmuyor.** Bulgu matrisinde 21 satır var; belgenin iç tutarlılığını zayıflatıyor.
- **İç çelişki.** Başta "mimari değişikliği içermez, detaylandırmadır" deniyor; sonra read replica için "Faz 2'de verilmesi gereken mimari karardır" deniyor. Read replica bir mimari karardır; ifadeler çelişiyor.
- **Tek-AI maddeleri fazla kesin sunuluyor.** Read replica, rapor sahipliği, zamanlanmış rapor gibi tek kaynaklı öneriler, faz planında üçlü-uzlaşma maddeleriyle aynı kesinlikte işlenmiş.
- **Read replica önerisi hacim kanıtına dayanmıyor.** Belge "veri hacmi canlıdan doğrulanmamıştır" derken, konsolidasyon bunu koşulsuz "Faz 2 mimari kararı" olarak sunuyor. "Hacim envanteri sonrası karar" olarak kalmalı.
- **"Mükerrer toplamlar" kuralı hiç açılmıyor.** Konsolidasyon, join deduplikasyonunu ve fiziksel/rezerve/kullanılabilir stok ayrımını ne genişletiyor ne de somut bir uygulama yoluna bağlıyor.

---

## 5. Düzeltmenin iki dokümana etkisi

- Konsolide doküman, çıkarılan "Aktarım gecikmesi / V3-legacy güncelliği" cümlesine hiçbir yerde dayanmaz; o cümlenin çıkarılması konsolidasyondaki hiçbir maddeyi geçersiz kılmaz.
- Buna karşılık korunan **"mükerrer toplamlar" kuralı** her iki dokümanda da ilke düzeyinde doğru konmuş ancak mekanizma düzeyine inilmemiştir. Bu kural önemseniyorsa, "Faz 1 / Veri ve yetki tasarımı" listesine somutlaştırıcı bir madde eklenmelidir: çoklu ödeme + çoklu iade/kısmi iade fan-out'unda toplamların nasıl tekilleştirileceği ve stokun üç ayrı ölçü olarak tanımlanması.

---

## 6. Özet

- **PDF:** Mimari omurga sağlam, dürüst ve fazlı. Asıl eksikler: semantik katman şeması, kur politikası, doğruluk ölçümü (eval), drill-down yetkisi ve KVKK detayı. "Mükerrer toplamlar" kuralı yerinde ancak uygulama mekanizması belirsiz.
- **Konsolidasyon:** Güçlü bir sentez; en değerli katkı "itirazlar" bölümü. Ancak madde sayısı tutarsız, "mimari değişiklik yok" ile read-replica "mimari karar" ifadeleri çelişiyor ve join-mükerrerliği kuralı derinleştirilmiyor.
- **Düzeltme:** Konsolidasyonu etkilemiyor; her iki doküman da korunan "mükerrer toplamlar" kuralını mekanizma düzeyine indirmediği için aşağıdaki iş planı bu maddeyi somutlaştırır.

---

## 7. İş planı (aksiyon listesi)

### 7.1 Bloklayıcı kararlar (uygulama öncesi çözülmeli)

1. Kur politikası: hangi tarihin kuru, varsayılan raporlama para birimi.
2. Ölçü sözlüğünün makine-okunur tek kaynağı ve şeması (metrics/dimensions/filters).
3. Drill-down v1'de var mı; varsa hangi ayrı izinle.
4. Kapsam daraltma: v1 satış + stok mu, tüm alanlar mı.
5. AI sağlayıcısı ve KVKK aktarım modeli.
6. "Mükerrer toplamlar" kuralının uygulama mekanizması (aşağıda).

### 7.2 Faz bazında iş kalemleri

- **Faz 1 — Veri ve yetki tasarımı**
  - Makine-okunur semantik katman; prompt, validator ve sorgu üreticisi aynı tanımdan beslenir.
  - Ölçü sürümleme ve formülün kullanıcıya gösterilmesi.
  - Döviz politikası ve takvim/karşılaştırma dönemi kuralları.
  - KVKK: aydınlatma, veri işleyen sözleşmesi, prompt log saklama süresi ve erişim yetkisi.
  - **Mükerrer toplamlar:** çoklu ödeme + çoklu iade/kısmi iade fan-out'unda toplamların tekilleştirilmesi; fiziksel/rezerve/kullanılabilir stokun üç ayrı ölçü olarak tanımlanması.

- **Faz 2 — Rapor yürütme temeli**
  - Lineage ("bu rakam nereden geldi") ve veri güncelliği damgası.
  - Tablo/grafik/satır limitleri, otomatik zaman gruplaması, büyük export → arka plan kuyruğu.
  - Read replica / reporting DB kararı — hacim envanteri tamamlandıktan sonra, kanıtla.

- **Faz 3 — AI ekranı**
  - Netleştirme UX'i: "Seni şöyle anladım" özeti, düzenlenebilir varsayım chip'leri, ikili belirsizlikte şıklı soru.
  - Eval seti (50–100 örnek istek + beklenen tanım), CI'da koşan; kabul eşiği `tanım eşleşmesi ≥ %90, sessiz yanlış varsayım = 0`.
  - Özet doğrulaması: özetteki her sayı sonuç kümesinde birebir bulunmalı.

- **Faz 4 — Kayıt ve paylaşım**
  - Rapor sahipliği devri ve kurumsal rapora dönüştürme.
  - Paylaşılan raporda kapsam uyarısı.
  - Export: v1'de XLSX + CSV; satır limiti tanımlı.

- **Faz 5 — Yük ve yayın kabulü**
  - Tenant bazlı token/maliyet sayacı ve davranış telemetrisi.
  - Degradasyon: AI kesilse de kayıtlı raporlar çalışmaya devam eder.
  - Deterministik düzenlemeler ("sütun grafiğine çevir", "son 90 güne çıkar") modele gitmeden işlenir.

### 7.3 Kontrol listesi

- [ ] Kur politikası kararlaştırıldı.
- [ ] Semantik katman şeması tek kaynak olarak tanımlandı.
- [ ] Drill-down yetki kararı verildi.
- [ ] Kapsam daraltma (v1 satış + stok) onaylandı.
- [ ] KVKK aydınlatma ve veri işleyen sözleşmesi hazır.
- [ ] Mükerrer toplamlar için join deduplikasyon kuralı yazıldı.
- [ ] Fiziksel/rezerve/kullanılabilir stok üç ayrı ölçü olarak tanımlandı.
- [ ] Eval seti CI'a bağlandı ve eşik tanımlandı.
- [ ] Read replica kararı hacim kanıtıyla verildi.
- [ ] Rapor sahipliği devri politikası yazıldı.
