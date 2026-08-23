---
title: Lookup Tipleri
route: /settings/lookup-types
group: Sistem
order: 70
summary: Sipariş durumu, ödeme yöntemi, cinsiyet gibi referans listelerinin (lookup tipi) ve değerlerinin görüntülenip yönetildiği ekran.
---

## Ne işe yarar
Lookup tipleri, sistemin çeşitli yerlerinde açılır liste olarak kullanılan referans listeleridir (örn. sipariş durumları, ödeme yöntemleri, cinsiyet). Bu ekranda mevcut tipler ve değerleri görülür; yeni tip açılabilir, tipe değer eklenip düzenlenebilir (ad, sıra, renk, varsayılan, aktif). Sistem yöneticisi kullanır; günlük operasyonda nadiren değişir.

> **Not:** Bu sayfa sol menüde yer almaz; doğrudan `/settings/lookup-types` adresinden açılır.

## Ekran yerleşimi
![Lookup Tipleri — sol tip listesi, sağ değer listesi](img/settings-lookup-types.webp)
1. **Başlık satırı** — "Lookup Tipleri", "N tip — sipariş durumu, ödeme yöntemi gibi referans listeleri"; sağda **+ Yeni Tip**.
2. **Tip listesi (sol kart)** — her tip için ad, altında kod ve sistem tipiyse "· sistem" notu. Tıklanınca sağda değerleri açılır.
3. **Değer listesi (sağ kart)** — "… değerleri" başlığı, **+ Değer Ekle** butonu ve değer satırları.
4. **Pencereler** — "Yeni Lookup Tipi", "Yeni Değer" / "Değer: ad".

## Liste ve filtreler
Arama ve sayfalama yoktur.

| Tip satırı | Anlamı |
|---|---|
| Ad | Tipin adı. |
| Kod · sistem | Tipin kodu; sistemle gelen tiplerde "sistem" notu. |

| Değer satırı | Anlamı |
|---|---|
| Renk noktası | Değere renk tanımlıysa renkli yuvarlak. |
| Ad | Değerin adı. |
| `Varsayılan` rozeti | Varsayılan değer. |
| `Aktif` / `Pasif` rozeti | Durum (pasif değerler de listelenir). |
| Düzenle → | Satıra tıklayınca değer penceresi açılır. |

Tip seçili değilken sağda "Değerlerini görmek için soldan bir tip seçin." yazar; tipin değeri yoksa "Bu tipin değeri yok."

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Tip | Sağ üst | "Yeni Lookup Tipi" penceresi açılır. | Kod en az 2 karakter ve ad dolu olmadan **Kaydet** pasif. |
| Tip satırına tıklama | Sol kart | Tipin değerleri sağda listelenir. | — |
| + Değer Ekle | Değer kartı başlığı | "Yeni Değer" penceresi açılır. | Bir tip seçili olmalı. |
| Değer satırına tıklama | Değer listesi | "Değer: ad" düzenleme penceresi açılır. | — |
| Kaydet | Pencere altı | Kaydeder, pencereyi kapatır, listeyi yeniler. | Ad dolu olmalı. |
| Vazgeç | Pencere altı | Pencereyi kapatır. | — |

Silme butonu yoktur; değeri kullanımdan kaldırmak için **Aktif** kutusunu kapatın.

## Form alanları

### Yeni Lookup Tipi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (en az 2 karakter) | Küçük harfe çevrilir, boşluklar alt çizgi olur. Örn. `cinsiyet`. Benzersiz olmalı: aynı kodla ikinci tip "'kod' kodu zaten mevcut." hatası verir. |
| Ad | Evet | Türkçe ad. Örn. `Cinsiyet`. |
| Açıklama | Hayır | Serbest açıklama. |

### Yeni Değer / Değer düzenleme
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ad | Evet | Değerin Türkçe adı. |
| Sıra | Hayır | Sayı; listelerde sıralama (varsayılan 0). |
| Renk | Hayır | `#hex` biçiminde, örn. `#10b981`; rozet/nokta rengi. |
| Varsayılan | Hayır | İşaretliyse listelerde varsayılan seçim. |
| Aktif | Hayır (yalnız düzenlerken) | Kapalı değer seçim listelerinde sunulmaz. |

## Durumlar ve iş kuralları
- Değer durumu: `Aktif` / `Pasif`; `Varsayılan` ayrıca işaretlenir.
- Tip kodu kayıt sonrası değiştirilemez; tip düzenleme/silme penceresi yoktur.
- Sistemle gelen ("sistem" notlu) tipler uygulamanın işleyişinde kullanılır; değerlerini pasif yaparken dikkatli olun.
- Kurulumla gelen örnekler: sipariş durumları, ödeme yöntemleri, cinsiyet (male/female/unisex).

## Adım adım
### Bir tipe yeni değer ekleme
1. Adres çubuğundan `/admin/settings/lookup-types` açın; soldan tipi seçin.
2. **+ Değer Ekle**'ye tıklayın; **Ad**, gerekirse **Sıra** ve **Renk** girin; **Kaydet**.

### Bir değeri kullanımdan kaldırma
1. Değer satırına tıklayın; **Aktif** kutusunu kaldırın; **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** "sistem" notlu tiplerin değerlerini pasif yapmak ya da varsayılanını değiştirmek uygulamanın ilgili akışlarını etkileyebilir; emin değilseniz değiştirmeyin.

> **Not:** "Lookup type '…' bulunamadı." hatası, değer eklenirken tipin silinmiş/değişmiş olduğunu gösterir; sayfayı yenileyin.

## İlgili sayfalar
- [Diller](/rehber/sistem/diller/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
