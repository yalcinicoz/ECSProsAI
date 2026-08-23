---
title: İçerik Sayfaları
route: /cms/pages
group: İçerik
order: 10
summary: Sözleşme metinleri (yasal sayfalar), kurumsal sayfalar ve SSS içeriğinin kanal bazlı listelendiği ve detay sayfasında düzenlendiği ekran; yasal sayfalarda içerik kaydı sözleşme sürümünü ilerletir.
---

## Ne işe yarar
Sitedeki "Mesafeli Satış Sözleşmesi", "Ön Bilgilendirme Formu", "Gizlilik ve Güvenlik", "Kullanım Koşulları", "Kargo ve
Teslimat", "Üyelik Sözleşmesi", "KVKK Aydınlatma" gibi **yasal** metinler, "Hakkımızda" gibi **kurumsal** sayfalar ve SSS
(soru-cevap) içerikleri buradan yönetilir. Her kanalın kendi sayfa kopyası vardır; içerik düzenleme ayrı ayrı yapılır,
istenirse bir kanaldaki içerik diğer kanallara kopyalanır. Yasal sayfalarda her içerik kaydı yeni bir **sözleşme sürümü**
oluşturur; sipariş ve üyelik kabulleri kabul anındaki sürüm tarihiyle saklanır.

## Ekran yerleşimi
![İçerik Sayfaları listesi](img/cms-pages.webp)
1. **Başlık ve sayaç** — "İçerik Sayfaları" ve "N kayıt — sözleşme metinleri, kurumsal sayfalar ve SSS içeriği buradan yönetilir"; sağda platform seçici.
2. **Sekmeler** — `Yasal` / `Kurumsal` / `Tümü`.
3. **Tablo** — sayfa satırları; satıra tıklayınca detay açılır.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| AD | Sayfa adı (Türkçe) ve altında sayfa kodu (ör. `mesafeli-satis-sozlesmesi`). |
| TÜR | `Yasal` / `Kurumsal` / `Landing`. |
| PLATFORM | Sayfanın ait olduğu kanal. |
| AKTİF | `Aktif` / `Pasif` rozeti. |
| SON İÇERİK | İçerik bölümlerinin en son kaydedildiği zaman (yasal sayfalarda sözleşme sürüm tarihiyle aynıdır); hiç düzenlenmediyse `—`. |
| (son sütun) | "Detay →" ipucu. |

| Sekme / Filtre | Ne yapar |
|---|---|
| `Yasal` | Yalnız yasal sayfalar (varsayılan sekme). |
| `Kurumsal` | Kurumsal sayfalar. |
| `Tümü` | Tüm türler. |
| Platform seçici | `Tüm platformlar` ya da tek kanal. |

Arama kutusu ve sayfalama yoktur; pasif sayfalar da listelenir. **Yeni sayfa oluşturma butonu yoktur** — sayfalar kanal
kurulumunda hazır gelir, burada yalnız düzenlenir.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Satır tıklama | Liste | Sayfanın detayı açılır (`/cms/pages/{id}`). | Panele giriş yeterli. |
| ← | Detay sol üst | Listeye döner. | — |
| Diğer Platformlara Kopyala | Detay sağ üst | Kopyalama penceresi açılır (aşağıda). | — |
| Kaydet (Sayfa Bilgileri) | Sayfa Bilgileri kartı | Ad, slug, meta, aktiflik ve yayın penceresi kaydedilir; "Kaydedildi ✓". | Ad (TR) ve Slug (TR) dolu olmalı. |
| Bölüm satırı (▾ düzenle) | İçerik Bölümleri | Bölümün editörü açılır/kapanır (▴). | — |
| Görsel Editör / HTML Kaynağı | Metin bölümü editörü | Zengin metin editörü ile ham HTML arasında geçiş. | — |
| İçeriği Kaydet | Metin bölümü editörü altı | Bölüm içeriği kaydedilir; "Kaydedildi ✓ — sözleşme sürüm tarihi güncellendi". ⚠️ Yasal sayfada yeni sözleşme sürümü oluşur. | Bölüm tipi metin (`rich_text`). |
| + Soru Ekle | SSS bölümü editörü | "Yeni Soru" penceresi açılır. | Bölüm tipi `faq`. |
| Soru satırı tıklama | SSS listesi | "Soruyu Düzenle" penceresi açılır. | — |
| Sil (soru) ⚠️ | SSS satırı sağı | "Bu soru silinsin mi?" onayı → soru silinir. | — |
| Kaydet / Vazgeç (soru) | Soru penceresi | Soru kaydedilir / pencere kapanır. | Soru ve Cevap dolu olmalı. |
| Kopyala (pencere) ⚠️ | Kopyalama penceresi | Seçilen kanalların aynı kodlu sayfasına bu sayfanın bölüm içerikleri yazılır; "N bölüm kopyalandı ✓". Hedefteki mevcut içerik **değişir**. | En az bir hedef seçili; hedef aynı sayfa koduna sahip olmalı. |
| Kapat (pencere) | Kopyalama penceresi | Pencere kapanır. | — |

