---
title: Yorum Moderasyonu
route: /storefront/reviews
group: Vitrin
order: 55
summary: Üyelerin ürünlere yazdığı yorumların yayın öncesi onaylandığı veya nedeniyle reddedildiği kuyruk; ürün puanları yalnız onaylı yorumlardan hesaplanır.
---

## Ne işe yarar
Üyelerin ürün sayfasından gönderdiği **yorum ve puanlar** sitede ancak onaylandıktan sonra yayınlanır; ürün
kartındaki ve detaydaki puan/yorum sayısı da yalnız onaylı yorumlardan hesaplanır. Bu ekran yayının kapısıdır:
içerik/müşteri ilişkileri sorumlusu bekleyen yorumları okur, fotoğraflarını kontrol eder, uygun olanı onaylar,
uygun olmayanı bir nedenle reddeder (neden üyeye "Reddedilenler" sekmesinde gösterilir).

## Ekran yerleşimi
![Yorum Moderasyonu — durum sekmeleri ve yorum tablosu](img/storefront-reviews.webp)
1. **Başlık** ve açıklama ("Ürün puanları yalnız onaylı yorumlardan hesaplanır; onay kuyruğu yayının kapısıdır.").
2. **Durum sekmeleri** — Onay Bekleyen · Onaylı · Reddedilen (açılışta Onay Bekleyen).
3. **Yorum tablosu** ve altta sayfalama (birden çok sayfa varsa).

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| Ürün / Üye | Ürün kodu; altında yorumu yazan üyenin adı. |
| Puan | 1–5 yıldız (`★★★★☆`). |
| Yorum | Yorum metni (`—` boşsa); varsa konu etiketi (gri kutu), yorum fotoğrafları (küçük resim — tıklayınca yeni sekmede açılır) ve reddedilmişse kırmızı `Red nedeni: …` satırı. |
| Tarih | Yorumun yazıldığı tarih. |
| Durum | `Onay Bekliyor` (sarı), `Onaylı` (yeşil), `Reddedildi` (kırmızı). |
| İşlem | Duruma göre **Onayla** ve/veya **Reddet**. |

| Filtre | Ne yapar |
|---|---|
| Durum sekmeleri | Listeyi seçilen duruma göre süzer; sekme değişince 1. sayfaya döner. |

Arama, ürün ya da kanal filtresi yoktur. Satır tıklanınca detay açılmaz. Liste boşsa "Bu durumda yorum yok." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Onayla | Satır, İşlem sütunu | Yorum `Onaylı` olur, sitede yayınlanır ve ürün puanına dahil edilir. Onay sorulmaz. | `Onaylı` olmayan satırlarda. |
| Reddet | Satır, İşlem sütunu | Tarayıcı penceresi **"Red nedeni (üyeye gösterilir):"** açılır; varsayılan metin `Yayın kriterlerine uygun değil.` Tamam → yorum `Reddedildi` olur, neden kaydedilir. İptal → işlem yapılmaz. | `Reddedildi` olmayan satırlarda. |
| Fotoğraf küçük resmi | Yorum sütunu | Fotoğrafı yeni sekmede tam boy açar. | Fotoğraflı yorumlarda. |
| Sayfalama | Tablo altı | Sayfalar arasında geçiş. | Birden çok sayfa varsa. |

## Durumlar ve iş kuralları
- Durum akışı: `Onay Bekliyor` → `Onaylı` ya da `Reddedildi`; onaylı yorum sonradan reddedilebilir, reddedilen
  sonradan onaylanabilir (onaylanınca red nedeni silinir).
- Sitede yalnız `Onaylı` yorumlar görünür; kart/detay puan ortalaması ve yorum sayısı yalnız onaylılardan hesaplanır.
- Red nedeni boş bırakılırsa `Yayın kriterlerine uygun değil.` yazılır; üye kendi "Yorumlarım → Reddedilenler"
  sekmesinde bu nedeni görür.
- Eski sistemden aktarılan onaysız yorumlar da bu kuyruğa düşer.

## Adım adım
**Bekleyen yorumları yayına alma**
1. **Onay Bekleyen** sekmesinde metni ve varsa fotoğrafları (tıklayıp büyüterek) inceleyin.
2. Uygunsa **Onayla**; değilse **Reddet** → nedeni düzenleyin (ör. "Kişisel bilgi içeriyor") → Tamam.
3. Onaylanan yorum kısa süre içinde ürün sayfasında ve kart puanında görünür.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Onayla anında yayınlar; geri almak için Onaylı sekmesinden **Reddet** kullanın (üyeye neden gösterilir).

> **İpucu:** Red nedenini açık ve kısa yazın — üye bunu görür ve yorumunu düzeltip yeniden gönderebilir.

> **Not:** Ürün Kartı sayfasında "Puan + yorum sayısı" öğesi kapalıysa onaylı yorumlar kartta görünmez, ürün
> detayında görünmeye devam eder.

## İlgili sayfalar
- [Ürün Kartı](/rehber/vitrin/urun-karti/)
- [Koleksiyon Moderasyonu](/rehber/vitrin/koleksiyon-moderasyonu/)
- [Üyeler](/rehber/musteriler/uyeler/)
