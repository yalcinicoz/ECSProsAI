---
title: Pazaryerleri
route: /marketplaces
group: Sipariş Yönetimi
order: 90
summary: Pazaryeri mağazalarınızın (Trendyol, Hepsiburada, n11, Amazon, Çiçeksepeti, Pazarama) kart görünümü; mağaza detayında ürün hazırlık denetimi, ürün gönderimi, fiyat-stok güncelleme, sipariş çekme, senkron geçmişi, sorun kuyruğu ve mutabakat.
---

## Ne işe yarar
Pazaryerleri sayfası, firmalarınızın pazaryeri mağazalarını tek ekranda toplar: hangi mağazanın bağlantısı sağlıklı, kaç ürün yüklü, kaç ürün gönderilmeyi bekliyor, kaç üründe hata var ve bugün kaç sipariş geldi. Mağaza kartına tıklayınca açılan **mağaza detayı** pazaryeri operasyonunun tamamını yürüttüğünüz yerdir: ürünleri hazırlık denetiminden geçirip gönderme, eksik bilgileri tamamlama, fiyat-stok güncelleme, sipariş çekme, gönderim paketlerini izleme, sorunları çözme ve mutabakat çalıştırma.

Bu ekranı pazaryeri operasyonundan sorumlu personel günlük olarak kullanır. Ürün gönderimi için önce [Kategori ve Özellik Eşleştirme](/rehber/siparis/kategori-ve-ozellik-eslestirme/) sayfasında eşlemelerin yapılmış olması gerekir.

## Ekran yerleşimi
![Pazaryerleri — mağaza kartları, firma filtresi ve özet şeridi](img/marketplaces.webp)
1. **Başlık çubuğu** — sayfa adı ve sağda üç buton: **Eşleştirme**, **Referans Verisi**, **Yeni Mağaza**.
2. **Firma filtresi + arama** — `Tümü` çipi ve her firma için ad + mağaza sayısı çipi; sağda "Mağaza ara…" kutusu.
3. **Özet şeridi** — beş kutu: Mağaza, Yüklü Ürün, Yüklenecek Ürün, Senkron Hatası, Bugün Gelen Sipariş (görünen mağazaların toplamı).
4. **Pazaryeri grupları** — mağazalar pazaryeri tipine göre gruplanır; grup başlığında logo, pazaryeri adı ve "N mağaza · N yüklü ürün" özeti; altında mağaza kartları.

*(1) Başlık ve butonlar · (2) Firma filtresi ve arama · (3) Özet şeridi · (4) Gruplu mağaza kartları*

## Liste ve filtreler

### Filtreler
| Filtre | Ne yapar |
|---|---|
| `Tümü` / firma çipleri | Kartları seçilen firmanın mağazalarıyla sınırlar. Çipteki sayı o firmanın mağaza adedidir. Bir firma seçiliyken **Yeni Mağaza** açılırsa firma alanı o firmayla dolu gelir. |
| Mağaza ara… | Yazdıkça mağaza adı, kodu veya pazaryeri adı üzerinde filtreler. |

Sıralama: aktif mağazası olan pazaryeri grupları önce, sonra gruplar ada göre; grup içinde aktif mağazalar önce, sonra koda göre. Hiç mağaza yoksa "Henüz pazaryeri mağazası tanımlanmamış." mesajı ve **İlk Mağazayı Ekle** butonu görünür; filtreye uyan yoksa "Filtreye uyan mağaza yok." yazar.

### Özet şeridi
| Kutu | Anlamı |
|---|---|
| Mağaza | Görünen mağaza sayısı; altında "N aktif · N pasif". |
| Yüklü Ürün | Tüm mağazalarda pazaryerine yüklenmiş varyant toplamı. |
| Yüklenecek Ürün | Kanalda satışa açık olup henüz gönderilmemiş ürün toplamı (yalnız aktif mağazalar). Sıfırdan büyükse sarı. |
| Senkron Hatası | Gönderimi hatalı biten varyant toplamı; altında kaç mağazada olduğu. Sıfırdan büyükse kırmızı. |
| Bugün Gelen Sipariş | Bugün oluşan pazaryeri siparişleri toplamı. |

