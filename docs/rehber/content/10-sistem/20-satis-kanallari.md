---
title: Satış Kanalları
route: /settings/channels
group: Sistem
order: 20
summary: Firmaların satış kanallarının (web sitesi, pazaryeri mağazası, mobil uygulama, mağaza/POS) kart görünümünde listelendiği; kanal adı, fiyat tipi, API kimlik bilgileri, stok görünürlüğü, ödeme yöntemleri, kargo gönderimi ve eski sistem eşlemesi gibi kanal ayarlarının düzenlendiği ekran.
---

## Ne işe yarar
Satış kanalı, bir firmanın ürün sattığı her bir mecradır: kendi web sitesi, Trendyol/Hepsiburada gibi pazaryeri mağazaları, mobil uygulama ya da fiziksel mağaza/POS. Bu ekranı sistem yöneticisi kullanır: yeni kanal açmak, kanalın fiyatlama tipini ve platforma özgü API bilgilerini girmek, sitede stoğu bitenlerin görünmesini, sunulan ödeme yöntemlerini, kapıda ödeme bedel/limitini ve kargo gönderim davranışını belirlemek için. Aynı kanal formuna **Firmalar › firma detayı › Satış Kanalları** bölümünden de ulaşılır.

## Ekran yerleşimi
![Satış Kanalları — firma sekmeleri ve kanal kartları](img/settings-channels.webp)
1. **Başlık satırı** — "Satış Kanalları" başlığı, kısa açıklama ve sağda **Yeni Kanal** butonu.
2. **Firma şeridi** — `Tümü` hapı (toplam kanal sayısıyla) ve her firma için bir hap (firma adı + o firmanın kanal sayısı; ana firmanın önünde renkli nokta). Seçili hap vurgulanır.
3. **Kanal kartları** — ızgara düzeninde kartlar (geniş ekranda üç sütun). Karta tıklayınca "Kanal Düzenle" penceresi açılır.
4. **Kanal penceresi** — yeni kanal / düzenleme formu (aşağıda).

## Liste ve filtreler
Liste kart biçimindedir, arama kutusu ve sayfalama yoktur.

| Filtre | Ne yapar |
|---|---|
| Tümü | Bütün firmaların kanallarını gösterir; kartların altında firma adı şeridi görünür (birden çok firma varsa). |
| Firma hapları | Yalnız seçilen firmanın kanallarını gösterir. Bu sırada **Yeni Kanal** o firma için açılır (firma seçimi sorulmaz). |

Kart içeriği:

| Kart bölgesi | Anlamı |
|---|---|
| İkon | Pazaryeri kanalında sarı çanta ikonu, diğerlerinde küre ikonu. |
| Ad / alt satır | Kanal adı ve platform tipi adı. |
| `Aktif` / `Pasif` rozeti | Kanalın durumu. |
| Kod | Kanal kodu. |
| Fiyat | Yalnız fiyat tipi seçiliyse: `×çarpan` ya da `Manuel`. |
| `API x/y` rozeti | Platform tipi şemasındaki kimlik bilgisi alanlarından kaçının dolu olduğu; tümü doluysa yeşil, eksikse sarı. |
| `Ayar x/y` rozeti | Şemadaki ayar alanlarının doluluk oranı. |
| Firma şeridi | Yalnız "Tümü" görünümünde ve birden çok firma varsa: kanalın firması. |

Boş durum mesajları: "Henüz satış kanalı tanımlanmamış." (Tümü) / "Bu firmaya henüz satış kanalı eklenmemiş." (firma seçiliyken); altında **Kanal Ekle** butonu. Hiç firma yoksa "Firma kaydı bulunamadı. Önce Firmalar sayfasından bir firma oluşturun." uyarısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Kanal | Sağ üst | "Yeni Satış Kanalı" penceresi açılır. "Tümü" seçiliyken ve birden çok firma varsa formda önce **Firma** seçilir. | En az bir firma olmalı. |
| Kanal Ekle | Boş liste kartı | Yeni Kanal ile aynı. | — |
| Kart tıklama | Kanal kartı | "Kanal Düzenle — kanal adı" penceresi açılır. | — |
| Oluştur / Kaydet | Pencere altı | Kanalı kaydeder ve pencereyi kapatır. | Yeni kanalda firma, platform tipi ve kanal adı dolu olmalı; düzenlemede kanal adı boş olamaz. En az bir ödeme yöntemi seçili olmalı. |
| İptal | Pencere altı | Değişiklikleri atar. | — |
| Göz ikonu | Şifre tipli şema alanının sağında | Girilen değeri göster/gizle. | — |

