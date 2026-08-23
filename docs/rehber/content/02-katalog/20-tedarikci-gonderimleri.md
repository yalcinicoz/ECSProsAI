---
title: Tedarikçi Gönderimleri
route: /catalog/product-submissions
group: Katalog
order: 20
summary: Tedarikçilerin dış bağlantı (Partner API) üzerinden gönderdiği ürün kartı tekliflerini inceleme, onaylama veya gerekçeyle reddetme ekranı.
---

## Ne işe yarar
Tedarikçileriniz, kendilerine tanımlanan dış bağlantı hesabıyla ürün kartı gönderebilir. Bu gönderimler doğrudan
kataloğa girmez; önce burada **bekleyen** olarak listelenir. Katalog sorumlusu gönderimi inceler, **Onayla** derse
sistem canlı ürün kartını oluşturur (ya da aynı tedarikçi + tedarikçi ürün kodlu mevcut ürünü günceller), **Reddet**
derse gerekçe tedarikçiye iletilir. Bu sayfa yalnızca ürün yönetimi yetkisi (`catalog.products.manage`) olan
kullanıcılara açıktır; yetkisi olmayanlar sol menüde görmez.

## Ekran yerleşimi
![Tedarikçi Gönderimleri listesi — durum sekmeleri ve gönderim tablosu](img/catalog-product-submissions.webp)
1. **Başlık** — "Tedarikçi Gönderimleri" ve "N kayıt · Partner API'den gelen ürün kartı gönderimleri".
2. **Durum sekmeleri** — `Bekleyen` (varsayılan) · `Onaylı` · `Reddedilen` · `Tümü`.
3. **Tablo** — gönderim satırları; satıra tıklayınca inceleme sayfası açılır.
4. **Sayfalama** — altta `Önceki` / `Sonraki` ve "X / Y" (birden fazla sayfa varsa).

## Liste ve filtreler

| Sütun | Anlamı |
|---|---|
| ÜRÜN KODU | Tedarikçinin kendi ürün kodu (gönderimi tanımlayan anahtar). |
| AD | Gönderilen ürün adı (Türkçe; yoksa ilk dil). |
| GRUP | Hedef ürün grubu kodu (tedarikçi grubu kodla seçer). |
| VARYANT | Gönderimdeki varyant sayısı. |
| DURUM | `Bekliyor` (sarı) · `Onaylandı` (yeşil) · `Reddedildi` (kırmızı). |
| GÖNDERİM | Gönderim tarihi ve saati. |
| ÜRÜN | Onaylandıysa oluşturulan/güncellenen canlı ürünün kodu; değilse `—`. |

| Filtre | Ne yapar |
|---|---|
| Durum sekmeleri | Listeyi tek duruma indirger; `Tümü` hepsini gösterir. Sekme değişince sayfa 1'e döner. |

- Sayfa başına 20 kayıt. Ayrıca arama/tedarikçi filtresi yoktur.
- Seçili durumda kayıt yoksa "Bu durumda gönderim yok." yazar.
- **Satır tıklama:** inceleme sayfası (`/catalog/product-submissions/<id>`) açılır.

## Butonlar ve aksiyonlar

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Durum sekmeleri | Tablo üstü | Listeyi süzer. | `catalog.products.manage` |
| Satıra tıklama | Tablo | Gönderim inceleme sayfası açılır. | — |
| Önceki / Sonraki | Tablo altı | Sayfa değiştirir. | Birden fazla sayfa. |

## Detay sayfası (inceleme)
![Gönderim inceleme — başlıkta durum rozeti, Reddet/Onayla butonları, Genel/Özellikler/Varyantlar/Görseller kartları](img/catalog-product-submissions-detay.webp)