### Mağaza kartı
| Kart öğesi | Anlamı |
|---|---|
| Üst renk şeridi + sağlık noktası | Mağaza sağlığı: yeşil (sağlıklı), sarı (uyarı), kırmızı (hata), gri (pasif). |
| Logo, ad, kod | Pazaryeri kısaltması (TY, HB, n11, AMZ, ÇS, PZ), mağazanın kanal adı ve kodu. |
| Firma rozeti | Birden fazla firma varsa mağazanın hangi firmaya ait olduğu. |
| `Aktif` / `Pasif` rozeti | Kanalın aktiflik durumu. Pasif mağaza kartı soluk görünür ve senkron çalışmaz. |
| Sağlık satırı | Aşağıdaki tablodaki mesajlardan biri; kart üzerinden bir senkron işlemi çalıştırıldığında o işlemin sonucu burada gösterilir. |
| YÜKLÜ | Pazaryerine yüklü varyant sayısı. |
| YÜKLENECEK | Kanalda açık, henüz gönderilmemiş ürün sayısı (pasif mağazada `—`). Sıfırdan büyükse sarı. |
| HATALI / AÇIK SİPARİŞ | Hatalı varyant varsa kırmızı **HATALI** sayısı; yoksa **AÇIK SİPARİŞ** sayısı gösterilir. |
| Ürünler · Siparişler · Senkron ▾ · ⚙ | Kartın alt butonları (bkz. Butonlar ve aksiyonlar). |

Sağlık mesajları (öncelik sırasıyla değerlendirilir — ilk tutan kural gösterilir):
| Renk | Mesaj | Anlamı / ne yapmalı |
|---|---|---|
| Gri | Mağaza pasif — senkron durduruldu | Kanal pasif. Ayarlar'dan **Aktif** işaretleyin. |
| Kırmızı | N üründe senkron hatası | Gönderimi hatalı biten ürün var → detay Ürünler → **Hatalı** filtresi. |
| Sarı | N açık sorun — Sorunlar sekmesine bakın | Mutabakat/gönderim sonuçlarından açılmış sorun var → detay **Sorunlar** sekmesi. |
| Sarı | Bağlantı kurulmadı — pazaryeri sözleşmesi/API bilgisi eksik | Mağazaya bağlı aktif pazaryeri sözleşmesi yok → Firma detayı → Entegrasyonlar. |
| Yeşil | Bağlantı hazır — henüz senkron yapılmadı | Sözleşme bağlı, ilk senkron yapılmamış. |
| Yeşil | Bağlantı sağlıklı · son senkron X önce | Her şey yolunda. |

Kartın boş alanına tıklayınca mağaza detayı açılır.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Eşleştirme | Başlık sağ üst | [Kategori ve Özellik Eşleştirme](/rehber/siparis/kategori-ve-ozellik-eslestirme/) sayfasına gider. | — |
| Referans Verisi | Başlık sağ üst | **Pazaryeri Referans Verisi** penceresini açar (pazaryeri kategori/özellik/değer ağaçlarını indirir). | — |
| Yeni Mağaza | Başlık sağ üst | "Yeni Pazaryeri Mağazası" penceresi — mağaza formu (aşağıda). Platform tipi listesinde yalnız pazaryeri tipleri çıkar. | — |
| İlk Mağazayı Ekle | Boş durum kartı | Yeni Mağaza ile aynı. | Hiç mağaza yokken |
| Ürünler | Kart altı | Mağaza detayını **Ürünler** sekmesinde açar. | — |
| Siparişler | Kart altı | Mağaza detayını **Siparişler** sekmesinde açar. | — |
| Senkron ▾ → Ürünleri Gönder… | Kart altı | Detay Ürünler sekmesini **Hazır** filtresiyle açar; gönderimi oradan seçerek yaparsınız. | Mağaza aktif |
| Senkron ▾ → Stok-Fiyat Güncelle | Kart altı | Yüklü ürünlerin fiyat ve stoğunu pazaryerine iter; yalnız değişenler gönderilir. Sonuç kartta: "Fiyat-stok gönderildi: N varyant (M değişmemiş atlandı)". | Mağaza aktif + sözleşme bağlı |
| Senkron ▾ → Siparişleri Çek | Kart altı | Pazaryerinden yeni siparişleri sorgular. Sonuç kartta: "Sipariş çekildi: N yeni". | Mağaza aktif + sözleşme bağlı |
| Senkron ▾ → Mutabakat Çalıştır | Kart altı | Pazaryerindeki ürün listesini bizdeki kayıtlarla karşılaştırır (bkz. Durumlar ve iş kuralları). Sonuç kartta: "Mutabakat: N karşılaştırıldı · N otomatik düzeltme · N sorun". | Mağaza aktif + sözleşme bağlı; mutabakat desteği olan pazaryeri |
| ⚙ (Mağaza ayarları) | Kart altı | "Mağaza Ayarları — Ad" penceresinde mağaza formunu düzenleme modunda açar. | — |