## Form alanları

### Temel bilgiler
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Firma | Evet (yalnız yeni kanalda ve "Tümü" görünümünden açıldıysa) | Aranabilir liste; ana firma "(Ana Firma)" ekiyle görünür. Firma hapı seçiliyken ya da firma detayından açıldıysa sorulmaz. |
| Platform Tipi | Evet (yalnız yeni kanalda) | Aktif platform tipleri; "(Pazaryeri)" ya da "(Özel Kanal)" ekiyle. Seçim, aşağıdaki Kimlik Bilgileri / Ayarlar alanlarını belirler. Düzenlemede değiştirilemez. |
| Kanal Adı | Evet | Örn. `Trendyol Ana Mağaza`. |
| Kod | Otomatik | Yalnız yeni kanalda görünür; kanal adından küçük harf + alt çizgi ile üretilir (örn. `trendyol_ana_magaza`). Kayıt sonrası değiştirilemez. |
| Fiyat Tipi | Hayır | `— Yok —`, `Manuel`, `Çarpan`. |
| Çarpan | Fiyat tipi Çarpan ise | Sayı, örn. `1.10` (temel fiyatın çarpılacağı katsayı). |

### Kimlik Bilgileri (API) ve Ayarlar (platform tipine göre)
Bu iki bölüm yalnız seçilen platform tipinin alan şeması varsa görünür (örneğin pazaryeri tiplerinde API anahtarı/şifresi). Şemadaki her alan, tipine göre metin, sayı, tarih (`GG.AA.YYYY`), şifre (göz ikonlu, "(şifreli alan)" etiketli) ya da onay kutusu olarak çizilir; şemada zorunlu işaretli alanlar `*` ile gösterilir. Şeması olmayan tipte "Bu platform tipi için alan şeması tanımlı değil. Platform Tipleri sayfasından şema ekleyebilirsiniz." notu görünür. Şema alanları [Platform Tipleri](/rehber/sistem/platform-tipleri/) ekranından tanımlanır.

### Stok Görünürlüğü
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Stoğu biten ürünleri listede göster | Hayır | İşaretliyse stoğu biten ürünler kanalın ürün listelerinde gösterilmeye devam eder; kapalıyken listelerden düşer. |
| Yalnız bu tarihten sonra açılanlar (boş = tümü) | Hayır | Yalnız üstteki işaretliyken görünür. Tarih girilirse stoğu bitenlerden yalnız stok kartı bu tarihten sonra açılanlar listelenir. |

### Ödeme Yöntemleri
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kart ile Öde (Online) | — | Sitede sunulan ödeme yöntemleri; ayar hiç kaydedilmemişse üçü de açık kabul edilir. |
| Kapıda Nakit Ödeme | — | |
| Kapıda Kart ile Ödeme | — | |
| Kapıda ödeme hizmet bedeli (TL) | Kapıda yöntemlerden biri açıksa görünür | Varsayılan `50`. Sipariş toplamına eklenir. |
| Kapıda ödeme üst sınırı (TL, 0 = sınırsız) | Kapıda yöntemlerden biri açıksa görünür | Varsayılan `3000`. Bu tutarın üstündeki siparişlerde kapıda ödeme sunulmaz. |

Kural: **En az bir ödeme yöntemi seçili olmalı** (hiçbiri seçili değilse kırmızı uyarı görünür ve kayıt reddedilir). Kapalı yöntem sitede hiç gösterilmez; sunucu da bu yöntemle gelen siparişi reddeder. Değişiklik yaklaşık 1 dakika içinde siteye yansır.