## Detay sayfası
![İçerik sayfası detayı — sürüm kartı, Sayfa Bilgileri ve İçerik Bölümleri](img/cms-pages-detay.webp)
*(1) Başlık: sayfa adı + Aktif/Pasif rozeti + "Diğer Platformlara Kopyala" · (2) Kod · Tür · Platform satırı · (3) ⚖️ sürüm takibi kartı (yalnız yasal) · (4) Sayfa Bilgileri · (5) İçerik Bölümleri*

### ⚖️ Yasal sözleşme metni — sürüm takibi (yalnız Yasal türde)
"Sözleşme sürümü: <tarih>" gösterir; tarih, sayfadaki bölümlerin en son kaydedilme zamanıdır (hiç düzenlenmediyse
sayfanın oluşturulma zamanı). Açıklama: sipariş ve üyelik kabulleri kabul anındaki sürüm tarihiyle saklanır; içerik
kaydedince yeni sürüm oluşur, önceki kabuller eski sürüm tarihini taşımaya devam eder (sipariş detayındaki "kabul edilen
sözleşmeler" bölümünde görünür).

### Sayfa Bilgileri
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ad (TR) | Evet | Sayfanın görünen adı. |
| Slug (TR) | Evet | Adres parçası (ör. `mesafeli-satis-sozlesmesi`). Değiştirirseniz sitedeki bağlantılar da değişir; dikkatli olun. |
| Meta Başlık | Hayır | Arama motoru başlığı. |
| Meta Açıklama | Hayır | Arama motoru açıklaması. |
| Aktif (sitede yayında) | — | İşaretli değilse sayfa sitede sunulmaz. |
| Yayın Başlangıcı | Hayır | Bu tarihten önce sayfa yayında sayılmaz. |
| Yayın Bitişi | Hayır | Bu tarihten sonra yayından kalkar. Boş = sınırsız. |

Bu kartın kaydı içerik bölümlerini ve sözleşme sürümünü **değiştirmez**.

### İçerik Bölümleri (n)
Her satırda bölüm tipi rozeti (`rich_text` = metin, `faq` = soru-cevap), bölüm adı, SSS'de soru sayısı, pasifse `Pasif`
rozeti ve "Son değişiklik: <tarih>" / "hiç düzenlenmedi" bilgisi vardır. Satıra tıklayınca editör açılır:

| Bölüm tipi | Editör |
|---|---|
| `rich_text` | Sekmeler: **Görsel Editör** (biçimlendirme araç çubuklu zengin metin) ve **HTML Kaynağı** (ham HTML, sabit genişlikli yazı alanı). Altta **İçeriği Kaydet**. Görsel Editör'e dönüşte editör yeniden kurulur; HTML'de yapılan değişiklik korunur. |
| `faq` | Soru listesi (sıra no + soru + cevap özeti; pasif sorular soluk ve `Pasif` rozetli), satır başına **Sil**, altta **+ Soru Ekle**. Soru penceresi alanları: Soru (zorunlu), Cevap (zorunlu), Sıra (sayı; yeni soruda en büyük sıra + 1), Aktif. |
| diğer | "Bu bölüm tipi (…) için panel editörü yok." |

### Diğer Platformlara Kopyala penceresi
- Açıklama: "Bu sayfanın içerik bölümleri seçilen platformların aynı sayfasına yazılır — oradaki mevcut içerik değişir.
  Firma/taraf bilgileri platformlara göre farklıysa kopyaladıktan sonra hedefte elle düzeltin."
- Liste: aynı koda sahip diğer kanal sayfaları, onay kutusu + kanal adı + "(son içerik: tarih)". Yoksa "Diğer
  platformlarda aynı kodlu sayfa yok."
- Eşleme: aynı tipteki bölümler sıra düzeninde eşlenir; hedefteki SSS soruları kaldırılıp kaynaktakiler kopyalanır.
  Otomatik eşitleme yoktur — kopyalama her seferinde bilinçli yapılır.

## Durumlar ve iş kuralları
- **Yayında olma koşulu:** Aktif işaretli + (Yayın Başlangıcı boş ya da geçmiş) + (Yayın Bitişi boş ya da gelmemiş).
  Yasal sayfalar sitede sözleşme pencerelerinde, ödeme adımındaki "Sözleşmeler ve Onaylar" bölümünde ve üyelik
  kaydındaki belgelerde bu koşulla gösterilir.
- **Sözleşme sürümü = son içerik kaydı tarihi.** Metin bölümünde **İçeriği Kaydet** ya da SSS'de soru ekleme/düzenleme/
  silme o bölümün "Son değişiklik" tarihini ilerletir; sayfanın sürümü bölümlerin en yenisidir ve listede SON İÇERİK
  olarak görünür.
- **Kabul kayıtları sürüm tarihiyle saklanır:** müşteri sipariş verirken / üye olurken kabul ettiği sözleşmelerin kodu,
  başlığı, kabul zamanı ve o anki sürüm tarihi kaydedilir. Sonradan metni değiştirmek eski kabulleri değiştirmez;
  sipariş detayında o kabulün yanında "metin bu kabulden sonra güncellendi" uyarısı görünür.
- Yazım düzeltmesi bile yeni sürüm sayılır; küçük düzeltmeleri toplu yapmak sürüm sayısını azaltır.
- Her kanalın sayfası ayrıdır; bir kanalda yapılan değişiklik diğerine **Diğer Platformlara Kopyala** ile taşınmadıkça
  yansımaz. Kopyalama yalnız aynı sayfa koduna yapılabilir; kaynağın kendisi hedef seçilemez.
- Yalnız metin (`rich_text`) bölümlerinin HTML'i düzenlenebilir; başka tipte bölüm için "Yalnız rich_text bölümlerinin
  HTML içeriği düzenlenebilir." hatası döner.

## Adım adım
**Mesafeli satış sözleşmesini güncelleme (tek kanal)**
1. `Yasal` sekmesinde platformu seçin, "Mesafeli Satış Sözleşmesi" satırına tıklayın.
2. İçerik Bölümleri'nde metin bölümüne tıklayın → Görsel Editör'de düzenleyin (ya da HTML Kaynağı).
3. **İçeriği Kaydet** → "Kaydedildi ✓ — sözleşme sürüm tarihi güncellendi". Sürüm kartındaki tarih yenilenir.

**Aynı metni diğer kanallara taşıma**
1. Düzenlediğiniz sayfada **Diğer Platformlara Kopyala** → hedef kanalları işaretleyin → **Kopyala**.
2. "N bölüm kopyalandı ✓" sonrası hedef kanalın sayfasını açıp firma unvanı/adres gibi kanala özel bilgileri düzeltin.

**SSS'ye soru ekleme**
1. SSS sayfasını açın, `faq` bölümüne tıklayın → **+ Soru Ekle** → Soru, Cevap, Sıra, Aktif → **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** **İçeriği Kaydet** yasal sayfada geri alınamayan bir sürüm ilerletmesidir; yayınlamadan önce metni
> HTML Kaynağı sekmesinde de gözden geçirin.

> **Dikkat:** Kopyalama hedefteki içeriğin üzerine yazar; kanallar farklı firmalara aitse sözleşme tarafı bilgilerini
> kopyalama sonrası hedefte mutlaka düzeltin.

> **İpucu:** Sayfa sitede görünmüyorsa sırayla Aktif, Yayın Başlangıcı/Bitişi ve platformu kontrol edin.

> **Not:** Detay sayfasında yalnız Türkçe alanlar (Ad (TR), Slug (TR)) düzenlenir.

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparisler/)
- [Üyeler](/rehber/musteriler/uyeler/)