> **Not:** Senkron menüsü pasif mağazada devre dışıdır. İşlem sürerken buton döner; hata olursa kartın sağlık satırında kırmızı mesaj görünür (ör. "Bu mağaza için aktif pazaryeri bağlantısı (sözleşme) yok.").

### Referans Verisi penceresi
Pazaryerlerinin kategori / özellik / değer ağaçları ayrı bir referans veritabanında saklanır ve bu pencereden güncellenir. Eşleştirme ekranı ve hazırlık denetimi bu veriyi kullanır.

| Öğe | Anlamı |
|---|---|
| Özet tablosu: Pazaryeri · Kategori · Özellik · Değer · Son Senkron | Pazaryeri başına indirilmiş kayıt sayıları. Kategori sütununda "(+N kaldırılmış)" pazaryerinin kaldırdığı kategorileri gösterir. Son Senkron'da durum rozeti (`Sürüyor a/b`, `Tamamlandı`, `Başarısız`), kapsam ve zaman; hiç çalışmadıysa "Henüz senkron yapılmadı". |
| Pazaryeri (seçim) | Senkronu başlatılacak pazaryeri. Listede yalnız referans indiricisi hazır pazaryerleri çıkar. |
| Kapsam (seçim) | `Kategoriler` (kategori ağacı) veya `Özellikler + Değerler (tüm yaprak kategoriler)`. |
| Senkronu Başlat | Arka planda koşu başlatır: "Senkron başlatıldı — ilerleme aşağıdaki listede." Pencereyi kapatabilirsiniz. |
| SON KOŞULAR | Son 10 koşu: pazaryeri + kapsam, durum rozeti, "+N yeni · ~N değişen · −N kaldırılan", ne zaman; hata varsa kırmızı metin. Sürüyor durumunda 4 saniyede bir kendini yeniler. |

> **Dikkat:** Özellik senkronu kategori sayısına göre uzun sürebilir. Aynı anda ikinci koşu başlatılamaz. Pencerede "Referans veritabanı yapılandırılmamış" kırmızı kutusu görünüyorsa sunucu ayarı eksiktir — sistem yöneticinize iletin.

> **İpucu:** Sıra: önce `Kategoriler`, sonra `Özellikler + Değerler`. Özellikleri indirilmemiş bir kategoriye eşlenen ürünler denetimde "Kategori özellikleri henüz indirilmedi" nedeniyle **Eksik** sayılır.

## Form alanları
Mağaza formu **Yeni Mağaza**, kart ⚙ ve detay **Ayarlar** sekmesinde aynıdır (kanal formuyla ortaktır).

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Firma | Evet | Yalnız yeni kayıtta, birden fazla firma varken ve firma filtresi `Tümü` ise görünür; aksi halde seçili firma kullanılır. |
| Platform Tipi | Evet | Yalnız yeni kayıtta. Liste "(Pazaryeri)" ekli tiplerle sınırlıdır (Trendyol, Hepsiburada, n11, Amazon, Çiçeksepeti, Pazarama). Düzenlemede değiştirilemez. |
| Kanal Adı | Evet | Mağazanın panelde görünen adı. Örn. `Trendyol Ana Mağaza`. |
| Kod | Otomatik | Yalnız yeni kayıtta, addan üretilir ve salt okunur gösterilir. |
| Fiyat Tipi / Çarpan | Hayır | `— Yok —`, `Manuel` veya `Çarpan`. Çarpan seçilirse çarpan değeri (örn. `1.10`). |
| Kimlik Bilgileri (API) | Şemaya göre | Platform tipinin alan şemasında kimlik alanı tanımlıysa sarı kutuda görünür. Pazaryeri API anahtarları ise **firma sözleşmesinde** tutulur (bkz. Durumlar ve iş kuralları). |
| Ayarlar | Şemaya göre | Platform tipinin şemasındaki ayar alanları. Şema yoksa "Bu platform tipi için alan şeması tanımlı değil." uyarısı çıkar. |
| Stoğu biten ürünleri listede göster | Hayır | Açılırsa "Yalnız bu tarihten sonra açılanlar" tarih alanı görünür (boş = tümü). |
| Ödeme Yöntemleri | En az biri | `Kart ile Öde (Online)`, `Kapıda Nakit Ödeme`, `Kapıda Kart ile Ödeme`. Kapıda ödeme seçiliyse hizmet bedeli ve üst sınır (0 = sınırsız) alanları açılır. |
| Paket bilgileri kargo şirketine gönderilsin | Hayır | Pazaryeri mağazalarında **kapalı** bırakın — kargo bilgisini pazaryeri kendisi iletir. |
| Eski platform Id | Hayır | Eski sistem köprüsü için; boş = sipariş senkronu kapalı. |
| Aktif | — | Yalnız düzenlemede. Kapalıysa mağaza pasif olur, senkron durur. |
| İptal / Kaydet | — | Kaydet zorunlu alanlar dolana kadar devre dışıdır. |

