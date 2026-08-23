---
title: Kampanya Tipleri
route: /promotion/campaign-types
group: Pazarlama
order: 15
summary: Tanımlı kampanya tiplerinin ve her tipin parametre şablonunun salt-okunur görüntülendiği ekran; kampanya oluştururken seçilecek tipleri tanımak için kullanılır.
---

## Ne işe yarar
Kampanya tipleri, kampanya oluştururken seçtiğiniz hazır kalıplardır ("İndirim", "Al X, Y Bedava/İndirimli",
"Kargo Kampanyası" vb.). Her tipin, kampanya formundaki **Parametreler** sekmesini üreten bir şablonu vardır. Bu ekran
tipleri ve şablonlarını **yalnız görüntüler**; yeni tip eklenemez, mevcutlar düzenlenemez (tipler platform
yönetimince tanımlanır). Bir kampanya açmadan önce hangi tipin hangi alanları istediğini burada inceleyebilirsiniz.

## Ekran yerleşimi
![Kampanya Tipleri listesi](img/promotion-campaign-types.webp)
1. **Başlık** — "Kampanya Tipleri" ve "Tanımlı kampanya tipleri ve parametre şablonları (salt-okunur). N tip".
2. **Tablo** — tip satırları; satıra tıklayınca detay penceresi açılır.
3. **Detay penceresi** — tipin açıklaması, özellik rozetleri ve parametre şablonu tablosu.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Tipin kodu (`discount`, `buy_x_get_y`, `cross_group_gift`, `bundle`, `free_shipping`, `review_reward`). |
| AD | Tipin adı (ör. "İndirim (Kapsam+Koşul+Fayda)"). |
| KAPSAM | Tipin etki alanı: `Ürün`, `Sepet`, `Kargo`, `Üye`. |
| PARAMETRE | Şablondaki alan sayısı ("6 alan"). |
| ÖZELLİK | Rozetler: `Ürün` (kampanyada ürün seçimi ister), `Kart fiyatı` (kartta kampanyalı fiyat gösterilebilir), `Stack` (başka kampanyalarla birleşebilir). |
| DURUM | `Aktif` / `Pasif`. Pasif tipler kampanya formunda seçilmemelidir (eski birleştirilmiş tipler pasiftir). |
| (son sütun) | "Detay →" ipucu. |

Filtre/arama yoktur; liste sıra numarasına göre gelir, sayfalama yoktur.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Satır tıklama | Tablo | "Kampanya Tipi: KOD" başlıklı detay penceresi açılır. | Panele giriş yeterli. |
| Pencereyi kapat (×) | Detay penceresi | Pencere kapanır; hiçbir şey değişmez. | — |

Bu ekranda kaydetme/silme yoktur.

## Detay penceresi
| Bölüm | İçerik |
|---|---|
| Ad ve açıklama | Tipin adı ve kısa açıklaması (ör. "Her X+Y adetlik grupta Y adet bedava/indirimli…"). |
| Rozetler | `Kapsam: Ürün/Sepet/Kargo/Üye` · `Ürün seçimi (tümü/filtre/manuel)` · `Kartta kampanyalı fiyat` · `Birleşebilir (stackable)` · `Aktif`/`Pasif`. |
| PARAMETRE ŞABLONU (n alan) | Tablo: **ALAN** (etiket + birim + yardım metni) · **ANAHTAR** (iç ad) · **TİP** (Yüzde, Tutar (₺), Tam sayı, Sayı, Evet/Hayır, Seçim) · **ZORUNLU** (Evet / —) · **DEĞERLER / KOŞUL** (seçim seçenekleri, "görünür: alan = değer" koşulu, min/max). |
| Alt not | Tiplerin platformdan bağımsız olduğunu ve kampanya oluşturma ekranından uygulanacağını hatırlatır. |

## Durumlar ve iş kuralları
| Tip | Kapsam | Ürün seçimi | Kartta fiyat | Birleşebilir | Ne yapar |
|---|---|---|---|---|---|
| İndirim (Kapsam+Koşul+Fayda) | Ürün | Evet | Evet | Hayır | Sepete ya da kapsamdaki ürünlere yüzde/tutar indirim; isteğe bağlı eşik (sepet tutarı/adedi, kapsam tutarı/adedi) ve yüzdede tavan. |
| Al X, Y Bedava/İndirimli | Ürün | Evet | Hayır | Hayır | Her X+Y adetlik grupta Y adet bedava/indirimli (1 alana 1 bedava, 3 al 2 öde, ikincisi %50). |
| Grup Al → Grup Hediye/İndirimli | Ürün | Evet | Hayır | Hayır | A grubundan alım koşulu sağlanınca B grubundan ürün bedava/indirimli. |
| Kombin İndirimi | Ürün | Evet | Hayır | Hayır | Belirli ürünler birlikte alınınca paket fiyatı/indirim. |
| Kargo Kampanyası | Kargo | Hayır | Hayır | Evet | Sepet eşiği / ödeme yöntemine göre ücretsiz ya da indirimli kargo. |
| Resimli Yorum Kampanyası | Üye | Hayır | Hayır | Evet | Fotoğraflı yorum yapan üyeye ödül; tetiği satın alma değildir. |

- "Kartta kampanyalı fiyat" yalnız İndirim tipinde ve koşulsuz + kapsamdaki ürünlere uygulandığında mümkündür; diğer
  tiplerde kartta yalnız kampanya bandı görünür, indirim sepette hesaplanır.
- Birleşemeyen tiplerde aynı ürüne denk gelen kampanyalardan yalnız en yüksek öncelikli olan uygulanır.
- Şablondaki "görünür: alan ≠ değer" koşulu, kampanya formunda o alanın yalnız belirli seçimlerde gösterildiğini
  söyler (ör. "Eşik değeri" yalnız koşul "Koşulsuz" değilse).

## Adım adım
**Bir kampanya açmadan önce tipi incelemek**
1. Listede ilgili tipin satırına tıklayın.
2. PARAMETRE ŞABLONU tablosunda zorunlu alanları (ZORUNLU = Evet) ve seçenekleri not edin.
3. Pencereyi kapatıp [Kampanyalar](/rehber/pazarlama/kampanyalar/) → **+ Yeni Kampanya** ile tipi seçin.

## İpuçları ve sık karşılaşılan durumlar
> **Not:** Listede `Pasif` görünen tipler eski sürümden kalan, "İndirim" tipi altında birleştirilmiş tanımlardır;
> yeni kampanyalarda kullanılmaz.

> **İpucu:** ANAHTAR sütunundaki iç adlar yalnız bilgi amaçlıdır; kampanya formunda etiketleri (ALAN sütunu)
> görürsünüz.

## İlgili sayfalar
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
- [Kuponlar](/rehber/pazarlama/kuponlar/)
