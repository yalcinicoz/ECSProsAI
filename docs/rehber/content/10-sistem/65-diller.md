---
title: Diller
route: /settings/languages
group: Sistem
order: 65
summary: Panelde ve çok dilli içerik alanlarında kullanılan dillerin (kod, ad, yazı yönü, varsayılan, durum) salt okunur listesi.
---

## Ne işe yarar
Diller ekranı, sistemde tanımlı dilleri gösterir. Çok dilli alanlar (ürün adı, kategori adı, firma adı, şema etiketleri…) bu dillere göre sekmelenir; **varsayılan** dil çoğu formda zorunlu kaynak dildir. Ekran yalnız görüntüleme içindir: dil ekleme/düzenleme panelde yoktur, sistem yönetimi tarafından yapılır.

> **Not:** Bu sayfa sol menüde yer almaz; doğrudan `/settings/languages` adresinden açılır.

## Ekran yerleşimi
![Diller listesi](img/settings-languages.webp)
1. **Başlık** — "Diller" ve "N kayıt — çok dilli içerik alanları bu dillere göre doldurulur" açıklaması.
2. **Tablo** — dil satırları.

## Liste ve filtreler
Arama/filtre ve sayfalama yoktur; pasif diller de listelenir.

| Sütun | Anlamı |
|---|---|
| KOD | Dil kodu (örn. `tr`, `en`). |
| AD | Dilin kendi dilindeki adı (örn. Türkçe, English). |
| YÖN | `Soldan sağa` / `Sağdan sola`. |
| VARSAYILAN | Varsayılan dilde `Varsayılan` rozeti; diğerlerinde `—`. |
| DURUM | `Aktif` / `Pasif`. |

Boş durumda "Tanımlı dil yok." yazar. Satır tıklaması bir şey açmaz.

## Butonlar ve aksiyonlar
Bu ekranda buton yoktur (salt okunur).

## Durumlar ve iş kuralları
- `Aktif` diller çok dilli alanlarda sekme olarak, Çeviriler ekranında sütun olarak görünür.
- `Varsayılan` dil, formlarda zorunlu olan kaynak dildir; diğer diller boş bırakılabilir.
- Sistemle birlikte `tr` (Türkçe, varsayılan) ve `en` (English) gelir.

## Adım adım
### Hangi dillerin aktif olduğunu kontrol etme
1. Adres çubuğuna `/admin/settings/languages` yazın.
2. DURUM sütununda `Aktif` olanlar çok dilli alanlarda kullanılabilir dillerdir.

## İpuçları ve sık karşılaşılan durumlar
> **Not:** Yeni bir dil eklenmesi ya da varsayılanın değişmesi gerekiyorsa sistem yönetimine başvurun; bu ekrandan yapılamaz.

## İlgili sayfalar
- [Çeviriler](/rehber/sistem/ceviriler/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
