---
title: Ürün Kartı
route: /storefront/product-card
group: Vitrin
order: 30
summary: Sitedeki ürün kartı öğelerinin kanal bazlı açılıp kapatıldığı, kart mesajlarının yönetildiği ve ürün listesi sıralama seçeneklerinin belirlendiği ekran; canlı önizlemelidir.
---

## Ne işe yarar
Sitede listelerde görünen **ürün kartının** hangi öğelerle çıkacağını kanal bazında belirler: rozetler, görsel
hover davranışı, fiyat/puan satırları, eylem butonları ve üç "değişken satır"daki kampanya rozeti / kart mesajı
kurgusu. İkinci sekmede kartlarda dönen kısa **kart mesajları** (örn. "Kargo bedava", "Aynı gün kargo") tanımlanır;
üçüncü sekmede sitedeki **sıralama menüsünde** hangi seçeneklerin sunulacağı seçilir. Pazarlama/vitrin sorumlusu
kampanya dönemlerinde ve tasarım değişikliklerinde kullanır. Sağdaki önizleme sitenin gerçek kart şablonuyla
çalışır; kaydetmeden yapılan yerleşim değişiklikleri anında yansır.

## Ekran yerleşimi
![Ürün Kartı — Yerleşim sekmesi ve canlı önizleme](img/storefront-product-card.webp)
1. **Başlık şeridi** — açıklama, `Kaydedilmemiş değişiklik` rozeti, **Kaydet**.
2. **Satış Kanalı seçici** — "Firma — Kanal" biçiminde; seçim oturumda hatırlanır.
3. **Sekmeler** — Yerleşim · Kart Mesajları · Sıralama.
4. **Sekme içeriği** (sol, geniş).
5. **Canlı Önizleme** (sağ, sabit genişlik) — sitenin gerçek kart şablonu, demo veriyle; yerleşim ayarları ve
   aktif kart mesajları anında görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Sağ üst | Yerleşim ve Sıralama sekmelerindeki tüm değişiklikleri kanala yazar; sitede en geç 5 dakika içinde görünür. | Kanal seçili ve değişiklik olmalı. Kart Mesajları kendi penceresinden kaydedilir. |
| Yeni Mesaj | Kart Mesajları sekmesi | "Yeni Kart Mesajı" penceresi. | Kanal seçili. |
| Mesaj satırı tıklama | Kart Mesajları tablosu | "Kart Mesajını Düzenle" penceresi. | — |
| Sil (pencere, sol alt) | Mesaj düzenleme | ⚠️ Mesajı siler (onay sorulmaz), pencere kapanır. | Yalnız mevcut mesajda. |
| Vazgeç / Kaydet (pencere) | Mesaj penceresi | Kapatır / mesajı oluşturur-günceller. | Mesaj metni (kaynak dil) gerekir. |

## Sekmeler

### Yerleşim
Her satır bir onay kutusudur; işaret kaldırılınca öğe sitedeki kartlardan kalkar. Öncelik ve alt seçenekler yalnız
ilgili kutu açıkken görünür.

**Rozetler (görsel üstü)**
| Anahtar | Varsayılan | Sitede etkisi |
|---|---|---|
| Videolu Ürün rozeti | Açık | Videosu olan ürünlerde oynatma rozeti; üzerine gelince video oynar. |
| Sponsorlu rozeti | Açık | Öne çıkarma penceresi içindeki ürünlerde "Sponsorlu" etiketi (öne çıkarma ürün kartı detayından tarih aralığıyla verilir). |
| Diğer renkler rozeti | Açık | 2+ renkli ürünlerde renk sayacı ve farklı renk seçeneklerinin balonu. |
| Galeri noktaları | Açık | Görsel üzerinde gezinirken galeri nokta göstergeleri. |

**Görsel hover efekti**
| Seçenek | Sitede etkisi |
|---|---|
| Resim geçişi — yatay harekette diğer görseller (varsayılan) | Fare görsel üzerinde yatay hareket ettikçe ürünün diğer görselleri gösterilir. |
| Yakınlaştırma — görsel hafifçe büyür (resim geçişi kapalı) | Görsel büyür; galeri gezinmesi kapanır. İki efekt birlikte kullanılmaz; mobildeki kaydırmalı galeri etkilenmez. |