### Kargo Gönderimi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Paket bilgileri kargo şirketine gönderilsin | Hayır | Varsayılan açık. Kendi satış kanallarınızda AÇIK olmalı: paket hazırlandığında kargo şirketine bildirilir. Pazaryerlerinde KAPALI bırakın: kargo bilgisini pazaryeri kendisi iletir. Kapalıyken bu kanalın siparişlerinde kargo gönderisi oluşturulmaz. |

### Eski Sistem (ECSGYE) Eşlemesi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Eski platform Id (boş = sipariş senkronu kapalı) | Hayır | Geçici köprü: bu kanalın siparişleri eski sisteme bu platform kimliğiyle yazılır. Boş bırakılırsa bu kanal eski sisteme senkronlanmaz. Ekrandaki ipucu satırı bilinen eşleme numaralarını gösterir. |

### Diğer
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Aktif | Hayır (yalnız düzenlerken) | Kapatılınca kanal `Pasif` olur. |

> **Not:** Bu formda görünmeyen bazı kanal ayarları (vitrin teması ve renk değişkenleri, marka adı, site kök adresi, ürün kartı/ürün listesi ve menü ayarları, sipariş onay politikası, takip/reklam ayarları) başka ekranlardan ya da platform yönetimince yazılır. Kanal formu kaydedilirken bu ayarlar **korunur**, silinmez.

## Durumlar ve iş kuralları
- Kanal durumu: `Aktif` / `Pasif`.
- Platform tipi `Pazaryeri` ise kart ikonunda ve firma detay tablosunda `Pazaryeri` rozeti görünür; pazaryeri mağazalarının operasyonu ayrıca Pazaryerleri modülünden yönetilir.
- Kanal kodu firma içinde benzersizdir; aynı koddan ikinci kanal açılmaya çalışılırsa "Bu kodda bir satış kanalı zaten mevcut." hatası gelir (kod addan üretildiği için aynı adlı ikinci kanal açılamaz).
- Şema dışı mevcut ayarlar (tema, marka, onay politikası vb.) formda görünmese de kayıt sırasında korunur.
- Stok görünürlüğü, ödeme yöntemleri ve kapıda ödeme bedel/limit ayarları siteye yaklaşık 1 dakika içinde yansır.

## Adım adım
### Yeni pazaryeri kanalı açma
1. Sistem › **Satış Kanalları**'nda ilgili firma hapını seçin, **Yeni Kanal**'a tıklayın.
2. **Platform Tipi**'nden pazaryerini seçin; **Kanal Adı** yazın (kod otomatik oluşur).
3. **Kimlik Bilgileri (API)** bölümünde pazaryerinin verdiği anahtar/şifreyi girin.
4. **Kargo Gönderimi** kutusunu kapatın (kargoyu pazaryeri iletir).
5. **Oluştur**'a tıklayın.

### Kapıda ödemeyi kapatma / limit değiştirme
1. Kanal kartına tıklayın.
2. **Ödeme Yöntemleri** bölümünde kapıda yöntemlerin işaretini kaldırın ya da bedel/üst sınırı güncelleyin.
3. **Kaydet**'e tıklayın; yaklaşık 1 dakika içinde sitede geçerli olur.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** `API 1/2` gibi sarı rozet, pazaryeri kimlik bilgilerinin eksik olduğunu gösterir; kartı açıp tamamlayın.

> **Dikkat:** Tüm ödeme yöntemlerini kapatıp kaydetmeye çalışırsanız "En az bir ödeme yöntemi seçili olmalı." uyarısı alırsınız.

> **Dikkat:** Kendi web sitesi kanalında "Paket bilgileri kargo şirketine gönderilsin" kapatılırsa o kanalın siparişleri için kargo gönderisi hiç oluşturulmaz.

> **Not:** Bu platform tipi için alan şeması tanımlı değilse Kimlik Bilgileri/Ayarlar bölümleri çıkmaz; gerekiyorsa Platform Tipleri ekranından şema ekleyin.

## İlgili sayfalar
- [Firmalar](/rehber/sistem/firmalar/)
- [Platform Tipleri](/rehber/sistem/platform-tipleri/)
- [Bildirim Şablonları](/rehber/sistem/bildirim-sablonlari/) (kanal bazlı sipariş onay politikası)
- [Kargo Bölgeleri](/rehber/siparis/kargo-bolgeleri/)