## Detay sayfası
Rota: `/marketplaces/:id`. Kart tıklamasıyla açılır; "‹ Pazaryerleri" bağlantısı listeye döner. Mağaza bulunamazsa "Mağaza bulunamadı — silinmiş veya pazaryeri mağazası olmayan bir kanal olabilir." mesajı görünür.

![Mağaza detayı — başlık, sağlık satırı ve Genel Bakış sekmesi](img/marketplaces-detay.webp)
1. **Başlık** — logo, mağaza adı, "Pazaryeri · kod · firma", `Aktif`/`Pasif` rozeti; sağda **Siparişler (N)** ve **Senkronize Et ▾** (Ürünleri Gönder… / Stok-Fiyat Güncelle / Siparişleri Çek).
2. **Sağlık satırı** — listedeki sağlık mesajıyla aynı; senkron sonucu burada gösterilir.
3. **Sekmeler** — Genel Bakış · Ürünler (yüklü + yüklenecek sayısı) · Siparişler (açık sipariş sayısı) · Senkron Geçmişi · Sorunlar (açık sorun sayısı) · Ayarlar.

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul |
|---|---|---|---|
| Siparişler (N) | Başlık | Siparişler sekmesine geçer. | — |
| Senkronize Et ▾ → Ürünleri Gönder… | Başlık | Ürünler sekmesini **yüklenecek** filtresiyle açar. | Mağaza aktif |
| Senkronize Et ▾ → Stok-Fiyat Güncelle | Başlık | Yüklü ürünlerin stok/fiyatını iter; sonuç sağlık satırında. | Mağaza aktif + sözleşme |
| Senkronize Et ▾ → Siparişleri Çek | Başlık | Yeni siparişleri sorgular; "Sipariş çekme tamamlandı: N yeni sipariş". | Mağaza aktif + sözleşme |

## Sekmeler

### Genel Bakış
| Öğe | Anlamı |
|---|---|
| Yüklü · Yüklenecek · Bekleyen · Hatalı · Açık Sipariş · Bugün Gelen | Altı sayaç kutusu; tıklayınca ilgili sekmeye (Ürünler / Siparişler) geçer. Yüklenecek sarı, Hatalı kırmızı vurgulanır. |
| Bağlantı kartı | "**Sözleşme bağlı** — servis: `kod` · son senkron X önce" ya da uyarı: "Bu mağazaya bağlı aktif bir pazaryeri sözleşmesi yok. Senkron çalışmaz — firma detayından pazaryeri servisi için sözleşme ekleyin." Altında **Firma sözleşmeleri ↗** bağlantısı firma detayına gider. |
| Son İşlemler | Son 5 senkron kaydı: tarih-saat, işlem (Ürün gönderimi / Stok güncelleme / Sipariş çekme), `Başarılı` / `Hata`. **Tümü →** Senkron Geçmişi'ne geçer. Boşsa "Henüz senkron işlemi yapılmamış." |

### Ürünler
![Ürünler sekmesi — Hazır filtresi, seçim ve Seçilenleri Gönder](img/marketplaces-detay--urun-gonderimi.webp)

Durum çipleri (her biri sayılıdır):
| Çip | Anlamı |
|---|---|
| Yüklü | Pazaryerine gönderilmiş ve kabul edilmiş varyantlar. |
| Hazır | Kanalda açık, henüz gönderilmemiş ve hazırlık denetiminden **sorunsuz** geçmiş ürünler — gönderilebilir. |
| Eksik | Denetimde eksik bulunan **veya henüz denetlenmemiş** ürünler. Satırda neden rozetleri ve **Tamamla** bağlantısı vardır. |
| Bekleyen | Gönderilmiş, sonucu pazaryerinden henüz gelmemiş varyantlar. |
| Hatalı | Pazaryerinin reddettiği varyantlar; satırda hata metni kırmızı görünür. Sayı sıfırdan büyükse çip kırmızı. |
| Pasif | Pazaryerinde pasife alınmış varyantlar. |

