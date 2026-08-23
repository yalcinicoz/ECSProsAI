---
title: Yeni Ürün Kartı
route: /catalog/products/new
group: Katalog
order: 11
summary: Ürün grubu, kod ve ad girerek hızlıca yeni ürün kartı açma; ayrıntılar kart açıldıktan sonra detay sayfasında tamamlanır.
---

## Ne işe yarar
Bu ekran yeni bir ürün kartını **en az bilgiyle** açmak içindir: ürün grubu, isteğe bağlı ürün kodu ve Türkçe ürün adı.
Kart oluşturulur oluşturulmaz ürünün detay sayfasına yönlendirilirsiniz; fiyat, KDV, açıklamalar, varyantlar,
görseller ve kanal fiyatları orada girilir. Katalog sorumlusu, bir ürünü sisteme ilk kez tanımlarken bu sayfayı kullanır.

> **Not:** Bu sayfa adım adım bir sihirbaz değildir; tek bir formdur. Varyant eksenleri, fiyat ve KDV burada sorulmaz —
> bunlar [Ürün Detayı](/rehber/katalog/urun-detay/) sayfasının sekmelerinde doldurulur.

## Ekran yerleşimi
![Yeni Ürün Kartı formu — ürün grubu, ürün kodu, ürün adı ve Oluştur butonu](img/catalog-products-new.webp)
1. **Üst başlık** — "Ürünler › Yeni Ürün Kartı" yol bilgisi, başlık ve "Ürün grubu, kodu ve adı ile hızlıca kart açın" açıklaması.
2. **İptal** bağlantısı — sağ üstte; kaydetmeden Ürün Kartları listesine döner.
3. **Form kartı** — üç alan ve altında **Ürün Kartını Oluştur** butonu.

## Form alanları

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ürün Grubu | Evet (`*`) | Aranabilir açılır liste; pasif gruplar dahil tüm ürün grupları listelenir. Liste yüklenirken "Yükleniyor…" yazar. **Oluşturulduktan sonra değiştirilemez** (alanın altında uyarı yazar). Grup, ürünün hangi özellikleri ve hangi varyant eksenlerini (renk, beden…) taşıyacağını belirler. |
| Ürün Kodu | Hayır | Boş bırakılırsa sistem `PRD-XXXXXXXX` biçiminde (8 karakterlik büyük harf/rakam) benzersiz bir kod üretir. Girilirse baştaki/sondaki boşluklar atılır ve kodun benzersiz olması gerekir. Örnek: `NK-AM270`. |
| Ürün Adı (TR) | Evet (`*`) | Türkçe ürün adı; imleç açılışta bu alandadır. Dolu olduğunda alan yeşil işaretlenir. Diğer dillerdeki adlar detay sayfasındaki **Genel → Çok Dilli İçerik** bölümünden girilir. Örnek: `Nike Air Max 270`. |

**Doğrulama ve hata mesajları**
- **Ürün Kartını Oluştur** butonu, ürün grubu seçilip Türkçe ad girilene kadar pasiftir.
- Aynı kod zaten kullanımdaysa formun altında kırmızı metinle `'<kod>' ürün kodu zaten mevcut.` görünür.
- Seçilen grup bulunamazsa `Ürün grubu bulunamadı.` uyarısı görünür.
- Başka bir hata olursa `Hata oluştu. Tekrar deneyin.` yazar; kayıt oluşmaz.

## Butonlar ve aksiyonlar

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Ürün Kartını Oluştur | Form kartının sağ altı | Ürün kartı kaydedilir ve otomatik olarak ürünün detay sayfasına gidilir. | Ürün grubu seçili + Türkçe ad dolu. |
| İptal | Sağ üst | Hiçbir şey kaydetmeden Ürün Kartları listesine döner. | — |
| Ürünler (yol bağlantısı) | Sol üst | Listeye döner. | — |

## Durumlar ve iş kuralları
- Yeni kart **satışa kapalı** (`Satış Kapalı`) doğar. Satışa açma işlemi detay sayfasındaki **Genel** sekmesinden yapılır.
- Satış fiyatı `0` ₺ ve KDV `%18` ile başlar; her ikisi de detay sayfasının **Genel** sekmesinde güncellenir.
- **Varyant ekseni olmayan grup** seçildiyse (örn. tekil ürünler), sistem ürünle birlikte özniteliksiz tek bir
  **varsayılan (default) varyant** açar: SKU'su ürün koduyla aynıdır, fiyatı ürün fiyatıdır. Satılabilir birim
  varyant olduğu için bu ürün ek işlem gerekmeden satışa hazır hale gelebilir.
- **Varyant ekseni olan grup** (renk, beden gibi) seçildiyse ürün **0 varyantla, taslak** olarak açılır; varyantlar
  detay sayfasındaki **Varyantlar** sekmesinden kombinasyon seçilerek oluşturulur.
- Ürün grubu sonradan değiştirilemez. Yanlış grupla açtıysanız ürünü silip yeniden oluşturmanız gerekir.

## Adım adım

**Yeni ürün kartı açma**
1. **Katalog → Ürün Kartları**'nda sağ üstteki **Yeni Ürün** butonuna tıklayın.
2. **Ürün Grubu** listesinden doğru grubu seçin (yazarak arayabilirsiniz).
3. İsterseniz **Ürün Kodu** girin; boş bırakırsanız otomatik kod atanır.
4. **Ürün Adı (TR)** alanına ürünün Türkçe adını yazın.
5. **Ürün Kartını Oluştur**'a tıklayın. Detay sayfası açılır.
6. Detay sayfasında sırasıyla **Genel** (fiyat/KDV/açıklama) → **Özellikler** → **Varyantlar** → **Görseller** →
   **Satış Kanalları** sekmelerini doldurun, en son **Genel** sekmesinden satışa açın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Ürün kartı açmadan önce ürün grubunun hazır olduğundan emin olun: grupta hangi özelliklerin bulunduğu ve
> hangilerinin varyant ekseni olduğu **Ürün Grupları** sayfasında tanımlanır. Grup eksikse önce grubu tamamlayın;
> aksi halde Özellikler ve Varyantlar sekmeleri boş gelir.

> **Dikkat:** `'… ürün kodu zaten mevcut.'` hatası aldıysanız kod başka bir üründe kullanılıyordur. Farklı bir kod
> girin ya da alanı boş bırakıp otomatik kod üretilmesini sağlayın.

> **Not:** Kendi kod sisteminiz varsa (ERP kodu gibi) kodu burada girmeniz önerilir; otomatik `PRD-…` kodları sonradan
> değiştirilemez.

## İlgili sayfalar
- [Ürün Kartları](/rehber/katalog/urun-kartlari/)
- [Ürün Detayı](/rehber/katalog/urun-detay/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/)