**Değişken satırlar** — kartta üç esnek alan vardır; her biri ayrı açılıp kapatılır:
| Alan | Konum | Alt seçenekler |
|---|---|---|
| Alan 1 — Görsel altı bant | Ürün görselinin hemen altındaki bant | Kampanya rozetleri (varsayılan açık), Kart mesajları, Öncelik. |
| Alan 2 — Ürün adı altı satır | Ürün adının altı | Kampanya rozetleri, Kart mesajları, Öncelik. |
| Alan 3 — Puan altı satır | Puan/yorum satırının altı | Kampanya rozetleri, Kart mesajları, **Kaç kişinin sepetinde**, **Kaç kişinin favorisi**, **Kaç kişi baktı** (canlı sayaçlar; 0 olan üründe gizli), Öncelik. |

`Öncelik` seçimi (yalnız hem kampanya hem mesaj açıkken): `Mesajlar önce` / `Kampanyalar önce` — alanda ikisi de
varsa hangisinin önce gösterileceği.

**Puan & Fiyat**
| Anahtar | Sitede etkisi |
|---|---|
| Puan + yorum sayısı | Onaylı yorumların ortalaması ve yıldızlar. |
| İndirim satırı | `-%X` rozeti + üstü çizili eski fiyat (eski fiyat satış fiyatından yüksekse). |
| Kampanyalı fiyat satırı | Ürün bazlı kampanya fiyatı (satış fiyatının altındaysa). |

**Eylem butonları**
| Anahtar | Sitede etkisi |
|---|---|
| Favori butonu | Kalp ikonu. |
| Koleksiyon butonu | Koleksiyona ekle ikonu. |
| Sepete Ekle | Sepet ikonu: masaüstünde üzerine gelince beden paneli, mobilde alttan açılan beden listesi. |
| Benzer ürünler | Koleksiyon ikonunun altındaki ikon; ürünün ilk görseliyle görsel arama (aynı cinsiyet + ürün grubu) sonuç sayfasını açar. |

**Sabit çekirdek** — Görsel, ürün adı, fiyat ve kart linki kapatılamaz (kilitli satır).

### Kart Mesajları
Kanalın kart mesajları listesi; satıra tıklayınca düzenlenir.

| Sütun | Anlamı |
|---|---|
| Mesaj | Kaynak dildeki metin. |
| Alan | `Alan 1/2/3` — mesajın hangi değişken satırda döneceği. |
| İkon | Seçilen ikon kodu (ör. `fa-truck`) ya da `—`. |
| Renk | `Yeşil`, `Turuncu`, `Bordo`, `Pembe` ya da `Varsayılan`. |
| Kapsam | `Tüm ürünler` / `N kategori` / `N ürün`. |
| Tarih | `Süresiz` ya da `başlangıç – bitiş`. |
| Sıra | Aynı alandaki mesajlar arasında sıra. |
| Aktif | `Aktif` / `Pasif` rozeti. |

**Yeni Kart Mesajı / Kart Mesajını Düzenle penceresi**
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Mesaj | Evet (kaynak dil) | Çok dilli kısa metin (`TR`/`EN` sekmeleri). Örn. `Kargo bedava`. |
| Alan | Evet | `Alan 1 — görsel altı bant`, `Alan 2 — ürün adı altı`, `Alan 3 — puan altı`. İlgili alanın Yerleşim'de "Kart mesajları" seçeneği açık olmalı. |
| Renk | Hayır | Varsayılan / Yeşil / Turuncu / Bordo / Pembe. |
| İkon (Font Awesome sınıfı) | Hayır | `fa-truck` gibi ikon kodu; altındaki hızlı seçim çipleri (`fa-truck`, `fa-truck-fast`, `fa-percent`, `fa-tags`, `fa-ticket`, `fa-clock`, `fa-fire`, `fa-gift`). |
| Kapsam | Evet | `Tüm ürünler`; `Kanal kategorileri` (aranabilir listeden işaretleyin, altta "N kategori seçili"); `Ürün kodları` (satır/virgül/noktalı virgülle ayrılmış kodlar, altta sayaç). |
| Başlangıç tarihi | Hayır | Boş = hemen başlar. |
| Bitiş tarihi | Hayır | Boş = süresiz. Bitişi geçen mesaj kartlarda gösterilmez. |
| Sıra | Hayır | Sayı; küçük olan önce. |
| Aktif | — | Pasif mesaj hiçbir kartta gösterilmez. |

