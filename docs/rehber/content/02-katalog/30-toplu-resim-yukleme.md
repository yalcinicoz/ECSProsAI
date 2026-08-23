---
title: Toplu Resim Yükleme
route: /catalog/bulk-images
group: Katalog
order: 30
summary: Bir klasördeki ürün resimlerini dosya adındaki barkoda göre ilgili varyantlara tek seferde eşleyip yükleme ekranı.
---

## Ne işe yarar
Fotoğraf çekimi sonrası yüzlerce resmi ürün ürün açıp yüklemek yerine, resimleri **barkoda göre adlandırılmış** bir
klasöre koyarsınız; bu ekran dosya adından barkodu okur, barkodun bağlı olduğu ürün/varyantı bulur ve tüm resimleri
tek tuşla yükler. Katalog ve fotoğraf ekibi, yeni sezon/parti resimlerini kataloğa işlerken bu ekranı kullanır.

## Ekran yerleşimi
![Toplu Resim Yükleme — set/arşiv ayar şeridi, klasör seçme alanı, özet şeridi ve barkod kartları](img/catalog-bulk-images.webp)
1. **Yol bağlantısı** — "Ürün Kartları › Toplu Resim Yükleme".
2. **Ayar şeridi** — `Set:` çipleri (resim seti) ve sağda **Mevcut resimleri arşivle** onay kutusu.
3. **Klasör alanı** — "Klasör seç veya sürükle-bırak" kesikli kutu; dosya adı biçimi ve taşıma bilgisi; seçilen klasörün adı.
4. **Özet şeridi** (klasör seçilince) — toplam resim, eşleşen ürün, bulunamayan barkod, sorgulanıyor sayaçları; sağda
   yükleme sonucu ve **Tümünü Yükle (N)** butonu.
5. **Barkod kartları** — her barkod için bir kart: barkod, eşleşen ürün › SKU, resim sayısı, durum, küçük resimler.

## Dosya adı kuralı
- Biçim: `barkod_sıra.uzantı` → `8690000000011_1.jpg`, `8690000000011_2.jpg`, `8690000000011_3.png`.
- Son alt çizgiden **önceki** kısım barkod, sonraki sayı sıra numarasıdır. Sıra numarası yoksa (`8690000000011.jpg`)
  dosya 0. sıra kabul edilir.
- Aynı barkodlu dosyalar bir araya toplanır ve sıra numarasına göre dizilir; **en küçük sıradaki resim kapak** olur
  (kartta "Kapak" rozeti).
- Kabul edilen uzantılar: jpg/jpeg, png, webp, gif, bmp, tif/tiff. Klasördeki alt klasörler ve `yuklenenler` klasörü
  okunmaz; yalnız klasörün kendi içindeki dosyalar alınır.
- Barkod, ürün kartındaki **Varyantlar → BARKOD** alanındaki değerle birebir aynı olmalıdır.

## Butonlar ve aksiyonlar

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Set: çipleri | Ayar şeridi | Resimlerin yükleneceği resim setini seçer; varsayılan set seçili gelir. | En az bir resim seti tanımlı olmalı. |
| Mevcut resimleri arşivle | Ayar şeridi | Varsayılan **işaretli**. İşaretliyken her varyant için seçili setteki mevcut aktif resimler arşive alınır ve yeni resimler onların yerine geçer. İşareti kaldırırsanız yeni resimler mevcutların yanına eklenir (ilk dosya yine kapak olarak işaretlenir). | — |
| Klasör seç (tıklama) | Klasör alanı | Tarayıcının klasör seçme penceresi açılır; klasör okuma + yazma izni istenir (taşıma için). | Chrome/Edge gibi klasör erişimini destekleyen tarayıcı. |
| Sürükle-bırak | Klasör alanı | Klasörü ya da dosyaları kutuya bırakın; klasör bırakılırsa taşıma özelliği de çalışır. | — |
| Tümünü Yükle (N) | Özet şeridi sağı | Eşleşen ve henüz yüklenmemiş tüm barkod gruplarını sırayla yükler; N = yüklenecek grup sayısı. | Tüm barkod sorguları bitmiş olmalı, en az bir eşleşme, set seçili. |
| × (küçük resim üstünde) | Barkod kartı | O dosyayı yükleme listesinden çıkarır (diskten silmez). Grubun son dosyası çıkarılınca kart kaybolur. | Grup henüz yüklenmemiş. |
| Ürün adı bağlantısı | Barkod kartı | Eşleşen ürünün detay sayfasını **yeni sekmede** açar. | Barkod eşleşmiş. |

## Barkod kartı ve durumlar

| Öğe / rozet | Anlamı |
|---|---|
| Barkod (kalın) | Dosya adından okunan barkod. |
| Dönen simge | Barkod sorgulanıyor. |
| ✓ (mavi) + ürün adı › SKU | Barkod bir varyantla eşleşti; kartın sol kenarı renkli. |
| `Bulunamadı` (turuncu) | Bu barkoda sahip varyant yok; bu grup **yüklenmez**. Kartın sol kenarı turuncu. |
| `N resim` | Gruptaki dosya sayısı. |
| `Kapak` rozeti + kalın kenarlık | Gruptaki ilk (en küçük sıra) resim; ürün ve varyant kapağı olarak işaretlenir. |
| Sağ alt sayı | Dosya adındaki sıra numarası. |
| `Yükleniyor` | Grup yükleniyor. |
| `Yüklendi` (+ `N taşındı`) | Grup başarıyla yüklendi; klasör erişimi varsa dosyalar `yuklenenler/` alt klasörüne taşındı. |
| `Hata` + kırmızı açıklama | Grup yüklenemedi (örn. "Hiçbir dosya yüklenemedi"). Diğer gruplar etkilenmez. |

