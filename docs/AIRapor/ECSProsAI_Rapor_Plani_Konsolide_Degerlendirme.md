# ECSProsAI — AI Raporlama Planı / Konsolide Değerlendirme

**Kaynak belge:** ECSProsAI_AI_Raporlama_Plani.pdf (Sürüm 1.0, 10 Eylül 2026)
**Değerlendirme:** Claude + Gemini + ChatGPT incelemelerinin birleştirilmesi
**Tarih:** 10 Eylül 2026
**Statü:** v1.1 için öneri listesi — mimari değişikliği içermez, detaylandırmadır.

---

## 1. Ortak sonuç

Üç bağımsız inceleme de aynı noktada birleşiyor: **mimari doğru kurulmuş.**

- Modelin serbest SQL üretmemesi, sadece yapılandırılmış rapor tanımı çıkarması
- Yetki denetiminin tamamen sunucu tarafında kalması
- Kayıtlı raporun tekrar AI'a yorumlatılmadan çalıştırılması
- İlk adımın sohbet ekranı değil, veri sözlüğü ve yetki denetimi olması

Bu dört karar tartışma konusu değil. Aşağıdaki 19 madde bu omurgayı değiştirmez, üzerine detay ekler.

---

## 2. Bulgu matrisi

| Konu | Claude | Gemini | ChatGPT |
|---|:--:|:--:|:--:|
| Semantik katman / JSON DSL şeması | ✓ | ✓ | – |
| Ölçü (metric) sürümleme | ✓ | – | ✓ |
| Döviz / kur politikası | ✓ | ✓ | ✓ |
| Netleştirme (ambiguity) UX'i | ✓ | ✓ | ✓ |
| Grafik ve satır limitleri, guardrail | ✓ | ✓ | – |
| AI kesilse de kayıtlı rapor çalışsın | ✓ | – | ✓ |
| Token / tenant maliyet takibi | ✓ | ✓ | – |
| CSV / export formatları | ✓ | – | ✓ |
| Drill-down | – | ✓ | ✓ |
| **Eval seti — NL→tanım doğruluk ölçümü** | ✓ | – | – |
| **KVKK, yurt dışı aktarım, prompt log'u** | ✓ | – | – |
| **Özet halüsinasyonuna karşı sayı doğrulama** | ✓ | – | – |
| **Kapsam daraltma (v1 = satış + stok)** | ✓ | – | – |
| Takvim kuralları, karşılaştırma dönemi | ✓ | – | – |
| Paylaşılan raporda kapsam uyarısı | ✓ | – | – |
| Otomatik zaman gruplaması, export kuyruğu | – | ✓ | – |
| Davranış telemetrisi (kaydetti mi / sildi mi) | – | ✓ | – |
| **Lineage — "bu rakam nereden geldi"** | kısmen | – | ✓ |
| **Read replica / reporting DB** | – | – | ✓ |
| **Rapor sahipliği devri** | – | – | ✓ |
| **Zamanlanmış rapor / alarm'a mimari hazırlık** | – | – | ✓ |

Üç incelemenin de bağımsız olarak yakaladığı üç boşluk — **döviz politikası, netleştirme UX'i, semantik katman şeması** — tartışmasız kabul edilmelidir.

---

## 3. Öne çıkan tekil katkılar

**Lineage / formül şeffaflığı.** Kullanıcı bir rakamdan şüphelendiği anda `Net Satış = Satış − İptal − İade` formülünü, sonucun hangi kaynaklardan üretildiğini (`Sipariş + İade + Komisyon`) ve veri güncelliğini (`10:42`) görebilmeli. Göremezse sisteme güvenmez ve Excel'ine döner.

**Read replica / reporting DB.** Dokümanda kuyruk ve limit var, ama ana veritabanının ağır raporlardan korunması yok. Bu bir Faz 5 optimizasyonu değil, **Faz 2'de verilmesi gereken mimari karardır** — sonradan ayırmak pahalıdır.

**Rapor sahipliği.** Personel ayrıldığında raporlarına ne olacağı, paylaşımların düşüp düşmeyeceği, kurumsal rapora dönüştürme imkânı tanımlanmalı.