1. **Geri bağlantısı** — "‹ Gönderimler" listeye döner.
2. **Başlık** — tedarikçi ürün kodu + durum rozeti; sağda (yalnız `Bekliyor` durumunda) **Reddet** ve **Onayla**.
3. **Sonuç şeridi** — onaylı gönderimde "Canlı ürün oluşturuldu: <kod> — eksik alanlar (fiyat/kategori/görsel) ürün
   kartından tamamlanır." (kod tıklanabilir, ürün detayını açar); reddedilmişte "**Red gerekçesi:** …".
4. **GENEL kartı** — Ad (dil koduyla, örn. "Kırmızı Elbise (tr) · Red Dress (en)"), Grup, varsa Kısa açıklama ve Açıklama.
5. **ÖZELLİKLER kartı** — gönderilen ürün özellikleri (özellik kodu → değer; çoklu değerler virgülle). Yoksa kart görünmez.
6. **VARYANTLAR (N) kartı** — tablo: `Eksen` (örn. "renk: Kırmızı / beden: M"), `SKU`, `Barkod`, `Stok`, `Fiyat`.
7. **GÖRSELLER kartı** — gönderilen görsel adresleri küçük resim olarak (yoksa kart görünmez; bozuk adreste yer tutucu).

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Onayla | Başlık sağı | Gönderim `Onaylandı` olur. Aynı tedarikçi + tedarikçi ürün kodu ile canlı ürün **yoksa** yeni ürün kartı oluşturulur (kod `PRD-XXXXXXXX`, satışa **kapalı**, KDV %18, satış fiyatı = ilk varyantın fiyatı, tedarikçi ve tedarikçi ürün kodu dolu, özellikler/eksen değerleri/varyantlar/görsel adresleri aktarılır). **Varsa** mevcut ürün güncellenir: ad/açıklama/fiyat yenilenir, özellikler yeniden yazılır, varyantlar SKU'ya göre eşlenir (mevcut güncellenir, yeni eklenir, gönderimde olmayan **pasife** alınır — silinmez). | Durum `Bekliyor`; `catalog.products.manage`. |
| Reddet | Başlık sağı | "Gönderimi Reddet" penceresi açılır; **Red gerekçesi (zorunlu)** metin kutusu (örn. "Görseller yetersiz, ürün adı kurallara uymuyor…"). **Reddet** butonu gerekçe yazılmadan pasiftir; **Vazgeç** pencereyi kapatır. Onaylanınca durum `Reddedildi`, gerekçe kaydedilir ve tedarikçiye kendi panelinde gösterilir. | Durum `Bekliyor`. |
| Canlı ürün kodu bağlantısı | Sonuç şeridi | Ürün detayını açar. | Onaylı gönderim. |

![Gönderimi Reddet penceresi — zorunlu red gerekçesi](img/catalog-product-submissions-detay--red-modal.webp)

**Hata mesajları (kırmızı şerit):**
- `Gönderim '<durum>' durumunda; yalnız pending onaylanır.` / `… yalnız pending reddedilir.` — gönderim zaten sonuçlanmış.
- `Grup bulunamadı: <kod>` — hedef ürün grubu silinmiş/pasif.
- `'<değer>' değeri artık havuzda yok (<özellik>)` ya da `'<değer>' eksen değeri artık havuzda yok (<eksen>)` — tedarikçinin
  gönderdiği özellik/eksen değeri sonradan değer havuzundan kaldırılmış; değeri Özellik Tipleri'nden geri ekleyip tekrar onaylayın.
- `SKU başka bir üründe kullanımda: <sku>` — revizyonda SKU çakışması.
- `Onay başarısız.` / `Red başarısız.` — genel hata.

