---
title: Paketleme İstasyonları
route: /fulfillment/packing-stations
group: Sipariş Yönetimi
order: 80
summary: Depolara bağlı fiziksel paketleme istasyonlarının (kod, ad, göz sayısı, OBM işareti, aktiflik) tanımlandığı liste.
---

## Ne işe yarar
Paketleme İstasyonları, depoya bağlı **fiziksel** paketleme noktalarının tanım listesidir: istasyon kodu/barkodu, adı,
göz (slot) sayısı, OBM (Ortak Birleştirme Masası) olup olmadığı ve aktiflik. Günlük operasyonda kullanılan **sanal
masalar** (Masa Aç ile açılan MASA 1, 2, …) bu listeden bağımsızdır; bu sayfa istasyon tanımlarının bakımı içindir.
Sayfa sol menüde yer almaz; adres çubuğuna `/fulfillment/packing-stations` yazılarak açılır.

## Ekran yerleşimi
![Paketleme İstasyonları listesi](img/fulfillment-packing-stations.webp)
1. **Başlık** — "Paketleme İstasyonları", kayıt sayısı, sağda **+ Yeni İstasyon**.
2. **Tablo** — satıra tıklayınca düzenleme penceresi açılır.
3. **Pencere (modal)** — yeni/düzenle formu.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | İstasyon kodu (örn. `PACK-01`); barkodu da aynı değerdir. |
| AD | Serbest ad; boşsa "—". |
| GÖZ SAYISI | İstasyondaki göz (slot) sayısı. |
| OBM | `Evet` / `Hayır` — Ortak Birleştirme Masası olarak işaretli mi. |
| DURUM | `Aktif` (yeşil) / `Pasif`. |
| (son sütun) | "Düzenle →" ipucu; satır tıklanınca pencere açılır. |

Liste aktif ve pasif tüm istasyonları gösterir; filtre ve sayfalama yoktur.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni İstasyon | Başlık sağı | "Yeni Paketleme İstasyonu" penceresi açılır. | — |
| Satıra tıklama | Tablo | "İstasyon: KOD" düzenleme penceresi açılır (kod değiştirilemez). | — |
| Kaydet | Pencere | Kaydeder, listeyi yeniler ve pencereyi kapatır. | Yeni kayıtta Depo + en az 2 karakter kod; göz sayısı > 0. |
| Vazgeç | Pencere | Pencereyi kapatır. | — |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Depo | Evet (yeni) | İstasyonun bağlı olduğu depo; düzenlemede gösterilmez/değiştirilemez. |
| İstasyon Kodu | Evet (yeni) | Büyük harfe çevrilir (örn. `PACK-01`); aynı zamanda istasyon barkodu olur; sonradan değiştirilemez. |
| Ad | Hayır | Serbest metin. |
| Göz (Slot) Sayısı | Evet | 1 ve üzeri; varsayılan 12. |
| OBM | Hayır | İşaretliyse istasyon Ortak Birleştirme Masası'dır. |
| Aktif | Hayır (yalnız düzenleme) | Kaldırılırsa istasyon `Pasif` olur. |

## Durumlar ve iş kuralları
| Durum | Anlamı |
|---|---|
| `active` Aktif | Kullanıma açık. |
| `inactive` Pasif | Düzenlemede "Aktif" kaldırıldı. |

- Operasyon ekranlarındaki masa numaraları (MASA N) bu listeden değil, Koli Duvarı'ndaki **Masa Aç** ile o an boş olan
  en küçük numaradan verilir; masa slot sayısı operasyon profilinden gelir.

## Adım adım
**Yeni istasyon tanımlama**
1. **+ Yeni İstasyon**'a tıklayın.
2. Depo seçin, İstasyon Kodu (örn. `PACK-02`) ve isteğe bağlı ad girin, göz sayısını yazın, gerekiyorsa OBM işaretleyin.
3. **Kaydet**.

**İstasyonu pasife alma**
1. Satıra tıklayın, **Aktif** işaretini kaldırın, **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **Not:** Kod alanı düzenlemede kilitlidir; yanlış kodla açılmış istasyonu pasife alıp yenisini oluşturun.

> **İpucu:** Kaydet butonu pasifse zorunlu alanlardan biri eksiktir (yeni kayıtta depo/kod, her zaman göz sayısı > 0).

## İlgili sayfalar
- [Masa ve Paketleme](/rehber/siparis/masa-ve-paketleme/)
- [Ara Ayrıştırma ve Koli Duvarı](/rehber/siparis/ara-ayristirma/)