**Özet şeridi:** `N resim` · `N ürün eşleşti` · `N barkod bulunamadı` (turuncu) · `Sorgulanıyor...`; yükleme sonunda
`N yüklendi` · `N taşındı` · `N hata`.

## Durumlar ve iş kuralları
- Barkodlar **10'arlı gruplar halinde** sorgulanır; büyük klasörlerde sorgu bitene kadar **Tümünü Yükle** pasiftir.
- Yükleme her barkod grubu için ayrı yapılır: dosya adları sunucuda otomatik (`ÜRÜNKODU_SET_VARYANT_xx`) verilir,
  sıra numarası dosya adındaki sıraya göre 1'den başlar; ilk resim hem **ürün kapağı** hem **varyant kapağı** olur.
- Resimler daima **varyanta bağlı** yüklenir (ürün geneline değil). Renk ekseni olan ürünlerde aynı rengin tüm
  bedenleri aynı barkoda sahip olmadığından, bir rengin resimlerini o rengin **herhangi bir bedeninin** barkoduyla
  yüklemeniz yeterlidir; site, renk için o varyantın resimlerini kullanır.
- "Mevcut resimleri arşivle" yalnız **aynı set + aynı varyant**taki resimleri arşivler; başka setler ve diğer
  varyantlar etkilenmez. Arşivlenen resim silinmez.
- Klasörü dosya seçici ile (klasör erişimi olmadan) ya da dosya sürükleyerek yüklediyseniz taşıma yapılmaz; ekranda
  "Bu tarayıcı otomatik taşıma desteklemiyor — yükleme sonrası dosyalar yerinde kalır" uyarısı görünür.
- Başarılı yüklenen dosya, klasör erişimi varsa `yuklenenler/<sunucu-dosya-adı>` olarak taşınır ve asıl dosya
  silinir; böylece aynı klasörü tekrar açtığınızda yalnız yüklenmemiş dosyalar kalır. Yüklenemeyen dosya yerinde kalır.
- Aynı klasör yeniden açılırsa önceki sonuçlar sıfırlanır; yüklenmiş (taşınmış) dosyalar artık listede görünmez.

## Adım adım

**Bir çekim partisini yükleme**
1. Resimleri bilgisayarınızda tek bir klasöre `barkod_1.jpg, barkod_2.jpg …` biçiminde adlandırarak koyun.
2. **Katalog → Toplu Resim Yükleme**'yi açın; `Set:` çiplerinden doğru seti seçin (genelde varsayılan).
3. Eski resimlerin yerine geçmesini istiyorsanız **Mevcut resimleri arşivle** işaretli kalsın; eklemek istiyorsanız kaldırın.
4. Klasör alanına tıklayıp klasörü seçin (tarayıcı izin sorarsa "Dosyaları düzenle/kaydet" iznini verin) ya da klasörü kutuya sürükleyin.
5. Özet şeridinde `Sorgulanıyor...` bitene kadar bekleyin. `Bulunamadı` olan barkodları not alın.
6. Kartlardaki küçük resimleri kontrol edin; yanlış dosyayı × ile listeden çıkarın. Kapak olacak resim 1 numaralı olmalı.
7. **Tümünü Yükle (N)**'e basın. Kartlar sırayla `Yüklendi` olur; sonunda özet şeridinde `N yüklendi · N taşındı` görünür.
8. `Hata` alan gruplar için açıklamayı okuyun, sorunu giderip aynı klasörü tekrar seçin — yalnız kalan dosyalar listelenir.

**"Bulunamadı" barkodlarını çözme**
1. Kartta barkodu kopyalayın; ürünün detay sayfasında **Varyantlar** sekmesinde BARKOD hücresini kontrol edin.
2. Barkod boşsa yazın ya da **Barkod Oluştur**'u kullanın; yazım farkı varsa dosya adını ya da varyant barkodunu düzeltin.
3. Klasörü yeniden seçin; grup artık eşleşir.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** En iyi deneyim için Chrome ya da Edge kullanın; klasör erişimi sayesinde yüklenen dosyalar otomatik
> `yuklenenler/` klasörüne taşınır ve yarım kalan işe kaldığınız yerden devam edebilirsiniz.

> **Dikkat:** Sıra numarası olmayan dosya 0. sıra olur ve `_1` dosyasının önüne geçerek **kapak** olabilir. Kapak
> olmasını istediğiniz resme en küçük numarayı verin.

> **Dikkat:** Dosya adında barkoddan sonra alt çizgi + sayı dışında bir ek varsa (örn. `8690000000011_on.jpg`) son
> parça sayı olmadığı için **tüm ad barkod sanılır** ve eşleşmez. Yalnız `_1`, `_2` … kullanın.

> **Not:** "Tümünü Yükle" pasifse ya henüz barkod sorgusu bitmemiştir, ya eşleşen hiç grup yoktur, ya da resim seti
> seçilmemiştir. Set listesi boşsa önce Katalog Ayarları'ndan bir resim seti tanımlayın.

> **Not:** Yükleme ürün kartında **Görseller** sekmesine yansır; orada kapağı değiştirebilir, tek tek arşivleyebilir ya
> da ürün geneli resim ekleyebilirsiniz.

## İlgili sayfalar
- [Ürün Kartları](/rehber/katalog/urun-kartlari/)
- [Ürün Detayı](/rehber/katalog/urun-detay/)
- [Katalog Ayarları](/rehber/katalog/katalog-ayarlari/)
