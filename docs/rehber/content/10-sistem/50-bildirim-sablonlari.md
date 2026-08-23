---
title: Bildirim Şablonları
route: /settings/notification-templates
group: Sistem
order: 50
summary: Sipariş onayı SMS ve e-posta şablonlarının (yer tutucularla) düzenlendiği ve kanal bazlı sipariş onay politikasının (kapıda/kartla ödemede onay isteme, onay linki ömrü) ayarlandığı ekran.
---

## Ne işe yarar
Bazı siparişlerde müşteriden, kendisine gönderilen bağlantıya tıklayarak siparişi **onaylaması** istenir (özellikle kapıda ödemeli siparişlerde sahte/şaka siparişleri elemek için). Bu ekranda o SMS'in ve e-postanın metni düzenlenir, önizlenir; ayrıca her satış kanalı için hangi siparişlerde onay isteneceği ve onay bağlantısının kaç saat geçerli olacağı belirlenir. Onay bağlantısına tıklayan müşteri siparişini onaylar; onaylı sipariş operasyona (ve eski sisteme "Hazırlanıyor" olarak) geçer.

## Ekran yerleşimi
![Bildirim Şablonları — SMS şablonu, e-posta şablonu ve onay politikası kartları](img/settings-notification-templates.webp)
1. **Başlık ve açıklama** — altında hata (kırmızı) / bilgi (yeşil) mesaj satırı.
2. **SMS ŞABLONU (Sipariş Onayı)** kartı — yer tutucu çipleri, metin kutusu, karakter sayacı, ÖNİZLEME, **SMS Şablonunu Kaydet**.
3. **E-POSTA ŞABLONU (Sipariş Onayı)** kartı — yer tutucular, Konu, Gövde (HTML), ÖNİZLEME, **E-posta Şablonunu Kaydet**.
4. **ONAY POLİTİKASI (kanal bazlı)** kartı — Firma ve Kanal seçimi, üç politika alanı, **Politikayı Kaydet**.

## Liste ve filtreler
Bu ekranda liste yoktur. Onay politikası kartındaki **Firma** ve **Kanal** açılır listeleri hangi kanalın ayarının görüntülenip kaydedileceğini seçer (ilk firma ve ilk kanal otomatik seçili gelir).

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yer tutucu çipleri | Şablon kartlarının üstü | Üzerine gelince açıklaması görünür; metne elle kopyalayıp yerleştirirsiniz (tıklama eklemez). | — |
| SMS Şablonunu Kaydet | SMS kartı | SMS metnini Türkçe şablon olarak kaydeder; "Şablon kaydedildi." mesajı. | — |
| E-posta Şablonunu Kaydet | E-posta kartı | Konu + HTML gövdeyi kaydeder. | — |
| Politikayı Kaydet | Onay politikası kartı | Seçili kanal için kapıda/kart politikası ve link ömrünü kaydeder; "Onay politikası kaydedildi (siteye ~1 dk içinde yansır)." | Kanal seçili olmalı. |

## Form alanları

### Yer tutucular (her iki şablonda)
| Yer tutucu | Gönderimde yerine gelen |
|---|---|
| `{ad}` | Alıcı adı |
| `{soyad}` | Alıcı soyadı |
| `{siparisNo}` | Sipariş numarası |
| `{tutar}` | Sipariş tutarı |
| `{link}` | Onay bağlantısı |
| `{sure}` | Link ömrü (saat) |

Önizleme örnek verilerle doldurulur (Ayşe Yılmaz, 1.249,90 vb.); gerçek değerler gönderim anında yerleştirilir.

### SMS şablonu
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Metin | Evet | Varsayılan: `Sayin {ad} {soyad}, {siparisNo} nolu siparisinizi onaylamak icin: {link} (Link {sure} saat gecerlidir.)`. Altında "N karakter" sayacı: değişkenler doldurulunca uzunluk değişir; Türkçe karakter SMS maliyetini artırabilir. |

### E-posta şablonu
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Konu | Evet | Varsayılan: `{siparisNo} — Siparişinizi Onaylayın`. |
| Gövde (HTML) | Evet | HTML yazılabilir; varsayılan metin onay bağlantısını `<a href="{link}">` ile verir. Önizleme gövdeyi olduğu gibi çizer. |

### Onay politikası (kanal bazlı)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Firma | Evet | Politikası düzenlenecek firma. |
| Kanal | Evet | Firmanın satış kanalı; politika kanal başına ayrı tutulur. |
| Kapıda ödemeli sipariş | — | `Her zaman onay iste` (varsayılan) / `Onay isteme`. |
| Kartla ödenen sipariş | — | `Yalnız ilk sipariş/misafir` (varsayılan) / `Her zaman onay iste` / `Onay isteme`. |
| Link ömrü (saat) | Evet | 1–168 arası; varsayılan 24. Aralık dışı değerde "Link ömrü 1-168 saat aralığında olmalı." hatası. |

## Durumlar ve iş kuralları
- Şablonlar şimdilik tek tip içindir: **sipariş onayı**; SMS ve e-posta kanalları için ayrı kayıt tutulur (Türkçe). Hiç kaydedilmemişse ekrandaki varsayılan metinler kullanılır.
- Onay isteyen politika geçerliyse sipariş, müşteri bağlantıya tıklayana kadar onaysız bekler; bağlantı süresi **Link ömrü** kadardır.
- Kartla ödenen siparişte politika onay istiyorsa ödeme alınsa bile sipariş otomatik onaylanmaz; müşteriye onay bağlantısı gönderilir.
- Politika ve şablon değişiklikleri yaklaşık 1 dakika içinde siteye/gönderimlere yansır.
- SMS ve e-posta gönderimi için firmada aktif SMS ve e-posta (SMTP) entegrasyonu tanımlı olmalıdır (Firmalar › Entegrasyonlar).

## Adım adım
### SMS metnini değiştirme
1. Sistem › **Bildirim Şablonları**'nı açın.
2. SMS kartındaki metni düzenleyin; yer tutucuları (`{link}` mutlaka) koruyun. Önizlemeyi kontrol edin.
3. **SMS Şablonunu Kaydet**'e tıklayın; yeşil "Şablon kaydedildi." mesajını görün.

### Bir kanalda kart siparişlerinde onayı kapatma
1. Onay politikası kartında **Firma** ve **Kanal**'ı seçin.
2. **Kartla ödenen sipariş** = `Onay isteme` yapın.
3. **Politikayı Kaydet**'e tıklayın.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** `{link}` yer tutucusunu şablondan çıkarırsanız müşteri onay bağlantısı alamaz ve onay isteyen siparişler takılı kalır.

> **İpucu:** SMS'te Türkçe karakter kullanmak mesaj sayısını (maliyeti) artırabilir; varsayılan metin bu yüzden Türkçe karaktersiz yazılmıştır.

> **Not:** Firma/Kanal değiştirildiğinde politika alanları o kanalın kayıtlı değerlerine döner; kaydedilmemiş değişiklikler kaybolur.

## İlgili sayfalar
- [Satış Kanalları](/rehber/sistem/satis-kanallari/)
- [Firmalar](/rehber/sistem/firmalar/) (SMS / e-posta entegrasyonları)
- [Siparişler](/rehber/siparis/siparisler/)