## Durumlar ve iş kuralları
| Durum | Anlamı / geçiş |
|---|---|
| `Bekliyor` (`pending`) | Tedarikçi gönderdi, insan onayı bekliyor. Gönderim bu aşamada tedarikçi tarafından içerik kurallarına göre otomatik doğrulanmıştır (geçersiz gönderimler buraya hiç düşmez). → `Onaylandı` ya da `Reddedildi`. |
| `Onaylandı` (`approved`) | Canlı ürün oluşturuldu/güncellendi; ÜRÜN sütununda kodu yazar. Geri alınamaz; düzeltmeler ürün kartından yapılır. |
| `Reddedildi` (`rejected`) | Gerekçeyle reddedildi; tedarikçi düzeltip **yeni** gönderim yapabilir. |

- Onayla, ürünü **satışa açmaz**; fiyat kontrolü, kanal fiyatları, kategori/menü yerleşimi ve görsellerin kendi
  sunucunuza yüklenmesi ürün kartından tamamlanır, sonra Genel sekmesinden satışa açılır.
- Eşleşme anahtarı **tedarikçi + tedarikçi ürün kodu**dur: aynı tedarikçi aynı kodla tekrar gönderirse onay, mevcut
  ürünü günceller (revizyon). Bu yüzden ürün kartındaki "Tedarikçi Ürün Kodu" alanını elle değiştirmeyin.
- Gönderimdeki stok değeri ürün kartına yazılmaz; tedarikçi stokları ayrı bir stok bildirimiyle, tedarikçiye ait depo
  kısmına işlenir.
- Tedarikçinin gönderim yapabilmesi için Cari modülünde tedarikçi hesabı ve ona bağlı bir dış bağlantı (API) hesabı
  tanımlı olmalıdır; tedarikçi kendi satıcı panelinden gönderimlerinin durumunu ve red gerekçesini görür.

## Adım adım

**Bekleyen gönderimi onaylama**
1. **Katalog → Tedarikçi Gönderimleri**'ni açın; `Bekleyen` sekmesi seçilidir.
2. Satıra tıklayın; Genel, Özellikler, Varyantlar ve Görseller kartlarını kontrol edin (ad kurallara uygun mu, eksen
   değerleri doğru mu, görseller yeterli mi).
3. **Onayla**'ya basın. Şeritte "Canlı ürün oluşturuldu: PRD-…" görünür; koda tıklayıp ürün kartına geçin.
4. Ürün kartında KDV/fiyat/kanal fiyatı/görselleri tamamlayıp satışa açın.

**Gönderimi reddetme**
1. İnceleme sayfasında **Reddet**'e basın.
2. Açılan pencerede gerekçeyi açık ve uygulanabilir yazın (tedarikçi bunu okuyacak), **Reddet**'i onaylayın.
3. Durum `Reddedildi` olur; tedarikçi düzelttiğinde yeni gönderim `Bekleyen` sekmesine düşer.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Onaydan önce gönderimdeki varyant fiyatlarını kendi fiyat politikanızla karşılaştırın; onay sonrası
> ürün satış fiyatı ilk varyantın fiyatı olur ve kanal fiyatları girilmemiş halde gelir.

> **Dikkat:** Onay geri alınamaz. Şüphe varsa önce reddedip düzeltilmiş gönderim isteyin; ya da onaylayıp ürün
> kartında düzeltin (ürün zaten satışa kapalı gelir, müşteri görmez).

> **Dikkat:** `'… değeri artık havuzda yok'` hatası, gönderimle onay arasında özellik değerinin silindiğini gösterir.
> Değeri Özellik Tipleri'nden yeniden ekleyin, sonra tekrar Onayla'ya basın; gönderim `Bekliyor` durumunda kalmıştır.

> **Not:** Sol menüde bu sayfayı görmüyorsanız kullanıcınızda ürün yönetimi yetkisi yoktur; yöneticinizden rol/yetki
> isteyin.

## İlgili sayfalar
- [Ürün Kartları](/rehber/katalog/urun-kartlari/)
- [Ürün Detayı](/rehber/katalog/urun-detay/)
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
- [Cari Hesaplar](/rehber/cari/cari-kartlar/)
- [API Hesapları](/rehber/sistem/kullanicilar/)
