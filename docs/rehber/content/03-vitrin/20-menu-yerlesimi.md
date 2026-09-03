---
title: Menü Yerleşimi
route: /storefront/menu-placement
group: Vitrin
order: 20
summary: Sitenin üst menüsünü (menü ağacı) sürükle-bırak ile kuran, kategori/link/başlık öğelerini düzenleyen ve mega menü davranışını ayarlayan ekran.
---

## Ne işe yarar
Sitenin üst menüsünde hangi kategorilerin, hangi sırada ve hangi başlıklar altında görüneceğini belirler. Menü
öğeleri **kanal kategorilerinden** beslenir; aynı kategori menüde birden çok yerde (örn. hem "Kadın" hem "İndirim"
altında) gösterilebilir. Katalog/pazarlama sorumlusu yeni kategori açtığında, sezon değişiminde ya da menü
düzenini yeniden kurgularken kullanır. Menü yoksa site, kategori ağacını olduğu gibi gösterir.

## Ekran yerleşimi
![Menü Yerleşimi — solda menü ağacı, sağda kategori havuzu](img/storefront-menu-placement.webp)
1. **Başlık şeridi** — açıklama, `Kaydedilmemiş değişiklik` rozeti ve **Kaydet** butonu.
2. **Satış Kanalı seçici** — kanal değişince ağaç yeniden yüklenir.
3. **Mega Menü Davranışı** kartı — "üzerine gelindiğinde açılsın" onay kutusu (anında kaydedilir).
4. **Menü Ağacı** (sol, geniş) — sürükle-bırak düzenlenen hiyerarşik liste; üstte öğe sayısı ve **Kök Öğe Ekle**.
5. **Kategori Havuzu** (sağ, dar) — kanalın tüm kategorileri; arama kutusu; sürükleyerek ya da `+` ile ağaca eklenir.

## Liste ve filtreler
**Menü Ağacı satırı** (soldan sağa): tutma kulpu · daralt/genişlet oku (alt öğesi varsa) · menü görseli (varsa)
· tip simgesi (zincir = Link, etiket = Başlık) · etiket · rozet · URL kodu · üzerine gelince aksiyon butonları.

| Görünüm | Anlamı |
|---|---|
| Soluk satır | Pasif öğe — sitede görünmez, yerleşimde saklanır. |
| Kırmızı `Silinmiş kategori` | Öğenin bağlı olduğu kanal kategorisi silinmiş; öğeyi düzenleyip yeni kategori seçin ya da silin. |
| Mavi üst/alt çizgi | Sürüklerken hedefin önüne/arkasına bırakılacağını gösterir. |
| Mavi dolgu | Sürüklerken hedefin **altına** (alt öğe) bırakılacağını gösterir. |

**Kategori Havuzu**
| Öğe | Anlamı |
|---|---|
| Kategori ara… | Ad ya da URL'ye göre havuzu süzer. |
| `menüde ×N` rozeti | Kategori menüde N yerde kullanılıyor. |
| `+` (üzerine gelince) | Kategoriyi menünün sonuna kök öğe olarak ekler. |

Kanal seçimi tarayıcı oturumunda hatırlanır; Kanal Kategorileri'nde seçtiğiniz kanal burada da açılır.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Sağ üst | Ağacın tamamı kaydedilir; sitede en geç 5 dakika içinde görünür. | Üst menü var ve kaydedilmemiş değişiklik olmalı. |
| Üst Menü Oluştur | Kanalın menüsü yoksa ortada | Kanal için boş üst menü ("Ana Menü") oluşturur; ağaç düzenlenebilir hale gelir. | Kanal seçili. |
| Mega menü onay kutusu | Mega Menü Davranışı kartı | İşaretlenince menü üzerine gelindiğinde mega menü açılır; kapalıyken (varsayılan) yalnız "Kategoriler" düğmesine tıklanınca açılır. Anında kaydedilir, sitede ≤5 dk. | — |
| Kök Öğe Ekle | Menü Ağacı başlığı | "Menü Öğesi Ekle" penceresi; öğe en üst seviyeye eklenir. | — |
| Sürükle-bırak (ağaç içi) | Satır | Satırın üst çeyreğine bırakma = önüne, alt çeyreğine = arkasına, ortasına = altına (alt öğe). Öğe kendi altına taşınamaz. Boş alana bırakma = kök sonuna ekler. | — |
| Sürükle-bırak (havuzdan) | Havuz satırı → ağaç | Kategori, bırakılan konuma yeni öğe olarak eklenir. | — |
| ↑ / ↓ | Satır (üzerine gelince) | Kardeşler arasında bir yukarı/aşağı taşır. | İlk/son öğede ilgili ok pasif. |
| + (Alt öğe ekle) | Satır | Bu öğenin altına yeni öğe ekleme penceresi. | — |
| Kalem (Düzenle) | Satır | "Menü Öğesini Düzenle" penceresi; **Uygula** ile değişiklik ağaca işlenir (kaydetmek için ayrıca Kaydet). | — |
| Çöp kutusu (Sil) | Satır | Öğeyi ağaçtan çıkarır. ⚠️ Alt öğesi varsa "… ve N alt öğesi menüden çıkarılacak. Emin misiniz?" onayı ister. Kaydedilene kadar geri alınabilir (sayfayı yenilemek değişiklikleri atar). | — |
| Daralt/genişlet oku | Satır | Alt öğeleri gizler/gösterir (yalnız görünüm). | — |

