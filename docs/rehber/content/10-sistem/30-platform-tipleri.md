---
title: Platform Tipleri
route: /settings/platform-types
group: Sistem
order: 30
summary: Satış kanallarının türlerinin (Web Sitesi, Trendyol, Hepsiburada, Mobil Uygulama, Mağaza/POS vb.) ve her tür için kanal formunda sorulacak alan şemasının yönetildiği ekran.
---

## Ne işe yarar
Platform tipi, bir satış kanalının "ne tür bir kanal" olduğunu söyler: kendi web siteniz mi, bir pazaryeri mi, mobil uygulama mı. Her tip için bir **alan şeması** tanımlanabilir; bu şema, Satış Kanalları ekranında o tipte kanal açılırken sorulacak kimlik bilgisi (API anahtarı, şifre) ve ayar alanlarını üretir. Bu ekranı sistem yöneticisi, yeni bir pazaryeri türü eklerken ya da mevcut bir türün kanal formuna alan eklerken kullanır. Günlük operasyonda nadiren değişir.

## Ekran yerleşimi
![Platform Tipleri listesi](img/settings-platform-types.webp)
1. **Başlık satırı** — "Platform Tipleri" başlığı ve açıklaması; sağda `Tümü` / `Aktif` görünüm anahtarı ve **Yeni Platform Tipi** butonu.
2. **Tablo** — her satır bir platform tipi; satır sonunda **Düzenle** butonu.
3. **Pencere** — oluşturma/düzenleme formu: üstte onay kutuları, çok dilli ad, otomatik kod ve **Kanal Alan Şeması** editörü.

## Liste ve filtreler
| Filtre | Ne yapar |
|---|---|
| Tümü / Aktif | `Tümü` pasif tipleri de gösterir; `Aktif` yalnız aktif olanları listeler. |

| Sütun | Anlamı |
|---|---|
| KOD | Tipin kodu; Türkçe addan otomatik üretilir (küçük harf, alt çizgi). Sonradan değiştirilemez. |
| AD | Tip adı (çok dilli; Türkçe gösterilir). |
| TİP | Yetenek rozetleri: `Pazaryeri` (ürün dışarı gönderilir) / `Dropship bayi` / `Kendi kanal`; ayrıca `Eşleme` (dış kategori eşlemesi gerekir), `Satıcı ürünleri`, `Dış kaynak`, `Stok eşiği N`, `Elle kanala al` görünebilir. |
| ŞEMA ALANLARI | Şemadaki ilk 3 alanın etiketi (kimlik bilgisi alanları sarı, ayar alanları gri), fazlası `+N`; şema yoksa `—`. |
| DURUM | `Aktif` / `Pasif`. |
| (son sütun) | **Düzenle** butonu. |