**Otomatik zaman gruplaması.** "Son 1 yılı ürün bazında grafikle" isteğinde patlamak yerine sistemin haftalık/aylık kırılıma zorlaması. Top-N sınırından daha iyi bir çözüm.

**Eval seti.** Güvenlik ve veri doğruluğu testleri güçlü, ancak "kullanıcı ne dedi, model ne anladı" ölçülmüyor. Bu ölçülmezse prompt veya model değişiminde neyin bozulduğu görülemez.

**KVKK.** Müşteri verisi + dış AI servisi kombinasyonu için aydınlatma, veri işleyen sözleşmesi ve yurt dışına aktarım dayanağı gerekir. Ayrıca kullanıcının serbest yazdığı prompt metni denetim kaydına yazılıyor; bu log'un kendisi kişisel veri deposuna dönüşebilir.

---

## 4. İtirazlar

### 4.1. Drill-down yetki boyutuyla birlikte ele alınmalı

Gemini ve ChatGPT drill-down'ı yetki boyutunu hiç konuşmadan öneriyor. Toplamdan ham satıra inmek, planın en zor koruduğu şeyi açar: satır ve alan yetkisi. "128.000 TL iadenin detayı" = müşteri kayıtları.

**Öneri:** Drill-down **kendi ayrı iznine sahip bir işlem** olarak tanımlanmalı; v1'de en fazla "zaten izinli olunan boyutlarda bir kırılım daha aşağı" seviyesinde kalmalı. Ham kayıt listesi ayrı bir iştir.

### 4.2. Kapsam daraltılmalı

Gemini ve ChatGPT kapsamı sorgulamıyor ("çıkarılacak madde yok"). Bu görüşe katılmıyoruz.

Projenin bir numaralı riski teknik değil: Faz 1'in *"satış, stok, maliyet, kâr, cari — hepsinin tanımı iş birimince onaylansın"* kapısı. Maliyet ve kâr tanımı her şirkette aylarca tartışılır; proje hiç ekran göstermeden orada tıkanabilir.

**Öneri:** `v1 = satış + stok` ile Faz 5'e kadar gidip yayına almak, sonra alan eklemek. Kapsam cümlesi *"hedef tüm izinli işletme raporlarıdır; ilk sürüm satış ve stok ile açılır"* olarak güncellenmeli.

### 4.3. Belgeden çıkarılabilecekler

- Güvenlik ilkeleri 3., 4. ve 6. sayfada kısmen tekrar ediyor; tek yerde toplanıp referans verilebilir.
- PDF çıktısı tablo için zayıf bir formattır; v1'de XLSX + CSV yeterli.
- Arşiv (belli tarihte donmuş çıktı) kullanıcı talebi netleşmeden şema açmayı gerektirmez, ertelenebilir.

---

## 5. Faz faz eklenecekler

### Faz 1 — Veri ve yetki tasarımı

- [ ] **Semantik katman:** makine-okunur tek kaynak (`metrics` / `dimensions` / `filters` şeması). Prompt, validator ve sorgu üreticisi aynı tanımdan beslenmeli; iki ayrı yerde durursa zamanla birbirinden kayar.
- [ ] **Ölçü sürümleme** + formülün kullanıcıya gösterilmesi. Tanım değişirse rapor açılışında "ölçü tanımı güncellendi" uyarısı.
- [ ] **Döviz politikası:** varsayılan raporlama para birimi + hangi tarihin kuru (sipariş / ödeme / rapor tarihi).
- [ ] **Takvim kuralları:** hafta başı günü, mali dönem, "son 30 gün" bugünü kapsıyor mu, saat dilimi, yaz saati.
- [ ] **Karşılaştırma dönemi:** YoY / MoM hizalaması, sıfır paydada % değişim davranışı.
- [ ] **KVKK:** aydınlatma, veri işleyen sözleşmesi, yurt dışı aktarım dayanağı, prompt log'unun saklama süresi ve erişim yetkisi.
- [ ] **Referans hesap otoritesi** ve ölçü tanımlarının tek karar vericisi (isimle belirlenmeli).

### Faz 2 — Rapor yürütme temeli