## Form alanları (Menü Öğesi Ekle / Düzenle penceresi)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Öğe Tipi | Evet | `Kategori` (ürün listesine gider), `Link` (serbest adres), `Başlık` (tıklanmayan grup başlığı). |
| Kategori | Kategori tipinde evet | Aranabilir liste "Ad (/url)"; seçilince öğenin adresi kategorinin URL'si olur. |
| Adres (URL) | Link tipinde | `/kampanya` gibi site içi yol ya da `https://…` tam adres. |
| Menü Etiketi | Link/Başlık'ta evet; Kategori'de hayır | Menüde görünen yazı (örn. `KADIN`). Kategori tipinde boş bırakılırsa kategori adı kullanılır. |
| Menü Görseli | Hayır | Mega menüde öğenin yanında gösterilen küçük görsel. **Görsel Yükle** ile dosya seçilir; CDN adresi sistem tarafından oluşturulur. JPEG, PNG, WebP, GIF ve SVG kabul edilir, en fazla 5 MB olabilir. |
| Rozet | Hayır | Öğenin yanında küçük etiket (`YENİ`). |
| Menüde göster | — | İşareti kaldırılırsa öğe pasif olur: sitede görünmez, yerleşimde saklanır. |

Pencere altındaki buton yeni öğede **Ekle**, düzenlemede **Uygula**; Kategori tipinde kategori seçilmeden, diğer
tiplerde etiket girilmeden buton pasiftir.

Menü görselleri vitrin banner'larından ve ürün görsellerinden ayrı olarak CDN'deki
`/storefront-v1/menus/YYYY/MM/` ağacında tutulur. Kullanıcı URL girmez; **Değiştir** yeni dosyayı yükler.
Kırmızı yazılı **Kaldır** butonu önce onay sorar; onaydan sonra yalnız formdaki görsel bağlantısını temizler.
Değişikliğin ağaca geçmesi için modalda **Uygula**, kalıcı olması için sayfada ayrıca **Kaydet** kullanılmalıdır.

## Durumlar ve iş kuralları
- Menü tek bir kanala aittir; her kanalın kendi üst menüsü vardır.
- Kaydet tüm ağacı yazar; ekrandaki değişiklikler kaydedilene kadar yalnız tarayıcıda durur (`Kaydedilmemiş
  değişiklik` rozeti). Kanal değiştirmek kaydedilmemiş değişiklikleri atar.
- Aynı kategori birden çok öğeye bağlanabilir (havuzda `menüde ×N`).
- Ürünü olmayan (boş) kategori öğeleri sitede otomatik budanır; ürün/stok gelince kendiliğinden görünür, bu
  durum en geç 15 dakikada oturur.
- Menüdeki bir öğeye tıklamak her durumda o kategorinin ürün listesini açar; mega menü ayarı yalnız açılma
  davranışını etkiler.
- "Silinmiş kategori" satırları kaydedilebilir ama sitede çalışmaz; temizleyin.

## Adım adım
**Yeni kategoriyi menüye ekleme**
1. Satış Kanalı'nı seçin; Kategori Havuzu'nda kategoriyi arayın.
2. Kategoriyi ağaçta istediğiniz üst öğenin **ortasına** sürükleyip bırakın (alt öğe olur) ya da `+` ile sona ekleyin.
3. Gerekirse kalemle etiket/rozet verin; **Kaydet**.

**Başlık altında gruplama**
1. **Kök Öğe Ekle** → Öğe Tipi `Başlık`, Menü Etiketi girin, **Ekle**.
2. İlgili kategorileri bu başlığın altına sürükleyin; **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Havuzdaki arama hem ada hem URL'ye bakar; `menüde ×N` rozeti olmayan kategoriler henüz menüde değildir.

> **Dikkat:** Kaydetmeden sayfayı kapatır ya da kanal değiştirirseniz düzenlemeler kaybolur.

> **Not:** "Bu kanalda henüz üst menü tanımı yok" mesajında önce **Üst Menü Oluştur**'a basın; o ana kadar site
> kategori ağacını kendiliğinden gösterir.

> **Not:** "Kaydedilemedi: …" kırmızı kutusu görünürse mesajdaki nedeni düzeltip tekrar Kaydet'e basın; ağaç
> ekranda korunur.

## İlgili sayfalar
- [Kanal Kategorileri](/rehber/vitrin/kanal-kategorileri/)
- [Ürün Kartı](/rehber/vitrin/urun-karti/)
- [Vitrin Yönetimi](/rehber/vitrin/vitrin-yonetimi/)