| Filtre / buton | Ne yapar |
|---|---|
| Ürün / barkod ara… | Enter ile uygular; ürün adı/kodu/barkod üzerinde arar. |
| Denetle | Yalnız Hazır/Eksik çiplerinde görünür. Pazaryeri için tüm adayların hazırlık denetimini yeniden çalıştırır: "Denetim bitti: N hazır, M eksik (T ürün)." |
| Toplu Tamamla (N) | Eksik çipinde ve seçim varken görünür; seçili ürünler için Tamamla penceresini toplu modda açar. |
| Seçilenleri Gönder (N) | Seçim varken görünür. Yüklü/Bekleyen/Hatalı/Pasif listelerinde seçilen **varyantları**, Hazır/Eksik listelerinde seçilen **ürünleri** (tüm varyantlarıyla) pazaryerine gönderir. |

Tablo sütunları:
| Sütun | Anlamı |
|---|---|
| ☐ | Satır seçimi; başlıktaki kutu sayfadaki tümünü seçer. |
| Ürün | Ürün adı; altında kod · SKU · (adaylarda) varyant sayısı · `PY: dış kimlik`. Hatalı satırda kırmızı hata metni; pazaryeri "beklenen kategori"yi bildirdiyse **→ "X" kategorisine istisna yaz + yeniden gönder** bağlantısı; Eksik satırda en çok 4 neden rozeti (+N). |
| Barkod | Varyant barkodu (`—` yoksa). |
| PY Fiyatı | Pazaryerine en son gönderilen/okunan satış fiyatı. |
| PY Stok | Pazaryerindeki bilinen stok. |
| Durum | Yüklü listelerde `Senkron` / `Bekliyor` / `Hatalı` / `Pasif`; aday listelerde `Hazır` ya da `Eksik` + **Tamamla**. |
| Son Senkron | Son başarılı senkron zamanı. |

Sayfa boyutu 25. Boş durum: adaylarda "Yüklenecek ürün yok — kanalda açık tüm ürünler gönderilmiş.", diğerlerinde "Kayıt bulunamadı."

Gönderim sonucu mesajları:
- Paket takipli pazaryerinde: "N varyant M pakette gönderildi — sonuç arka planda sorgulanıyor (Senkron Geçmişi). Atlanan: a eksik/denetimsiz, b değişmemiş, c barkodsuz."
- Anlık sonuç veren pazaryerinde: "Gönderim bitti: x/y başarılı · z hata — ilk hata metni".
- İstisna bağlantısından: "İstisna yazıldı ve ürün yeniden gönderildi (N varyant)."

Neden rozetleri ve çözümleri:
| Rozet | Çözüm |
|---|---|
| Kategori eşlemesi yok | Eşleştirme → Kategori Eşleme'de ürünün grubunu eşleyin. |
| Kategori ataması bekliyor (havuz) | Grup "Havuz" kipinde eşlenmiş; **Tamamla** ile aday kategorilerden birini ürüne atayın. |
| Hiçbir kategori kuralı tutmadı | "Koşullu" eşlemede varsayılan hedef verin ya da **Tamamla** ile kategori atayın. |
| Kategori eşlemesi kırık | Eşleştirme → Gözden Geçir'den eşlemeyi düzeltin. |
| Zorunlu özellik eksik: X | **Tamamla** ile değeri girin ya da Özellik & Değer sekmesinde X özelliğini bir özelliğinize eşleyin. |
| Değer eşlemesiz: X | Özellik & Değer sekmesinde X için değer eşlemesini tamamlayın ya da **Tamamla** ile ürün-özel değer seçin. |
| Kategori özellikleri henüz indirilmedi (Referans Verisi → Özellikler senkronu) | Referans Verisi penceresinden `Özellikler + Değerler` kapsamını çalıştırın, sonra **Denetle**. |

#### Tamamla penceresi
Başlık "Tamamla: ürün adı" ya da toplu modda "Toplu Tamamla — N ürün". Üstte ürün kodu, grup ve "Çözülen kategori: yol" (çözülemediyse `Kategori çözülemedi` rozeti); altında neden rozetleri ya da `Bu ürün hazır ✓`.

| Alan | Açıklama |
|---|---|
| 1) KATEGORİ | Kategori çözülmemişse zorunlu, toplu modda isteğe bağlı ("mevcut çözümü istisnayla değiştirir"). Havuz eşlemesinde aday kategoriler radyo düğmesi olarak; sistem önerileri "Ad %skor" çipi olarak listelenir; "Veya farklı bir kategori ara…" kutusu en az 2 harfle pazaryeri kategorilerinde arar. |
| 2) EKSİK ZORUNLU ÖZELLİKLER (kategori yolu) | Eksik zorunlu özellik başına bir satır: liste tipi özellikte açılır kutu (`— seç —`), serbest özellikte metin kutusu. "değer eşlemesiz — buradan ürün-özel seçilebilir" notu, genel değer eşlemesi yokken ürüne özel değer girilebileceğini söyler. |
| Kapat | Pencereyi kapatır. |
| Kaydet ve Yeniden Denetle | Girilenleri kaydeder ve ürünü hemen yeniden denetler: "Kaydedildi — N ürün hazır, M üründe hâlâ eksik var." Hiçbir şey girilmediyse devre dışıdır. |