Satır tıklaması ayrı bir detay açmaz; düzenleme **Düzenle** ile yapılır. Sayfalama yoktur.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Platform Tipi | Sağ üst | "Yeni Platform Tipi" penceresi açılır. | Kaynak dilde ad girilmeden **Oluştur** pasiftir (kod addan üretilir). |
| Düzenle | Satır sonu | "Platform Tipi Düzenle — ad" penceresi açılır. | — |
| Oluştur | Yeni pencere altı | Kaydeder ve pencereyi kapatır. | — |
| Kaydet | Düzenleme penceresi altı | Kaydeder; pencere **kapanmaz**, solda yeşil "Kaydedildi" rozeti birkaç saniye görünür; çalışmaya devam edebilirsiniz. | — |
| Kapat / İptal | Pencere altı | Pencereyi kapatır (kaydedilmemiş değişiklikler atılır). | — |
| Alan Ekle | Şema editörü altı (kesikli çerçeveli buton) | Şemaya boş bir alan satırı ekler. | — |
| Çöp kutusu ikonu | Şema alanı satırının sonu | Alanı şemadan kaldırır. | ⚠️ Kaydedince kanal formundan alan kaybolur; mevcut kanallardaki kayıtlı değerler silinmez ama formda gösterilmez. |
| Dil sekmeleri (TR / EN …) | Şema alanı kartında "Etiket:" satırı | Alan etiketini o dilde düzenler; dolu dilin yanında küçük nokta görünür. | — |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Yetenekler (kanal davranışı) | Hayır | Bu tipteki kanalların varsayılan davranışı. Üstteki **Pazaryeri şablonu / Kendi kanal şablonu / Dropship bayi şablonu** butonları tüm alanları tek seferde doldurur; sonra tek tek değiştirilebilir. Alanlar aşağıda. |
| Aktif | Hayır (yalnız düzenlerken) | Pasif tip, Satış Kanalları formundaki Platform Tipi listesinde görünmez. |
| Ad (çok dilli) | Kaynak dilde evet | Dil sekmeli alan; diğer diller isteğe bağlı. |
| Otomatik Kod | Otomatik (yalnız yeni) | Türkçe addan üretilir (örn. "Çiçeksepeti" → `ciceksepeti`). Kayıt sonrası değiştirilemez. |
| Kanal Alan Şeması | Hayır | Aşağıdaki şema editörü. Boşsa "Henüz alan tanımlanmadı." yazar. |

### Yetenekler — alanlar
| Alan | Açıklama |
|---|---|
| Ürün dışarı gönderilir (pazaryeri) | Açıksa ürünler batch/adaptörle karşı tarafa yüklenir ve tip **Pazaryeri** sayılır (Pazaryerleri modülünde mağaza olarak görünür, kanal kartında çanta ikonu). |
| Dış kategori/özellik eşlemesi gerekir | Ürün grubu → dış kategori, özellik/değer eşlemeleri bu kanal için zorunludur. |
| Hazırlık denetimi | Ürünün kanalda listelenebilmesi için ön kontrol seviyesi: `Hafif` (görsel, fiyat, satış açık), `Hafif + kanal fiyatı var`, `Tam` (eşleme + zorunlu özellik). |
| Fiyat kaynağı | `Kanal fiyat tipi` / `Bayi fiyat listesi` / `Kanal fiyatı + pazaryeri geri okuma`. |
| Satış durdurma penceresi | Ürün satışı tarih aralığıyla geçici durdurulabilir. |
| Listeden düşürme batch'i | Pazaryerinde yüklü ürün uzaktan pasife alınabilir. |
| Üçüncü taraf satıcı ürünleri | Satıcı panelinden gelen ürünler bu kanalın kapsamına girebilir (kanal bazında ezilebilir). |
| Dış tedarik kaynağı ürünleri | Dropship tedarik kaynaklarından gelen ürünler kapsama girebilir (kanal bazında ezilebilir). |
| Sipariş yönü | `İçeride oluşur` (site/POS) / `Bayi Partner API ile gönderir` / `Pazaryerinden çekilir`. |
| Stok eşiği (minStock) | Kanala verilen adet = **max(0, net stok − eşik + 1)**. Eşik 3 iken net 3 adet → kanala 1 verilir, net 2 → 0 (satılamaz). Kanal bazında ezilebilir. |
| Kapsama giren ürün otomatik kanalda | Kapalıysa ürün kapsama girse de personel "Kanala al" demeden satışa açılmaz (kanal bazında ezilebilir). |
| Karşı taraf bizim Partner API'mizi kullanır | Dropship bayi ürün/stok/fiyatı bizim API'mizden çeker. |

