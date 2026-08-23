---
title: Çeviriler
route: /settings/translations
group: Sistem
order: 60
summary: Panelde görünen sabit arayüz metinlerinin gruplar (Genel, Katalog, Siparişler…) ve diller bazında düzenlendiği, yeni metin anahtarlarının eklendiği ekran.
---

## Ne işe yarar
Arayüz Çevirileri ekranı, panelde görünen sabit metinlerin (buton adları, başlıklar, mesajlar) dil bazında karşılıklarını yönetir. Yeni bir dil açıldığında ya da bir ifadenin değiştirilmesi istendiğinde kullanılır. Ürün adı, kategori adı gibi **içerik** çevirileri buradan değil, ilgili kayıtların çok dilli alanlarından yapılır.

## Ekran yerleşimi
![Arayüz Çevirileri — grup listesi ve dil sütunlu çeviri tablosu](img/settings-translations.webp)
1. **Başlık satırı** — "Arayüz Çevirileri"; sağda değişiklik sayacı (`N değişiklik`), **Anahtar Ekle** ve **Kaydet** butonları.
2. **Grup listesi (sol)** — "Grup" başlığı altında: Genel, Katalog, Siparişler, Stok, Müşteriler, POS, Finans, Fulfillment, Pazarlama, CMS, Kimlik.
3. **Arama ve sayaç** — "Anahtar veya değer ara…" kutusu; yanında "N anahtar · M dil".
4. **Çeviri tablosu** — ilk sütun anahtar, sonraki her sütun bir dil; hücreler doğrudan yazılabilir.

## Liste ve filtreler
| Filtre | Ne yapar |
|---|---|
| Grup (sol liste) | Yalnız o grubun anahtarlarını yükler. Grup değiştirince kaydedilmemiş değişiklikler ve arama temizlenir. |
| Anahtar veya değer ara… | Anahtar adında ya da herhangi bir dildeki değerde geçen metne göre süzer. |

| Sütun | Anlamı |
|---|---|
| Anahtar | Metnin iç adı (küçük harf, alt çizgi). |
| Dil sütunları (`TR`, `EN` …) | O dildeki metin; hücre boşsa `[dil]` yer tutucusu görünür. Değiştirilen hücre renkli çerçeve ve küçük nokta ile işaretlenir. |

Boş durumlar: "… grubunda henüz çeviri yok" + **İlk anahtarı ekle** butonu; aramada sonuç yoksa `"…" için sonuç bulunamadı`. Sayfalama yoktur; tablo kendi içinde kayar, başlık satırı sabittir.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Hücreye yazma | Tablo | Değişiklik taslak olarak tutulur, sayaç artar; kaydedilene kadar sunucuya gitmez. | — |
| Kaydet | Sağ üst | Tüm taslak değişiklikleri topluca kaydeder; başarıda buton kısa süre "Kaydedildi" olur. Hata olursa kırmızı "Kaydetme başarısız. Lütfen tekrar deneyin." şeridi çıkar. | En az bir değişiklik olmalı (yoksa pasif). |
| Anahtar Ekle | Sağ üst | "Yeni Anahtar — Grup adı" penceresi açılır. | — |
| İlk anahtarı ekle | Boş grup | Aynı pencere. | — |
| Ekle | Pencere altı | Anahtarı tüm diller için (boş bırakılanlar boş değerle) oluşturur. | Anahtar dolu olmalı. |
| İptal | Pencere altı | Pencereyi kapatır. | — |

## Form alanları (Yeni Anahtar)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Anahtar | Evet | Küçük harf ve alt çizgi; boşluklar alt çizgiye çevrilir, büyük harfler küçülür. Örn. `ornek_metin_anahtari`. Kayıt sonrası değiştirilemez. |
| Dil alanları (`TR`, `EN` …) | Hayır | Her aktif dil için bir kutu; boş bırakılabilir, sonra tablodan doldurulur. |

## Durumlar ve iş kuralları
- Değişiklikler grup bazında taslakta tutulur; **grup değiştirmek taslağı siler** — önce kaydedin.
- Aynı anahtar yeniden eklenirse değerler üzerine yazılır (yeni kayıt açılmaz).
- Dil sütunları, Diller ekranındaki dillerden gelir; yeni dil eklenince sütun otomatik belirir.
- Anahtar silme bu ekranda yoktur.

## Adım adım
### Bir metni başka dilde düzeltme
1. Sistem › **Çeviriler**'de soldan ilgili grubu seçin.
2. Arama kutusuna metni yazıp satırı bulun; ilgili dil hücresine yeni metni yazın.
3. **Kaydet**'e tıklayın; "Kaydedildi" görünene kadar sayfadan ayrılmayın.

### Yeni anahtar ekleme
1. Grubu seçin, **Anahtar Ekle**'ye tıklayın.
2. Anahtarı ve dillerdeki karşılıklarını yazın, **Ekle**'ye basın.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Kaydetmeden grup değiştirirseniz ya da sayfadan çıkarsanız taslak değişiklikler kaybolur; sağ üstteki "N değişiklik" sayacına bakın.

> **İpucu:** Arama hem anahtarda hem değerlerde çalışır; ekranda gördüğünüz Türkçe metni aratarak anahtarı bulabilirsiniz.

## İlgili sayfalar
- [Diller](/rehber/sistem/diller/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