> **Not:** Toplu modda form ilk seçili ürüne göre kurulur; girilenler seçili ürünlerin tümüne uygulanır — aynı gruptaki ürünler için tasarlanmıştır. Buraya girilen değerler kendi kataloğunuza **yazılmaz**, yalnız bu ürünlerin pazaryeri kaydında tutulur. Burada seçilen kategori de genel eşlemeye dokunmaz; yalnız bu ürün için istisna olur.

### Siparişler
Çipler: `Açık` (bekliyor/onaylandı/hazırlanıyor), `Tümü`, `Tamamlanan` (teslim edildi), `İptal/İade`.

| Sütun | Anlamı |
|---|---|
| Sipariş No | Sipariş numarası. |
| Müşteri | Alıcı adı (`—` yoksa). |
| Durum | `Bekliyor` · `Onaylandı` · `Hazırlanıyor` · `Kargoda` · `Teslim Edildi` · `İptal` · `İade`. |
| Tutar | Genel toplam ve para birimi. |
| Tarih | Oluşturulma tarih-saati. |

Satıra tıklayınca sipariş detayı açılır. Sayfa boyutu 20. Boşsa "Bu mağazaya ait sipariş bulunamadı."

### Senkron Geçmişi
**Gönderim Paketleri** bloğu (paket varsa en üstte): pazaryerine paket halinde gönderilen işlerin canlı takibi; açık paket varken 5 saniyede bir yenilenir ve **Şimdi Sorgula** butonu çıkar.
| Sütun | Anlamı |
|---|---|
| Tarih | Paketin gönderildiği an. |
| Tip | `Ürün gönderimi` ya da `Stok-fiyat`; altında pazaryerinin paket numarası. |
| Durum | `Gönderildi` → `Sonuç bekleniyor` → `Tamamlandı` / `Hatalarla bitti` / `Zaman aşımı` / `Başarısız`. |
| Çözülen | "a/b çözüldü" ve varsa "· n hata". |
| Ayrıntı | Paket hatası; yoksa ilk hatalı barkod ve hatası "(+N)"; sonuç beklenirken "sonraki sorgu X içinde". |

İşlem kayıtları: üstte işlem filtresi (`Tüm işlemler` / `Ürün gönderimi` / `Stok güncelleme` / `Sipariş çekme`).
| Sütun | Anlamı |
|---|---|
| Zaman | Kayıt zamanı. |
| İşlem | İşlem türü (mutabakat koşuları `reconcile` adıyla listelenir). |
| Sonuç | `Başarılı` / `Hata` ve hata mesajı. |
| Süre | İşlem süresi (ms / sn). |

Sayfa boyutu 30. Boşsa "Henüz senkron kaydı yok."

### Sorunlar
Mutabakat ve gönderim sonuçlarından **otomatik açılan**, koşul ortadan kalkınca **kendiliğinden kapanan** kuyruk.
| Sütun | Anlamı |
|---|---|
| Tip rozeti | `Fiyat sapması` · `Stok sapması` · `Pazaryerinde yok` · `Paket zaman aşımı` · `Gönderim hatası` · `Bizde kayıtsız`. |
| Başlık / ayrıntı / → önerilen aksiyon | Ne olduğu, ayrıntısı ve yeşil renkte ne yapmanız gerektiği. |
| Son görülme | Koşulun en son ne zaman görüldüğü. |
| Yoksay | Sorunu kapatır. Koşul sürüyorsa sonraki taramada **yeniden açılır** (bilinçli). |

Örnekler: "Ürün pazaryerinde bulunamadı: barkod" → "Ürünü Hazır listesinden yeniden gönderin." · "Fiyat sapması %X: barkod" → "Pazaryerindeki fark kampanya/elle müdahale olabilir — doğruysa yoksayın, değilse Stok-Fiyat Güncelle çalıştırın." · "Gönderim paketi zaman aşımına uğradı (N satır)" → "Mutabakat çalıştırın: pazaryerindeki fiili durum item'ları çözer." Boşsa "Açık sorun yok — her şey yolunda."

