---
title: Ürün Kartları
route: /catalog/products
group: Katalog
order: 10
summary: Stok kartlarının (ürün + varyant) listelendiği, arandığı ve yeni kart açmaya geçilen ekran.
---

## Ne işe yarar
Ürün Kartları, mağazanızdaki tüm ürünlerin (stok kartlarının) ana listesidir. Katalog sorumluları
ürün aramak, satışta olup olmadığını görmek, bir ürünün detayına girmek ve yeni ürün kartı açmak için
bu ekranı kullanır. Listedeki her satır bir **ürün**dür; ürünün satılabilir birimleri olan **varyantlar**
(renk/beden kombinasyonları) ürünün detay sayfasında yönetilir.

## Ekran yerleşimi
![Ürün Kartları listesi — başlık, Tümü/Satışta anahtarı, arama kutusu ve ürün tablosu](img/catalog-products.webp)
1. **Başlık alanı** — "Ürünler" başlığı ve toplam ürün sayısı (örn. "28.549 ürün").
2. **Durum anahtarı** — sağ üstte `Tümü` / `Satışta` iki seçenekli filtre.
3. **Yeni Ürün** butonu — yeni ürün kartı açma sayfasına götürür.
4. **Arama kutusu** — tablonun üst şeridinde; yazdıkça listeyi süzer.
5. **Tablo** — ürün satırları; satıra tıklayınca detay sayfası açılır.
6. **Sayfalama** — tablonun altında (yalnızca birden fazla sayfa varsa).

## Liste ve filtreler

| Sütun | Anlamı |
|---|---|
| Ürün | Ürün adı (Türkçe ad; yoksa ilk dildeki ad) ve altında ürün kodu. Solda ürün simgesi. |
| Grup | Ürünün bağlı olduğu ürün grubu (örn. T-Shirt, Elbise). Grup bulunamazsa `—`. |
| Varyant | Ürünün varyant sayısı. |
| Durum | `Satışta` (yeşil) ya da `Satış Kapalı` (gri) rozeti — ürünün genel satış anahtarı. |
| › | Satırın sağındaki ok; satırın tıklanabilir olduğunu gösterir. |

| Filtre | Ne yapar |
|---|---|
| `Tümü` / `Satışta` | `Satışta` seçilince yalnızca satışa açık ürünler listelenir; `Tümü` satışa kapalı ürünleri de gösterir. Seçim değişince sayfa 1'e döner. |
| Arama kutusu ("Ürün adı, kod…") | Ürün kodunda ve Türkçe ürün adında geçen metni arar (büyük/küçük harf duyarsız). Yazmayı bıraktıktan kısa bir süre sonra otomatik süzer; sayfa 1'e döner. |

- **Sıralama:** Liste ürün koduna göre sıralıdır; sütun başlıklarına tıklayarak sıralama değiştirilemez.
- **Sayfalama:** Sayfa başına 20 ürün. Altta "1–20 / 28549" aralığı, `‹` `›` okları, en fazla 5 sayfa numarası ve
  "Sayfa X / Y" bilgisi bulunur.
- **Satır tıklama:** Satıra tıklayınca ürünün detay sayfası açılır (`/catalog/products/<ürün-kodu>`).
- **Boş durum:** Arama sonucu yoksa `"…" için ürün bulunamadı`, hiç ürün yoksa `Henüz ürün eklenmemiş` yazar.

> **Not:** Bu listede ürün grubuna, kanala veya kategoriye göre ayrı bir filtre yoktur; toplu seçim/toplu işlem de
> bulunmaz. Grup bilgisi yalnızca sütun olarak görünür. Satış kanalı bazlı işlemler için
> [Kanal Ürünleri](/rehber/vitrin/kanal-urunleri/) sayfası kullanılır.

## Butonlar ve aksiyonlar

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Ürün | Sağ üst | [Yeni Ürün Kartı](/rehber/katalog/urun-olusturma/) sayfası açılır. | Panele giriş yeterli. |
| `Tümü` / `Satışta` | Sağ üst | Listeyi satış durumuna göre süzer. | — |
| Satıra tıklama | Tablo | [Ürün detayı](/rehber/katalog/urun-detay/) açılır. | — |
| `‹` / `›` ve sayfa numaraları | Tablo altı | Sayfalar arasında geçiş. | Birden fazla sayfa olmalı. |

## Durumlar ve iş kuralları
- `Satışta` / `Satış Kapalı` rozeti ürünün **genel satış anahtarı**dır. Kapalı ürün hiçbir satış kanalında satılmaz;
  açık ürün kanal ayarlarına göre satılır. Anahtar ürün detayındaki **Genel** sekmesinden değiştirilir.
- Yeni oluşturulan ürün **satışa kapalı** doğar; fiyat, varyant ve görseller tamamlandıktan sonra panelden açılır.
- Varyant sayısı 0 olan bir ürün satışa açılsa bile sitede satın alınamaz; satılabilir birim her zaman varyanttır.
- Hızlı erişim: üst çubuktaki komut paletinde (arama) ürün adı/kodu yazarak da doğrudan ürün detayına gidebilirsiniz.

## Adım adım

**Bir ürünü bulup detayına gitme**
1. Sol menüden **Katalog → Ürün Kartları**'nı açın.
2. Arama kutusuna ürün kodunun veya adının bir parçasını yazın (örn. `NK-AM` ya da `air max`).
3. Liste süzülünce ilgili satıra tıklayın; ürün detay sayfası açılır.

**Satışa kapalı ürünleri gözden geçirme**
1. Sağ üstteki anahtarı `Tümü` konumunda bırakın (varsayılan).
2. `Durum` sütununda `Satış Kapalı` rozetli satırları inceleyin; gerekirse detayına girip **Genel** sekmesinden satışa açın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Arama hem koda hem Türkçe ada bakar; İngilizce ada göre arama yapmaz. Kodun tamamını bilmiyorsanız bir
> parçasını yazmanız yeterlidir.

> **Dikkat:** Satırlar tıklanabilir olduğu için metni seçmek yerine tıklamak sizi detaya götürür. Geri dönmek için
> tarayıcının geri tuşunu ya da detay sayfasındaki "Ürünler" bağlantısını kullanın.

> **Not:** "Grup" sütununda `—` görüyorsanız ürünün grubu pasif/silinmiş olabilir; bu ürün için Özellikler ve
> Varyant Ekle işlevleri kısıtlı çalışır. Ürün Grupları sayfasından grubu kontrol edin.

## İlgili sayfalar
- [Yeni Ürün Kartı](/rehber/katalog/urun-olusturma/)
- [Ürün Detayı](/rehber/katalog/urun-detay/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/)
- [Toplu Resim Yükleme](/rehber/katalog/toplu-resim-yukleme/)
