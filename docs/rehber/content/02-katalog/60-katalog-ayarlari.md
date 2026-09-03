---
title: Katalog Ayarları
route: /catalog/settings
group: Katalog
order: 60
summary: Ürün görsellerinin sunulduğu CDN/görsel sunucusu, yerel depolama ve video sunucusu ayarları ile resim setlerinin yönetildiği ekran.
---

## Ne işe yarar
Katalog Ayarları, ürün görsellerinin ve videolarının **nereye kaydedileceğini ve hangi adresten sunulacağını** belirleyen
ayarlar ile **resim setlerini** (aynı ürünün farklı çekim/kanal görsel takımları) yönetir. İki sekmesi vardır:
**Resim Sunucusu** (CDN, uygulamanın otomatik SFTP/S3 aktarımı, yerel fallback ve video sunucusu anahtarları) ve **Resim Setleri**.

Bu ekranı kurulum ve altyapı sorumluları kullanır — mağaza açılışında, görsel sunucusu/CDN değiştiğinde ya da yeni bir
resim seti (örn. ikinci bir çekim takımı) tanımlanacağında. Günlük ürün işlemlerinde buraya dokunulmaz.

> **Dikkat:** Buradaki değerler tüm mağaza görsellerinin adreslerini etkiler. Yanlış bir CDN adresi ya da yükseklik değeri
> sitedeki tüm ürün görsellerinin kırık/yanlış boyutta çıkmasına yol açar. Değişiklikleri anında geçerli olur.

## Ekran yerleşimi
![Katalog Ayarları — Resim Sunucusu sekmesi (CDN, Yerel Depolama / FTP, Video Sunucusu bölümleri) ve Resim Setleri sekmesi](img/catalog-settings.webp)
1. **Başlık** — "Katalog Ayarları", altında "Resim sunucusu ve resim seti yönetimi".
2. **Sekmeler** — `Resim Sunucusu` (varsayılan açık) ve `Resim Setleri`.
3. **İçerik** — Resim Sunucusu'nda bölümlü ayar formu ve altta **Kaydet**; Resim Setleri'nde set tablosu ve **Yeni Set** butonu.

## Sekmeler

### Resim Sunucusu
Formun başında CDN, toplu yükleme ve eski depolama bilgilerinin yönetildiği belirtilir. Alanlar bölümlere
ayrılmıştır; dolu alanlar yeşil kenarlıkla işaretlenir. Değerler boş bırakılırsa sistem varsayılanı kullanılır
(aşağıda belirtilmiştir).

Vitrin Yönetimi aynı SFTP/S3 bağlantı bilgilerini kullanır; ancak ürün dosyalarının `/var/www/html/images`
köküne yazmaz. Vitrin dosyaları ayrı `/var/www/html/storefront/pages/desktop` ve
`/var/www/html/storefront/pages/mobile` ağaçlarında, object storage tarafında da `storefront/` prefix'i altında tutulur.
Public sunum yolu edge-cache güvenliği için sürümlüdür: `https://cdn.misharitalia.com/storefront-v1/`.
Eski taşınmış görsellerde bulunmayan responsive adaylara istek atılmaması için CDN `srcset` kapısı varsayılan
kapalıdır; yeni varyant ağacı eksiksiz doğrulandıktan sonra ayrıca açılabilir.

#### Form alanları — CDN Ayarları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| CDN Temel URL | Hayır | Görsellerin sunulduğu CDN kök adresi (örn. `https://cdn.alanadiniz.com/img`). Görsel adresleri `<CDN Temel URL>/<yükseklik>/<kalite>/<dosya>` biçiminde üretilir. |
| CDN Kalite (%) | Hayır | 0-100 arası sıkıştırma kalitesi. Boşsa **85**. |
| Thumbnail Yüksekliği | Hayır | Küçük görsel yüksekliği (piksel) — sepet, listelerdeki küçük resimler. Boşsa **240**. |
| Liste/Detay Yüksekliği | Hayır | Kategori listesi kartları ve ürün detayı ana görseli. Boşsa **640**. |
| Zoom Yüksekliği | Hayır | Ürün detayında büyütme (zoom) görseli. Boşsa **1200**. |

#### Form alanları — Toplu Yükleme / SFTP
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Dönüştürme Kalitesi (%) | Evet | SFTP WebP ve S3 JPEG kopyalarının kalitesi; varsayılan **80**, geçerli aralık 1-100. |
| SFTP Sunucu Adresi | Evet | WebP origin sunucusunun host adı veya IP adresi. |
| SFTP Port | Evet | Varsayılan `22`. |
| SFTP Kullanıcı Adı | Evet | WebP origin hesabı. |
| SFTP Şifre | Evet | Kaydedilince şifrelenir ve ekranda `•••` görünür. Maskeli değer değiştirilmeden kaydedilirse mevcut şifre korunur. |
| SFTP Dosya Yolu | Evet | WebP hedef dizini; varsayılan `/var/www/html/images`. |