### Sıralama
Sitedeki ürün listesi sıralama menüsünde gösterilecek seçenekler. Kapatılan seçenek menüde listelenmez.

| Seçenek | Not |
|---|---|
| Önerilen Sıralama | Varsayılan — kapatılamaz (kilitli). |
| En Düşük Fiyat · En Yüksek Fiyat · En Yeniler | Anlık veriyle sıralar. |
| En Yüksek Puanlı Ürünler · En Fazla Yorum Alan Ürünler · Favoriye En Çok Eklenen Ürünler · Sepete En Çok Atılan Ürünler · En Çok Bakılan Ürünler · En Çok Satılan Ürünler | Sayaç tabanlı; yaklaşık 10 dakikada bir tazelenen verilerle sıralar. |

Bu sekmedeki değişiklikler de sağ üstteki **Kaydet** ile yazılır.

## Durumlar ve iş kuralları
- Ayarlar **kanal bazlıdır**; her kanalın kendi yerleşimi, mesajları ve sıralama seçenekleri vardır.
- Yerleşim/Sıralama değişikliği Kaydet'e kadar yalnız ekranda ve önizlemede görünür; kanal değiştirmek
  kaydedilmemiş değişiklikleri korumaz.
- Kart mesajları anında kaydedilir (pencere Kaydet); sitede gösterim için hem mesaj **Aktif** hem tarih penceresi
  geçerli hem de ilgili alanın "Kart mesajları" seçeneği Yerleşim'de açık olmalıdır.
- Aynı alanda hem kampanya rozetleri hem mesajlar varsa sırayla dönerek gösterilir; "Öncelik" hangisinin önce
  geleceğini belirler.
- Sosyal kanıt sayaçları (sepet/favori/görüntülenme) değeri 0 olan üründe görünmez.
- Önizleme demo veriyle üretilir; gerçek ürünlerdeki kampanya/puan değerlerini göstermez.

## Adım adım
**Kampanya dönemi için kart mesajı ekleme**
1. Satış Kanalı'nı seçin, **Kart Mesajları** sekmesine geçin, **Yeni Mesaj**.
2. Mesajı yazın, Alan'ı (örn. Alan 1) ve rengi seçin, ikon çipinden `fa-percent` seçin.
3. Kapsam'ı `Kanal kategorileri` yapıp ilgili kategorileri işaretleyin; başlangıç/bitiş tarihlerini girin; **Kaydet**.
4. **Yerleşim** sekmesinde Alan 1'in "Kart mesajları" kutusunun açık olduğundan emin olun; gerekiyorsa **Kaydet**.
5. Önizlemede mesajın döndüğünü kontrol edin.

**Bir kart öğesini tüm sitede kapatma**
1. Yerleşim sekmesinde ilgili kutunun (örn. "Benzer ürünler") işaretini kaldırın.
2. Önizlemede kontrol edip sağ üstten **Kaydet**'e basın; 5 dakika içinde sitede kalkar.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Önizleme kaydetmeden güncellenir; birkaç varyasyonu deneyip sonra kaydedebilirsiniz.

> **Dikkat:** Mesaj penceresindeki **Sil** onay sormaz ve geri alınamaz.

> **Dikkat:** Mesaj "Aktif" olduğu halde sitede görünmüyorsa bitiş tarihini ve Yerleşim'de o alanın
> "Kart mesajları" kutusunu kontrol edin.

> **Not:** "Kaydedilemedi: …" / "İşlem başarısız: …" kutuları sunucu hatasını gösterir; ikon alanına yalnız
> geçerli ikon kodu (`fa-…`) yazılabilir.

## İlgili sayfalar
- [Kanal Kategorileri](/rehber/vitrin/kanal-kategorileri/)
- [Menü Yerleşimi](/rehber/vitrin/menu-yerlesimi/)
- [Yorum Moderasyonu](/rehber/vitrin/yorum-moderasyonu/)
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
