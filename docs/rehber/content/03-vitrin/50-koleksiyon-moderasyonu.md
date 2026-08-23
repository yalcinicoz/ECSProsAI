---
title: Koleksiyon Moderasyonu
route: /storefront/collections
group: Vitrin
order: 50
summary: Üyelerin sitede oluşturduğu koleksiyonların onaylandığı veya reddedildiği kuyruk; yalnız onaylı ve herkese açık koleksiyonlar vitrindeki "Koleksiyonlar" bloğunda gösterilebilir.
---

## Ne işe yarar
Üyeler sitede beğendikleri ürünlerden **koleksiyon** oluşturup herkese açık yapabilir ve paylaşabilir. Herkese
açık bir koleksiyonun vitrinde (ana sayfadaki "Koleksiyonlar" bloğunda) görünebilmesi için burada
**onaylanması** gerekir. İçerik/pazarlama sorumlusu düzenli olarak "Onay Bekleyen" kuyruğunu gözden geçirir;
uygunsuz ad/açıklama taşıyan koleksiyonları reddeder. Moderasyon üyenin koleksiyonu kendi hesabında
kullanmasını engellemez; yalnız vitrin görünürlüğünü belirler.

## Ekran yerleşimi
![Koleksiyon Moderasyonu — durum sekmeleri ve koleksiyon tablosu](img/storefront-collections.webp)
1. **Başlık** ve açıklama ("Onaylı + herkese açık koleksiyonlar vitrin 'Koleksiyonlar bloğu'nda kullanılabilir.").
2. **Durum sekmeleri** — Onay Bekleyen · Onaylı · Reddedilen (açılışta Onay Bekleyen).
3. **Koleksiyon tablosu** ve altta sayfalama (birden çok sayfa varsa).

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| Koleksiyon | Koleksiyon adı; varsa altında açıklaması. Üyenin otomatik "Kaydedilenler" koleksiyonu `(otomatik — Kaydedilenler)` notuyla gösterilir. |
| Görünürlük | `Herkese açık` ya da `Gizli`; paylaşıma açıksa ` · Paylaşılabilir` eklenir. |
| Ürün | Koleksiyondaki ürün sayısı. |
| Oluşturulma | Üyenin oluşturduğu tarih. |
| Durum | `Onay Bekliyor` (sarı), `Onaylı` (yeşil), `Reddedildi` (kırmızı). |
| İşlem | Duruma göre **Onayla** ve/veya **Reddet** butonları. |

| Filtre | Ne yapar |
|---|---|
| Durum sekmeleri | Listeyi seçilen duruma göre süzer; sekme değişince 1. sayfaya döner. |

Arama kutusu ve kanal filtresi yoktur; tüm kanalların koleksiyonları birlikte listelenir. Satır tıklanınca detay
açılmaz. Liste boşsa "Bu durumda koleksiyon yok." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Onayla | Satır, İşlem sütunu | Koleksiyon `Onaylı` olur; herkese açıksa vitrin bloğunda listelenebilir. Onay sorulmaz, anında uygulanır. | `Onaylı` olmayan satırlarda görünür. |
| Reddet | Satır, İşlem sütunu | Koleksiyon `Reddedildi` olur; vitrinde gösterilmez. Onay sorulmaz. | `Reddedildi` olmayan satırlarda görünür. |
| Sayfalama | Tablo altı | Sayfalar arasında geçiş. | Birden çok sayfa varsa. |

## Durumlar ve iş kuralları
- Durum akışı: `Onay Bekliyor` → `Onaylı` ya da `Reddedildi`. Onaylı bir koleksiyon sonradan reddedilebilir,
  reddedilen sonradan onaylanabilir (ilgili buton görünür kalır).
- Vitrindeki "Koleksiyonlar" bloğu yalnız **Onaylı + Herkese açık** koleksiyonları listeler; gizli koleksiyon
  onaylansa bile vitrinde görünmez.
- Moderasyon tarihi kaydedilir; üye kendi koleksiyon sayfasında durumunu görür.
- "Kaydedilenler" (otomatik) koleksiyonları üyenin hızlı kaydetme listesidir; genelde onaylanmasına gerek yoktur.

## Adım adım
**Onay kuyruğunu temizleme**
1. **Onay Bekleyen** sekmesinde adı/açıklamayı ve ürün sayısını kontrol edin.
2. Uygun olanlara **Onayla**, uygunsuzlara **Reddet** deyin; satır listeden düşer.
3. Vitrinde göstermek için Vitrin Yönetimi'nde "Koleksiyonlar" bloğu ekleyin (yalnız onaylı + herkese açık olanlar gelir).

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Onayla/Reddet anında uygulanır ve onay sormaz; yanlış işlemi diğer sekmeden bulup ters butonla düzeltin.

> **Not:** Onaylı koleksiyon vitrinde görünmüyorsa Görünürlük sütununu kontrol edin — `Gizli` koleksiyonlar
> vitrine çıkmaz; ayrıca blok yayınlanmış olmalıdır.

## İlgili sayfalar
- [Vitrin Yönetimi](/rehber/vitrin/vitrin-yonetimi/)
- [Yorum Moderasyonu](/rehber/vitrin/yorum-moderasyonu/)
- [Üyeler](/rehber/musteriler/uyeler/)