#### Form alanları — Toplu Yükleme / OVH Object Storage
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| S3 Servis URL | Evet | S3-compatible HTTPS endpoint; örnek `https://s3.de.io.cloud.ovh.net/`. |
| S3 Bucket | Evet | JPEG kopyalarının yazılacağı bucket adı. |
| S3 Access Key | Evet | Kaydedilince şifrelenir ve GET yanıtında maskelenir. |
| S3 Secret Key | Evet | Kaydedilince şifrelenir ve GET yanıtında maskelenir. |

#### Form alanları — Yerel Depolama
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Yerel Kayıt Dizini | Hayır | Panelden yüklenen görsellerin sunucuda kaydedildiği klasör (örn. `/opt/ECSProsAI/media/images/products/`). |
| Yerel Sunucu URL | Hayır | Bu klasördeki dosyaların dışarıdan erişileceği adres ön eki (örn. `/media/images/products/`). CDN tanımlı olmayan setlerde görsel adresi buradan üretilir. |

#### Form alanları — Video Sunucusu
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Video Kayıt Dizini | Hayır | Panelden yüklenen ürün videolarının kaydedildiği klasör (örn. `/opt/ECSProsAI/media/videos/products/`). |
| Video Sunucu URL | Hayır | Video dosyalarının dışarıdan erişim adresi ön eki (örn. `/media/videos/products/`). |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Formun altı, sağ | Formdaki **tüm** alanlar tek seferde kaydedilir; başarılıysa yanında "✓ Kaydedildi" görünür. Hata olursa formun altında kırmızı mesaj çıkar ("Kayıt sırasında hata oluştu." ya da sunucu mesajı). | `catalog.settings.manage` |

### Resim Setleri
Resim seti, bir ürünün görsellerinin ait olduğu takımdır (örn. "Varsayılan Resim Seti", "Stüdyo Çekimi"). Her görsel
yüklenirken bir sete bağlanır; ürün kartının **Resimler** sekmesinde set seçici bulunur. Mağaza bir setten görsel
isterken o sette görsel yoksa yedek zinciri izlenir: ürün için tanımlı özel eşleme → setin **Fallback** seti (zincir
halinde) → **Varsayılan** set.

| Sütun | Anlamı |
|---|---|
| KOD | Setin kodu (küçük harf, boşluksuz); varsayılan sette yanında `Varsayılan` rozeti. Dosya adlarında kullanılır. |
| AD | Görünen ad. |
| FALLBACK | Bu sette resim yoksa kullanılacak yedek setin adı; yoksa `—`. |
| ÖNCELİK | Sıra önceliği; tablo bu değere (küçükten büyüğe), eşitlikte ada göre dizilir. |
| DURUM | `Aktif` / `Pasif`. |
| (kalem) | Düzenleme penceresini açar (satıra tıklamak da aynı işi yapar). |

Üstte "N set tanımlı" bilgisi ve **Yeni Set** butonu bulunur. Set yoksa "Henüz resim seti tanımlanmamış." yazısı görünür.

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Set | Sekme, sağ üst | "Yeni Resim Seti" penceresi açılır. | Panele giriş yapmış kullanıcı |
| Satır tıklama / kalem | Tablo | "Resim Setini Düzenle" penceresi açılır. Varsayılan sette alanlar salt okunur gelir ("Varsayılan resim seti düzenlenemez ve silinemez.") ve yalnız **Kapat** vardır. | — |
| Oluştur / Kaydet | Pencere, sağ alt | Set oluşturulur ya da güncellenir. Ad (ve oluşturmada Kod) boşsa pasiftir. | — |
| Sil → "Emin misiniz?" → Evet, Sil / Vazgeç | Düzenleme penceresi, sol alt | ⚠️ İki aşamalı onay; onaylanınca set silinir. | Varsayılan olmayan set; sete bağlı resim/video/ürün eşlemesi olmamalı ve başka setin fallback'i olmamalı |
| İptal / Kapat | Pencere | Pencereyi kapatır. | — |

#### Form alanları — Yeni Resim Seti / Resim Setini Düzenle
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (yalnız oluşturmada) | Otomatik küçük harfe çevrilir, boşluklar `-` olur (örn. `standart`). Oluşturulduktan sonra değiştirilemez; dosya adlarında kullanılır. Aynı kod varsa: "'<kod>' kodu zaten kullanılıyor." |
| Ad | Evet | Görünen ad (örn. "Standart Çekim"). |
| Fallback Set | Hayır | Açılır liste (`— Yok —` veya diğer setler). "Bu sette resim yoksa kullanılacak yedek set." |
| Sıra Önceliği | Hayır | 0 ve üzeri tam sayı; küçük değer üstte. |
| Aktif | Hayır | Yalnız düzenlemede; kaldırılırsa set pasife alınır. |