### Şema editörü — her alan için
| Sütun / kutu | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| # | — | Sıra numarası. |
| ANAHTAR | Evet | Alanın iç adı; boşluklar alt çizgiye çevrilir, küçük harfe indirilir. Örn. `api_key`, `api_secret`, `magaza_id`. |
| TİP | Evet | `Metin`, `Şifre`, `Sayı`, `Tarih`, `Evet/Hayır`. Şifre tipli alan kanal formunda göz ikonlu gizli kutu olarak çizilir. |
| BÖLÜM | Evet | `Kimlik Bilgileri` (API anahtarı, şifre — hassas veriler) ya da `Ayarlar` (sözleşme tarihi, komisyon vb.). |
| ZOR. | Hayır | İşaretliyse kanal oluşturulurken alan `*` ile zorunlu gösterilir. |
| Etiket (dil sekmeli) | Önerilir | Kanal formunda görünen ad; boşsa anahtar gösterilir. |

Editörün altındaki not: *Kimlik Bilgileri: API anahtarı, şifre — hassas veriler. Ayarlar: Sözleşme tarihi, komisyon vb. Zor.: Kanal oluşturulurken zorunlu alan.*

## Durumlar ve iş kuralları
- Durum: `Aktif` / `Pasif`. Pasif tip yeni kanal açarken seçilemez; mevcut kanallar etkilenmez.
- Kod benzersizdir: aynı addan ikinci tip oluşturulursa "Bu kodda bir platform tipi zaten mevcut." hatası gelir.
- Şema, **yalnız kanal formunu** şekillendirir; Kimlik Bilgileri bölümündeki değerler kanal kaydında tutulur. (Firma entegrasyonlarının şeması ise ayrı bir ekranda, Servis Kataloğu'nda tanımlanır.)
- Varsayılan tipler (Web Sitesi, Trendyol, Hepsiburada, n11, Amazon, Çiçeksepeti, Pazarama, Mobil Uygulama, Mağaza / POS, **Dropship Bayi**) sistemle birlikte gelir; yetenekleri koda göre önceden doldurulmuştur.
- Ekranlar kanalın **tipine değil yeteneklerine** bakar: "Pazaryeri" ayrı bir tip değil, "Ürün dışarı gönderilir" yeteneğinin açık olmasıdır. Yalnız dört yetenek (satıcı ürünleri, dış kaynak, otomatik kanala al, stok eşiği) kanal bazında ezilebilir; diğerleri tipten gelir.

## Adım adım
### Yeni pazaryeri tipi ekleme
1. Sistem › **Platform Tipleri**'nde **Yeni Platform Tipi**'ne tıklayın.
2. Yetenekler bölümünde **Pazaryeri şablonu**'na tıklayın (ürün dışarı gönderilir + eşleme + tam hazırlık + pazaryerinden sipariş çekme); kaynak dilde **Ad** yazın (kod otomatik oluşur).
3. **Alan Ekle** ile `api_key` (Şifre, Kimlik Bilgileri, zorunlu) gibi alanlar ekleyin; etiketlerini girin.
4. **Oluştur**'a tıklayın. Artık Satış Kanalları'nda bu tipte kanal açılabilir.

### Mevcut tipe alan ekleme
1. Satırda **Düzenle**'ye tıklayın; **Alan Ekle** ile yeni alanı tanımlayın.
2. **Kaydet**'e basın; "Kaydedildi" rozetini görün, **Kapat** ile çıkın.
3. İlgili kanalları açıp yeni alanı doldurun (kart üzerindeki `API x/y` rozeti eksik alanları gösterir).

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Düzenleme penceresinde **Kaydet** pencereyi kapatmaz; birkaç değişikliği art arda kaydedebilirsiniz.

> **Dikkat:** Bir alanı silmek geri alınamaz bir şema değişikliğidir; kanallarda girilmiş değerler formda görünmez olur.

> **Not:** Şemasız bir tipte kanal açarken kanal formu "Bu platform tipi için alan şeması tanımlı değil." notu gösterir; sorun değil, kanal yine oluşturulur.

## İlgili sayfalar
- [Satış Kanalları](/rehber/sistem/satis-kanallari/)
- [Firmalar](/rehber/sistem/firmalar/)
- [Servis Kataloğu](/rehber/sistem/servis-katalogu/)
