---
title: Vitrin Yönetimi
route: /storefront/pages
group: Vitrin
order: 60
summary: Ana sayfa ve diğer yerleşimlerdeki vitrin bloklarının (slider, banner, ürün carousel'i, story, duyuru…) taslak olarak düzenlendiği, segment bazlı önizlendiği, versiyonlu yayınlandığı ve gerektiğinde eski versiyona dönüldüğü ekran grubu.
---

## Ne işe yarar
Sitenin ana sayfası ve diğer sabit alanları (duyuru şeridi, ürün listesi üst/alt, ürün detay altı, sepet,
teslimat, ödeme) **bloklardan** oluşur. Bu ekranda blokları taslak olarak ekler, öğelerini (görsel/link/metin)
ve ürün kaynaklarını düzenler, kimlere gösterileceğini belirler, **Yayınla** ile versiyonlu olarak canlıya
alırsınız. Taslak değişiklikler canlıyı etkilemez; site yalnız yayındaki aktif versiyonu okur. Pazarlama/vitrin
sorumlusu kampanya dönemlerinde, sezon geçişlerinde ve ana sayfa düzeni değiştiğinde kullanır. Üç sayfadan
oluşur: **Vitrin Yönetimi** (blok listesi + yayın), **Blok detayı** (alanlar, ayarlar, öğeler) ve
**Geçmiş & Versiyonlar** (yayın versiyonları, geri dönüş, değişiklik geçmişi).

## Ekran yerleşimi
![Vitrin Yönetimi — yerleşim sekmeleri, araç çubuğu ve canlı önizlemeli blok listesi](img/storefront-pages.webp)
1. **Başlık şeridi** — açıklama ve sağda **Platform seç** (kanal) listesi; seçim oturumda hatırlanır.
2. **Yerleşim sekmeleri** — Ana Sayfa · Global Üst Alan (Duyuru) · Ürün Listesi Üst · Ürün Listesi Alt ·
   Ürün Detay Alt · Sepet · Teslimat · Ödeme.
3. **Araç çubuğu** — solda **Yeni Blok**, **Segment Önizleme**, **Geçmiş & Versiyonlar →**; sağda yayın notu
   kutusu ve **Yayınla**.
4. **Segment Önizlemesi** paneli (Segment Önizleme açıkken).
5. **Cihaz çerçevesi** — 🖥 Masaüstü / 📱 Mobil önizleme genişliği.
6. **Blok listesi** — her blok kendi yönetim çubuğu ve gerçek içerik önizlemesiyle sayfa sırasına göre alt alta.

## Liste ve filtreler
Blok listesinde her blok bir karttır. **Yönetim çubuğu** (soldan sağa):
| Öğe | Anlamı |
|---|---|
| ↑ / ↓ | Bloğu yerleşim içinde bir üst/alt sıraya taşır (anında kaydedilir). |
| Başlık | Blok başlığı. |
| Tip rozeti | Blok tipi (Banner, Slider, Carousel Ürün Listesi…); yanında varsa şablon adı. |
| `tarihli` | Bloğun başlangıç/bitiş tarihi tanımlı. |
| `üye bağlamlı` (sarı) | Ürün kaynağı ziyaretçiye özel (Son Gezilenler / Favoriler); önizlenemez. |
| `Aktif` / `Pasif` rozeti | Pasif blok yayına girmez; kart soluk gösterilir. |
| Pasifleştir / Aktifleştir · Düzenle · Sil | Blok aksiyonları (aşağıda). |

Çubuğun altında bloğun **gerçek içerik önizlemesi** görünür: story kapakları, kategori kapsülleri, marka logoları,
görselli yorum kartları, ikon/bilgi bandı kutuları, Instagram ızgarası, banner/slider görselleri, duyuru şeridi,
ürün kaynağından gelen ilk ürün kartları (ad + fiyat), koleksiyonlar (ad · ürün · görüntülenme), tabs sekme
adları. Kaynak boşsa "Kaynak ürün döndürmedi", öğe yoksa "Öğe yok" yazar. Yerleşimde hiç blok yoksa
"Bu yerleşimde taslak blok yok — 'Yeni Blok' ile başlayın." görünür.

| Filtre | Ne yapar |
|---|---|
| Platform seç | Zorunlu; seçilmeden "Blokları yönetmek için platform seçin." uyarısı. |
| Yerleşim sekmeleri | Yalnız o yerleşimin bloklarını gösterir. Her yerleşim kendi sırasını taşır. |
| Masaüstü / Mobil | Önizleme çerçevesini daraltır; mobil görseller varsa onları kullanır. Canlıyı etkilemez. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Blok | Araç çubuğu | "Yeni Blok" penceresi: Blok tipi (liste), Şablon (tipte varsa), Başlık (TR). **Oluştur** → blok seçili yerleşimin sonuna aktif olarak eklenir ve detay sayfası açılır. | Platform seçili; tip + başlık zorunlu. |
| Segment Önizleme / Segment Önizlemeyi Kapat | Araç çubuğu | Segment Önizlemesi panelini açar/kapatır. | — |
| Önizle | Segment paneli | Seçilen ziyaretçi profiline göre taslak blokların **Görünür/Gizli** listesi ve nedeni (öğe N/M, ürün sayısı) hesaplanır. Taslak veriyle çalışır, canlıyı etkilemez. | — |
| Geçmiş & Versiyonlar → | Araç çubuğu | Geçmiş sayfasına gider (aşağıda). | — |
| Yayınla | Araç çubuğu, sağ | Platformun **tüm yerleşimlerindeki aktif taslak bloklar** doğrulanır ve yeni versiyon olarak canlıya alınır; yayın notu kaydedilir. Hata varsa kırmızı kutuda "Yayınlanamadı: …" ve yayın yapılmaz. | Platform seçili. |
| ↑ / ↓ | Blok çubuğu | Sırayı değiştirir; anında kaydedilir (taslak). | İlk/son blokta pasif. |
| Pasifleştir / Aktifleştir | Blok çubuğu | Bloğun Aktif bayrağını çevirir (taslak). | — |
| Düzenle | Blok çubuğu | Blok detay sayfası. | — |
| Sil | Blok çubuğu | ⚠️ "Blok silinsin mi? (Canlı yayın bir sonraki Yayınla'ya kadar etkilenmez)" onayı; onayla taslak blok silinir. | — |

### Segment Önizlemesi paneli
| Alan | Seçenekler |
|---|---|
| Şehir | 81 il (plaka kodu); boş = Konumsuz. |
| Cinsiyet | Kadın / Erkek; boş = Bilinmiyor. |
| Cihaz | Masaüstü / Mobil / Tablet / iOS Uygulama / Android Uygulama. |
| Üyelik | Misafir / Üye; Üye seçilince **Üye Grubu** alanı açılır. |

Sonuçta "Çözülen segment: … — yerleşim: …" satırı ve her blok için `Görünür`/`Gizli` rozeti + neden.

## Blok detay sayfası
![Blok detayı — Blok Alanları, Blok Ayarları ve Öğeler](img/storefront-pages-detay.webp)
1. **Başlık** — ← Vitrin Yönetimi bağlantısı, blok başlığı + tip/şablon; sağda `Aktif`/`Pasif` rozeti ve **Kaydet**.
2. **Blok Alanları** kartı (sol).
3. **Blok Ayarları** kartı (sağ) — Ürün Kaynağı / Koleksiyon Kaynağı / Görünüm / Hedefleme.
4. **Öğeler** tablosu (alt; yalnız öğe destekleyen tiplerde) ve **Öğe Ekle / Öğe Düzenle** penceresi.

**Kaydet** blok alanlarını, ayarlarını ve (öğeli tipte) öğe listesinin tamamını birlikte yazar; hata kırmızı kutuda.

### Blok Alanları
| Alan | Zorunlu | Açıklama / kurallar |
|---|---|---|
| Başlık (TR) | Evet | Blok başlığı; sitede tipine göre gösterilir. |
| Alt başlık (TR) | Hayır | İkincil metin. |
| Yerleşim | Evet | Bloğun bağlı olduğu alan; değiştirilirse blok o yerleşime taşınır. |
| Şablon | Tipte varsa | Banner: `tekli`, `ikili`, `uclu`, `dortlu`, `besli`, `reklam`. Carousel: `standart`, `ozel-fiyat`, `flash`. Diğer tiplerde "Bu tipte şablon seçilmez." |
| Sıra | Hayır | Yerleşim içindeki sıra (listedeki oklarla da değişir). |
| Öncelik | Hayır | Aynı sıradaki bloklar arasında ikincil sıralama. |
| Başlangıç / Bitiş | Hayır | Tarih-saat penceresi; dışında blok gösterilmez (`tarihli` rozeti). |
| Aktif | — | "pasif blok hiçbir koşulda yayınlanmaz". |

### Blok Ayarları
**Ürün Kaynağı** — Carousel/Infinity/Kategori Çok Satanları tiplerinde zorunlu ("bu tipte zorunlu"); diğer tiplerde
**+ Ürün şeridi ekle (opsiyonel)** ile eklenir, **Ürün şeridini kaldır** ile kaldırılır. Tabs'ta her sekme öğesinin
kendi kaynağı vardır.
| Alan | Açıklama |
|---|---|
| Kaynak | `Yeni Gelenler`, `Çok Satanlar`, `Kampanyalı Ürünler`, `Kategori`, `Marka`, `Manuel Ürün Listesi`, `Son Gezilenler (üyeye özel)`, `Favoriler (üyeye özel)`. |
| Ürün adedi (limit) | 1–48 (boş = varsayılan 12). |
| Sıralama | `Varsayılan`, `En Yeni`, `Fiyat Artan`, `Fiyat Azalan`. |
| Kanal Kategorisi | Kaynak `Kategori` iken; kanalın yayındaki kategorileri. |
| Satış penceresi (gün) | Kaynak `Çok Satanlar` iken; boş = 90. |
| Ürün kodları | Kaynak `Manuel` iken; virgül/satırla ayrılmış kodlar, sıra korunur. |
| Marka değer Id | Kaynak `Marka` iken; marka özellik değerinin kimliği. |
| Min fiyat / Max fiyat | Fiyat aralığı. |
| Etiketler | En az biri eşleşen ürün etiketleri (Kategori kaynağında kapalı). |
| Yalnız stokta olanlar / Yalnız indirimliler | Onay kutuları (Kategori kaynağında kapalı — kategori kendi stok kuralını uygular). |

Üyeye özel kaynaklarda not: içerik ziyaretçinin kendi verisiyle dolar; **misafirde blok görünmez**, panelde önizlenemez.

**Koleksiyon Kaynağı** — Koleksiyonlar tipinde zorunlu.
| Alan | Açıklama |
|---|---|
| Adet (limit) | 1–50 (boş = 10). |
| Sıralama | `En Yeni` / `Popüler (görüntülenme)`. |
| Manuel seçim (ShareCode, virgüllü) | Belirli koleksiyonların paylaşım kodları; boş = otomatik. |
Yalnız **onaylı + herkese açık** koleksiyonlar listelenir (bkz. Koleksiyon Moderasyonu).

**Görünüm**
| Alan | Görünme koşulu | Açıklama |
|---|---|---|
| Tema | Carousel tipleri | Arka plan teması: `varsayilan`, `sicak`, `mavi`, `yesil`, `lila`, `turuncu-kirmizi`, `mavi-mor`, `yesil-turkuaz`, `pembe-sari`, `lacivert-mavi`, `gun-batimi`, `deniz-uclu`, `orman-uclu`, `seker-uclu`, `premium-uclu`, `flash-urunler`. |
| "Tümünü Gör" linki | Her zaman | Blok başlığındaki bağlantı (örn. `/urun-listesi`). |
| Flash bitişi | Şablon `flash` | Geri sayımın bittiği tarih-saat. |
| Görünüm tipi | Kategoriler bloğu | `Kapsül` / `Kare`. |
| Mobilde yatay kaydırma (carousel) | Her zaman | Mobilde öğeler yatay kaydırılır. |

Kayıtlı ayar okunamıyorsa sarı uyarı ve **Ayarları sıfırla** butonu görünür.

**Hedefleme (kimlere gösterilsin)** — Blok seviyesinde kural alan tiplerde (Banner, Carousel, Infinity, Tabs,
Koleksiyonlar, Kategoriler, Markalar, Instagram, Kategori Çok Satanları). Seçim yoksa "Hedefleme yok — herkese
gösterilir." Seçili koşulların **tümü** sağlanan ziyaretçiye gösterilir; bir alan içindeki seçenekler "veya"dır.
| Alan | Seçenekler |
|---|---|
| Cinsiyet | Kadın / Erkek |
| Cihaz | Mobil / Masaüstü / iOS Uygulama / Android Uygulama |
| Üyelik | Misafir / Üye |
| Şehirler | 81 il (çoklu) |
| Bölgeler | Marmara, Ege, Akdeniz, İç Anadolu, Karadeniz, Doğu Anadolu, Güneydoğu Anadolu |
| Üye Grupları | Tanımlı üye grupları (çoklu) |

### Öğeler
Öğe destekleyen tiplerde (Banner, Slider, Story, Tabs, Kategoriler, Markalar, Instagram, Duyuru Şeridi, Çoklu
Banner, Görselli Yorumlar, Bilgi Bandı, İkon Banner, Çerçevesiz Carousel) **Öğeler (N)** tablosu:
| Sütun | Anlamı |
|---|---|
| Sıra | Öğe sırası. |
| Başlık | Öğe başlığı. |
| Görsel | Küçük resim; görsel yoksa seçili ikon. |
| Link | Öğenin hedefi. |
| Durum | `Aktif` / `Pasif`. |
| İşlem | **Kaldır** (listeden çıkarır; Kaydet ile kesinleşir). |

Satıra tıklayınca **Öğe Düzenle**, **Öğe Ekle** ile yeni öğe penceresi açılır; **Tamam** değişikliği listeye
işler, kalıcı kayıt için sayfadaki **Kaydet** gerekir. "Öğe yok — öğesiz öğeli blok yayında gösterilmez."

**Öğe Ekle / Öğe Düzenle penceresi**
| Alan | Açıklama / kurallar |
|---|---|
| Başlık (TR) / Alt başlık (TR) | Metinler (story/yorum/bilgi bandı kartlarında gösterilir). |
| Video URL (story) | Story tipinde oynatılacak video adresi. |
| Buton metni (TR) | Banner/slider butonu. |
| Link | Listeden seçilir: `Ana Sayfa (/)`, `Tüm Ürünler (/urun-listesi)`, kanalın kategorileri (`Ad (/url)`) ya da `Özel URL (elle gir)…` — özel değer `/` veya `https://` ile başlamalı, aksi halde kırmızı uyarı. |
| Görsel / Mobil görsel | **Görsel Yükle** ile dosya seçilir (JPEG/PNG/WebP/GIF/SVG) ve CDN'ye yüklenir; **Değiştir** / **Kaldır**. Ürünlerin `images` kökü kullanılmaz: masaüstü dosyaları `storefront/pages/desktop`, mobil dosyaları `storefront/pages/mobile` altında tutulur ve public URL `https://cdn.misharitalia.com/storefront-v1/...` biçimindedir. Tipine göre önerilen boyut altta yazar (aşağıdaki tablo). Öneriye uymayan dosyada sarı uyarı + **Yine de Yükle** / **Vazgeç**. |
| İkon | Açılır ızgaradan ikon seçilir (kamyon, yüzde, hediye, yıldız…); **Kaldır** temizler. Banner rozet metni için aynı alanda serbest metin (`%50 İndirim`) yazılabilir. |
| Sıra | Sayı. |
| Aktif | Pasif öğe yayına girmez. |
| Yeni sekmede aç | Link yeni sekmede açılır. |
| Sekmenin Ürün Kaynağı | Yalnız Tabs'ta: sekmenin ürün kaynağı (Ürün Kaynağı alanlarıyla aynı). |
| Öğe Hedeflemesi | Öğe seviyesinde kural alan tiplerde (Slider, Story, Tabs, Duyuru Şeridi, Çoklu Banner, Görselli Yorumlar, Bilgi Bandı, İkon Banner, Çerçevesiz Carousel); Hedefleme formunun aynısı. Banner öğelerine kural verilemez. |

**Görsel boyut önerileri** (uyarı engellemez, "Yine de Yükle" mümkündür):
| Blok tipi | Görsel | Mobil görsel |
|---|---|---|
| Slider | 1920×840 (oran ~2.3), ≤350 KB, en az 1600 px genişlik | ≥800 px genişlik, ≤250 KB |
| Banner | 1200×490 (oran ~2.4), ≤200 KB, en az 1100 px | görsel ile aynı |
| Çoklu Banner | 330×495 (dikey 2:3), ≤120 KB | aynı |
| Çerçevesiz Carousel | 800×978, ≤250 KB | aynı |
| Görselli Yorumlar | 480×600, ≤150 KB | aynı |
| Story | kapak ≥128×128 kare, ≤100 KB | aynı |
| Kategoriler | ≥128×128, ≤100 KB | aynı |
Uyarı türleri: "Görsel dar" (telefonda bulanık), "En-boy oranı uyumsuz" (kırpılır/ezilir), "Dosya büyük"
(sayfa hızını düşürür; WebP önerilir). SVG/GIF piksel kontrolüne girmez.

## Geçmiş & Versiyonlar sayfası
![Vitrin — Geçmiş & Versiyonlar](img/storefront-pages-history.webp)
Vitrin Yönetimi'nden **Geçmiş & Versiyonlar →** ile açılır (platform oradan gelir; doğrudan açılırsa "Platform
seçilmedi — vitrin yönetiminden gelin." ve geri butonu). Üç kart:
| Kart | İçerik | Aksiyon |
|---|---|---|
| Yayın Versiyonları | `vN`, yayın tarihi, not; aktif olanda `Aktif Yayın` rozeti. | Diğer versiyonlarda **Bu versiyona dön** → o versiyon yeniden aktif olur (geri dönüş kaydı açılır; taslaklar değişmez). |
| Yayın Geçmişi | Her yayın denemesi: `vN (önceki vM)`, tarih, not, rozet `Yayınlandı` / `Geri Dönüş` / `Başarısız`; başarısızda hata metni. | — |
| Değişiklik Geçmişi | Kim, neyi, ne zaman: `Oluşturuldu`, `Güncellendi`, `Silindi`, `Aktifleştirildi`, `Pasifleştirildi`, `Yayınlandı`, `Geri Dönüş`, `Önizlendi` rozetleri + kayıt adı + kullanıcı + tarih. | — |
**← Vitrin Yönetimi** butonu listeye döner.

## Durumlar ve iş kuralları
- **Taslak ≠ canlı:** blok ekleme/düzenleme/silme/sıralama yalnız taslağı değiştirir; site yalnız **aktif yayın
  versiyonunu** okur. Canlıya almak için **Yayınla** şarttır.
- Yayınla, platformun **tüm yerleşimlerindeki aktif** blokları tek versiyonda toplar; pasif blok ve pasif öğeler
  yayına girmez. Tarih penceresi ve hedefleme canlıda ziyaretçi bazında değerlendirilir.
- Yayın engelleri ("Yayınlanamadı: …" ve geçmişte `Başarısız`): bilinmeyen tip/yerleşim, tipe uymayan şablon,
  zorunlu ürün/koleksiyon kaynağı tanımsız, kural verilemeyen tipte kural, bozuk kural içeriği. Hata düzeltilene
  kadar önceki versiyon yayında kalır.
- Öğeli blok öğesiz yayınlanırsa sitede gösterilmez; üyeye özel kaynaklı blok misafirde gösterilmez.
- Yeni yayın sitede kendiliğinden geçerli olur (önbellek versiyonla yenilenir); geri dönüş de aynı şekilde.
- Geri dönüş taslakları değiştirmez; taslaklar en son düzenlediğiniz hâlde kalır. Tekrar Yayınla taslaklardan
  yeni versiyon üretir.
- Blok tipleri ve kural seviyeleri: Banner (blok kuralı, öğeli, şablonlu), Slider/Story/Duyuru Şeridi/Çoklu Banner/
  Görselli Yorumlar/Bilgi Bandı/İkon Banner/Çerçevesiz Carousel (öğe kuralı, öğeli), Carousel Ürün Listesi
  (blok kuralı, ürün kaynağı zorunlu, şablonlu), Infinity Ürün Listesi ve Kategori Çok Satanları (blok kuralı,
  ürün kaynağı zorunlu), Tabs (blok+öğe kuralı, sekme öğeleri kendi kaynağıyla), Koleksiyonlar (blok kuralı,
  koleksiyon kaynağı zorunlu), Kategoriler/Markalar/Instagram (blok kuralı, öğeli).

## Adım adım
**Ana sayfaya kampanya banner'ı ekleyip yayınlama**
1. Platformu seçin, **Ana Sayfa** sekmesinde **Yeni Blok** → tip `Banner`, şablon `ikili`, başlık girin, **Oluştur**.
2. Detayda **Öğe Ekle** → Başlık, **Görsel Yükle** (öneri 1200×490), Link'ten kampanya kategorisini seçin, **Tamam**;
   ikinci öğeyi de ekleyin. Gerekirse Başlangıç/Bitiş tarihi verin. **Kaydet**.
3. ← Vitrin Yönetimi'ne dönün; oklarla sıraya koyun; **Segment Önizleme → Önizle** ile Görünür olduğunu doğrulayın.
4. Yayın notu yazıp **Yayınla**. Hata kutusu çıkarsa mesajdaki bloğu düzeltip tekrar yayınlayın.

**Yanlış yayını geri alma**
1. **Geçmiş & Versiyonlar →** → Yayın Versiyonları'nda önceki `vN` satırında **Bu versiyona dön**.
2. Taslakta hatayı düzeltin ve yeniden **Yayınla**.

**Belirli şehirlere özel slider görseli**
1. Slider bloğunun detayında öğeyi açın; **Öğe Hedeflemesi**'nde Şehirler'e ilgili illeri ekleyin; **Tamam** → **Kaydet**.
2. Segment Önizleme'de Şehir'i seçerek öğe sayacını (`öğe: N/M`) kontrol edin; **Yayınla**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Blok silme onay ister ama geri alınamaz; yayındaki kopya bir sonraki Yayınla'ya kadar sitede kalır.

> **Dikkat:** Detay sayfasında öğe eklemek/kaldırmak tek başına yetmez — pencere **Tamam**'dan sonra sayfadaki
> **Kaydet**'e basmazsanız öğe değişiklikleri kaybolur.

> **İpucu:** Blok listesindeki önizleme gerçek taslak içeriği gösterir; "Kaynak ürün döndürmedi" görüyorsanız kaynak
> filtrelerini (fiyat, etiket, stok) gevşetin.

> **Not:** "Yayınlanamadı: … ürün kaynağı tanımsız" mesajı Carousel/Infinity bloğunda Kaynak seçilmediğini gösterir;
> ya kaynak tanımlayın ya bloğu pasifleştirin.

> **Not:** Segment Önizleme "Gizli" diyorsa nedeni satırda yazar (tarih penceresi dışı, hedefleme uyumsuz, öğe/ürün
> yok, üyeye özel kaynak + misafir).

## İlgili sayfalar
- [Kanal Kategorileri](/rehber/vitrin/kanal-kategorileri/)
- [Koleksiyon Moderasyonu](/rehber/vitrin/koleksiyon-moderasyonu/)
- [Ürün Kartı](/rehber/vitrin/urun-karti/)
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
- [Gruplar (Üye Grupları)](/rehber/musteriler/uyelik-gruplari/)