## Durumlar ve iş kuralları
- **Görsel adresi kuralı:** Mağazadaki görsel adresleri `CDN Temel URL / yükseklik / kalite / dosya adı` biçiminde üretilir; üç yükseklik (thumbnail / liste-detay / zoom) farklı yerlerde kullanılır. Alan boşsa varsayılan (85, 240, 640, 1200) devreye girer.
- **Toplu yükleme hedefi:** Varsayılan aktif dual-target akışında WebP, SFTP Dosya Yolu'na; aynı benzersiz
  basename'li JPEG, S3 bucket köküne yazılır. Servis ayarları her dosya upload'ının başlangıcında bu
  ekrandan yeniden okur.
- **Hata-güvenli davranış:** SFTP veya S3 ayarı eksikse ya da hedeflerden biri başarısızsa görsel kaydı
  aktifleştirilmez. Normal yapılandırmada sessiz yerel disk fallback'i yoktur; yerel adapter yalnız geliştirme
  veya teşhis için `CatalogImageStorage:Enabled=false` açık override'ıyla seçilebilir.
- **Kullanıcı aktarımı:** Kullanıcı FTP/SFTP istemcisiyle dosya göndermez. Toplu yükleme ekranına seçilen dosyaları API,
  arka planda SFTP ile CDN origin'e ve S3 API'siyle object storage'a aktarır.
- **Secret güvenliği:** SFTP şifresi ile S3 anahtarları Data Protection kullanılarak şifreli saklanır ve API GET yanıtında gerçek değerlerini dönmez.
- **Eski FTP alanları:** Kullanıcı akışında kullanılmaz ve formda gösterilmez; veri kaybı oluşmaması için
  daha önce kaydedilmiş anahtarlar DB'de yerinde bırakılır.
- **Varsayılan set:** Sistemde tek bir varsayılan set vardır; düzenlenemez ve silinemez. Tüm yedek zincirlerinin son durağıdır.
- **Set silme koşulları:** Set silinemez eğer — varsayılansa; sete bağlı resim varsa; sete bağlı video varsa; ürün bazlı set eşlemelerinde kullanılıyorsa; başka bir setin fallback'iyse. Her durumda pencerede ilgili hata mesajı gösterilir.
- **Kod değişmez:** Set kodu dosya adlarına işlendiği için oluşturulduktan sonra değiştirilemez.
- **Anında geçerlilik:** Kaydedilen sunucu ayarları bir sonraki görsel isteğinden itibaren geçerlidir; yeniden başlatma gerekmez.

## Adım adım

### CDN / görsel sunucusu ayarlarını girme
1. **Katalog › Katalog Ayarları**'na girin; `Resim Sunucusu` sekmesi açık gelir.
2. **CDN Temel URL**'yi (sonunda `/` olmadan) ve gerekirse **CDN Kalite (%)** ile üç yükseklik değerini girin.
3. Toplu yükleme için SFTP host/port/kullanıcı/şifre/yol ile S3 servis URL/bucket/access key/secret key alanlarını doldurun.
4. **Kaydet**'e tıklayın; "Kaydedildi" yazısını ve secret alanlarının maskelendiğini görün.
5. Kontrollü bir toplu yüklemede WebP ve JPEG kopyalarının iki hedefte de oluştuğunu doğrulayın.

### Yeni resim seti tanımlama
1. `Resim Setleri` sekmesine geçin, **Yeni Set**'e tıklayın.
2. **Kod** (örn. `studyo`) ve **Ad** (örn. "Stüdyo Çekimi") girin.
3. Bu sette görseli olmayan ürünler için **Fallback Set** seçin (genellikle varsayılan set).
4. **Sıra Önceliği** verin ve **Oluştur**'a tıklayın.
5. Ürün kartlarının Resimler sekmesinde yeni set artık seçilebilir.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Yükseklik değerlerini değiştirmeden önce CDN'nizin o yükseklikte görsel üretebildiğinden emin olun; CDN yalnız tanımlı boyutları sunuyorsa farklı bir değer kırık görsele yol açar.

> **Dikkat:** Kaydet ekranda gösterilen alanları yazar. Eski FTP anahtarları ekranda gösterilmez ve Kaydet işlemi bunları değiştirmez.

> **Dikkat:** Bir resim setini silemiyorsanız pencerede nedeni yazar (bağlı resim/video, ürün eşlemesi ya da başka setin fallback'i). Önce bağımlılığı kaldırın ya da seti **Pasif** yapmakla yetinin.

> **Not:** Varsayılan set penceresinde yalnız Kod ve Ad salt okunur görünür; **Kapat** dışında işlem yoktur.

## İlgili sayfalar
- [Ürün Kartları](/rehber/katalog/urun-kartlari/) — Resimler sekmesi ve set seçimi
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