### Ayarlar
Solda **Mağaza Ayarları** (yukarıdaki mağaza formu, düzenleme modu). Sağda **Pazaryeri Sözleşmesi** kartı: "API kimlik bilgileri (şifreli) firma sözleşmesinde tutulur. Senkron işlemleri bu sözleşme üzerinden çalışır." Durum: `✓ Aktif sözleşme bağlı (servis kodu)` ya da `⚠ Aktif sözleşme yok — senkron devre dışı.` **Firma sözleşmelerine git ↗** firma detayının Entegrasyonlar bölümüne götürür.

## Durumlar ve iş kuralları
- **Bağlantı:** Mağaza, aynı firmanın pazaryeri servisine bağlı **aktif sözleşmesi** (Firma detayı → Entegrasyonlar → Entegrasyon Ekle) üzerinden çalışır. Trendyol sözleşmesinde Satıcı ID, API Key, API Secret, Marka ID ve Kargo Firma ID zorunludur; Hepsiburada'da Merchant ID + API kullanıcı adı/şifresi; n11'de App Key/Secret; Amazon'da Seller ID, Marketplace ID, Access/Secret Key (+ Refresh Token). Sözleşme yoksa tüm senkron işlemleri "Bu mağaza için aktif pazaryeri bağlantısı (sözleşme) yok." hatasıyla döner.
- **Varyant akışı:** `Hazır` → (gönderim) → `Bekliyor` → `Senkron` ya da `Hatalı`; pazaryerinde pasife alınan `Pasif`. Yüklenecek ürün = kanalda satışa açık olup gönderilmiş varyantı olmayan ürün.
- **Hazırlık denetimi:** Ürün ancak kategori çözülmüş (istisna > koşullu kural > birebir eşleme) ve kategorinin zorunlu özellikleri (varyant ekseni olanlar hariç) doldurulmuşsa **Hazır** olur. Özellik değeri kaynağı öncelik sırası: ürün-özel pazaryeri değeri > değer eşlemesi > sabit değer > serbest geçirme. Eşleme ya da tamamlama değişince ilgili ürünler yeniden denetlenir.
- **Ürün gönderimi (paket takipli pazaryeri — bugün Trendyol):** yalnız **Hazır** ürünler gider; içeriği/fiyatı/stoğu değişmemiş yüklü varyant ve barkodsuz varyant atlanır; en çok 100'lük paketler halinde gönderilir; sonuç arka planda 1 → 2 → 5 → 10 → 30 dk aralıklarla sorgulanır; 24 saatte sonuç alınamazsa paket `Zaman aşımı` olur ve "Paket zaman aşımı" sorunu açılır — bu satırlar körlemesine yeniden gönderilmez, mutabakat pazaryerindeki fiili durumu okuyarak çözer. Anlık sonuç veren pazaryerlerinde tek istekte en çok 200 varyant gönderilir.
- **Stok-Fiyat Güncelle:** yalnız yüklü (`Senkron`) varyantlar; bilinen pazaryeri fiyat/stoğuyla aynı olanlar atlanır; paket takipli pazaryerinde 500'lük paketlerle gider ve Senkron Geçmişi'nde `Stok-fiyat` paketi olarak izlenir; anlık pazaryerlerinde tek istekte en çok 500 varyant.
- **Mutabakat:** pazaryeri listesi sayfa sayfa çekilir. Stok farkı **her zaman** otomatik düzeltilir (bizim veri kazanır); fiyat farkı eşiğin (varsayılan %10) altındaysa otomatik düzeltilir, üstündeyse "Fiyat sapması" sorunu açılır (kampanya/elle müdahale olabilir — körlemesine ezilmez); bizde yüklü görünüp pazaryerinde olmayan ürün için "Pazaryerinde yok" sorunu açılır ve ürün yeniden gönderime açılır; pazaryerindeki fiili kategori farklıysa ürün için kategori istisnası olarak kaydedilir (personelin elle yazdığı istisnanın üzerine yazılmaz). Mutabakat desteği olmayan pazaryerinde "… için mutabakat desteği henüz yok." hatası döner.
- **Sorun kuyruğu:** aynı koşul için ikinci sorun açılmaz; koşul kalkınca otomatik kapanır; Yoksay geçicidir.
- **Zamanlama:** Paket sonuçlarının sorgulanması otomatiktir. Mutabakat ve stok-fiyat senkronu için zamanlanmış otomatik çalışma yoktur — karttan/detaydan elle tetiklenir.
- **Yetki:** Sayfa giriş yapmış panel kullanıcılarına açıktır; ayrı bir izin aranmaz.