- [ ] **Read replica / reporting DB ayrımı** — mimari karar olarak şimdi verilmeli.
- [ ] **Lineage:** sonucun hangi kaynaklardan üretildiği + veri güncelliği damgası.
- [ ] **Limitler:** tablo gösterim limiti, grafik veri noktası limiti, otomatik zaman gruplaması, büyük export → arka plan kuyruğu.

### Faz 3 — AI ekranı

- [ ] **Netleştirme UX'i** (üç önerinin birleşimi):
  1. Çalıştırmadan önce "Seni şöyle anladım" özeti
  2. Varsayımlar düzenlenebilir chip olarak görünür (`Sipariş tarihi` · `İptaller hariç` · `KDV dahil` · `TRY`)
  3. Gerçek ikili belirsizlikte şıklı soru (`[Toplam Ciro]` `[Satılan Adet]`)

  Bloklayan soru yalnızca maliyeti yüksek belirsizlikte sorulur.
- [ ] **Eval seti:** 50–100 örnek istek + beklenen rapor tanımı, CI'da koşan. Faz 3 kabul kapısı sayısal olmalı: *tanım eşleşmesi ≥ %90, sessiz yanlış varsayım = 0.*
- [ ] **Özet doğrulaması:** özet metnindeki her sayı sonuç kümesinde birebir bulunmalı; bulunmuyorsa özet gösterilmez. Özet üretimi ayrı ve araçsız bir çağrı olmalı, çıktısı düz metin olarak render edilmeli.

### Faz 4 — Kayıt ve paylaşım

- [ ] **Rapor sahipliği devri** / kurumsal rapora dönüştürme.
- [ ] **Paylaşılan raporda kapsam uyarısı:** "Bu rapor senin veri kapsamınla hesaplandı, sahibinden farklı sonuç görebilirsin." Bu ibare olmazsa "sistem yanlış hesaplıyor" şikayeti gelir.
- [ ] **Export:** v1'de XLSX + CSV; PDF sonraya. Export satır limiti tanımlanmalı.

### Faz 5 — Yük ve yayın kabulü

- [ ] **Tenant bazlı token / maliyet sayacı** + davranış telemetrisi (kaydetti mi, sildi mi, düzeltti mi) → eval setini besler.
- [ ] **Degradasyon planı:** AI sağlayıcısı kesildiğinde kayıtlı raporlar çalışmaya devam eder; yeni istekte davranış tanımlı olmalı.
- [ ] **Maliyet optimizasyonu:** sözlük büyük ve sabit olduğundan prompt caching; "sütun grafiğine çevir", "son 90 güne çıkar" gibi düzenlemeler modele hiç gitmeden deterministik işlenmeli.

### Mimari açık kapı (v1'de yapılmaz, engellenmez)

- [ ] **Zamanlanmış rapor ve eşik alarmı.** ("Her pazartesi geçen haftanın satış raporu", "stok 20'nin altına düşerse bildir".) Tanım + zamanlama + iletim kanalı ayrımı şimdiden düşünülürse sonradan ucuz olur.

---

## 6. Uygulama öncesi kalan kararlar (mevcut listeye eklenecek)

1. Kur politikası — hangi tarihin kuru, varsayılan raporlama para birimi
2. Drill-down v1'de var mı, hangi ayrı izinle
3. Kapsam daraltma kararı — v1 satış + stok mu, tüm alanlar mı
4. AI sağlayıcısı ve KVKK aktarım modeli
5. Ölçü tanımlarının tek karar vericisi kim

---

## 7. Son not

Dokümanın en güçlü cümlesi son paragrafta:

> *"İlk adım AI sohbet ekranını açmak değil, güvenilir veri sözlüğü ve zorunlu yetki denetimini kurmaktır."*

Bu cümle projenin bütün mantığını açıklıyor ve sona değil **birinci sayfaya** taşınmalı. Okuyucu kararını ilk 30 saniyede veriyor.

Özet: omurga sağlam, üç incelemenin toplamı 19 madde ekliyor, hiçbiri mimariyi değiştirmiyor. Tek yapısal itiraz kapsam maddesidir ve o da teknik değil takvim/risk kararıdır.