## Adım adım

**1. Yeni pazaryeri mağazası bağlama**
1. **Yeni Mağaza** → firma (gerekirse), platform tipi (örn. Trendyol), kanal adı girin → **Kaydet**.
2. Mağaza kartına tıklayın → **Ayarlar** → **Firma sözleşmelerine git ↗** (ya da Genel Bakış → Firma sözleşmeleri).
3. Firma detayında **Entegrasyonlar → Entegrasyon Ekle**: pazaryeri servisini seçip API kimliklerini ve zorunlu ayarları (Trendyol'da Marka ID, Kargo Firma ID) girin, sözleşmeyi aktif bırakın.
4. Pazaryerleri listesine dönün: kartın sağlık satırı "Bağlantı hazır — henüz senkron yapılmadı" olmalıdır.

**2. İlk ürün gönderimi**
1. **Referans Verisi** → pazaryeri seçin → `Kategoriler` → **Senkronu Başlat**; bitince `Özellikler + Değerler` ile tekrarlayın.
2. **Eşleştirme** sayfasında ürün gruplarınızı ve özelliklerinizi eşleyin (bkz. [Kategori ve Özellik Eşleştirme](/rehber/siparis/kategori-ve-ozellik-eslestirme/)).
3. Mağaza detayı → **Ürünler** → **Hazır** çipi → **Denetle**.
4. **Eksik** çipine geçin; satırdaki **Tamamla** (ya da birden çok seçip **Toplu Tamamla**) ile kategori/özellik eksiklerini kapatın → **Kaydet ve Yeniden Denetle**.
5. **Hazır** çipinde ürünleri seçin → **Seçilenleri Gönder (N)**.
6. **Senkron Geçmişi** → Gönderim Paketleri'nde paketin `Tamamlandı` olmasını bekleyin; hatalı satırlar **Hatalı** çipine düşer.

**3. Pazaryerinin kategori nedeniyle reddettiği ürünü düzeltme**
1. **Ürünler → Hatalı** çipinde ürünün kırmızı hata metnini okuyun.
2. Hata pazaryerinin beklediği kategoriyi içeriyorsa altta çıkan **→ "X" kategorisine istisna yaz + yeniden gönder** bağlantısına tıklayın; ürün için istisna yazılır (genel eşleme değişmez) ve yeniden gönderilir.
3. Yeni kategorinin zorunlu özellikleri eksikse ürün **Eksik**'e düşer → **Tamamla** ile doldurup tekrar gönderin.

**4. Mutabakat ve sorun çözme**
1. Kartta **Senkron ▾ → Mutabakat Çalıştır** (ya da detay sağlık satırında sonucu izleyin).
2. Detay → **Sorunlar**: her satırdaki "→ önerilen aksiyon"u uygulayın (yeniden gönder, Stok-Fiyat Güncelle, …).
3. Bilinçli fark ise **Yoksay**.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Kartta **HATALI** sayısı görünüyorsa Hatalı filtresine, sarı "açık sorun" mesajı görünüyorsa Sorunlar sekmesine gidin — ikisi farklı şeylerdir: hata tek bir gönderimin sonucu, sorun ise mutabakat/paket takibinin bulgusudur.

> **Dikkat:** "Seçilenleri Gönder" Eksik listesinde de görünür; ancak eksik/denetlenmemiş ürünler gönderime girmez ve sonuç mesajında "Atlanan: N eksik/denetimsiz" olarak raporlanır. Önce tamamlayıp denetleyin.

> **Dikkat:** Mağazayı **Aktif** işaretini kaldırarak pasife alırsanız tüm senkron menüleri devre dışı kalır ve kart gri olur; yüklü ürünler pazaryerinde silinmez.

> **Not:** "Fiyat-stok gönderildi: 0 varyant (N değişmemiş atlandı)" normaldir — pazaryerindeki bilinen değerler bizimkilerle aynıdır.

> **Not:** "Tek istekte en fazla 200 varyant gönderilebilir" hatası anlık sonuç veren pazaryerlerinde çıkar; seçimi küçültüp parça parça gönderin.

## İlgili sayfalar
- [Kategori ve Özellik Eşleştirme](/rehber/siparis/kategori-ve-ozellik-eslestirme/)
- [Komisyon Yönetimi](/rehber/cari/komisyon-yonetimi/)
- [Siparişler](/rehber/siparis/siparisler/)
- [Firmalar ve Sözleşmeler](/rehber/sistem/firmalar/)
- [Kanallar](/rehber/sistem/satis-kanallari/)
