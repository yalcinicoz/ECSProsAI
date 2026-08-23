---
title: Özellik Tipleri
route: /catalog/attribute-types
group: Katalog
order: 40
summary: Ürün ve varyantları tanımlayan özelliklerin (renk, beden, kumaş, marka…) ve bu özelliklerin seçim değerlerinin merkezi olarak yönetildiği ekran.
---

## Ne işe yarar
Özellik tipleri, kataloğun ortak "özellik havuzu"dur: renk, beden, kumaş türü, marka, cinsiyet gibi ürünü ya da varyantı
tanımlayan her nitelik burada bir kez tanımlanır. Ürün grupları bu havuzdan özellik seçerek kendi şablonlarını kurar;
ürün kartlarında girilen değerler de buradan gelir. Seçim listesi tipindeki özelliklerin değerleri (örn. Beden → S, M, L)
yine bu ekranda, özellik tipinin detayında yönetilir.

Bu ekranı katalog yapısını kuran yöneticiler kullanır: yeni bir özelliğe ihtiyaç duyulduğunda (örn. "Kapasite"), bir
özelliğin mağaza filtrelerinde görünmesi istendiğinde ya da bir seçim değerinin adı/sırası/rengi düzenleneceğinde.

> **Dikkat:** Özellik tipleri ve değerleri **platform seviyesinde** tanımdır. Ekleme/düzenleme için `catalog.platform.manage`
> yetkisi gerekir. Bu yetki yoksa sayfa başlığının yanında **Salt Okunur** rozeti görünür; liste ve detay görüntülenir,
> ancak hiçbir buton gösterilmez.

## Ekran yerleşimi
![Özellik Tipleri listesi — üst araç çubuğu, Tümü/Aktif anahtarı ve özellik tablosu](img/catalog-attribute-types.webp)
1. **Başlık ve kayıt sayısı** — "Özellik Tipleri" başlığı, yanında (yetki yoksa) Salt Okunur rozeti, altında toplam kayıt sayısı.
2. **Sağ üst araç çubuğu** — `Tümü` / `Aktif` anahtarı ve **Yeni Özellik Tipi** butonu.
3. **Tablo** — tüm özellik tipleri; satıra tıklayınca detay sayfası açılır.

![Özellik Tipi detayı — Özellik Ayarları kartı ve Değerler tablosu](img/catalog-attribute-types-detay.webp)
1. **Kırıntı ve başlık** — `Özellik Tipleri › <kod>`; başlıkta Türkçe ad, altında kod rozeti, veri tipi rozeti, Aktif/Pasif rozeti ve değer sayısı.
2. **Özellik Ayarları kartı** — veri tipi, sıra, filtre ve durum ile çok dilli ad; sağ üstte **Kaydet**.
3. **Değerler kartı** — yalnız *Seçim Listesi* ve *Çoklu Seçim* tiplerinde görünür; **Değer Ekle** butonu ve değer tablosu.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| Kod | Özelliğin sistem kodu (örn. `beden`, `filtre_rengi`). Türkçe addan otomatik üretilir, sonradan değişmez. |
| Ad | Türkçe ad (Türkçe yoksa girilen ilk dil). |
| Veri Tipi | Rozet: `Seçim Listesi`, `Çoklu Seçim`, `Metin`, `Sayı`, `Evet/Hayır`. |
| Değer Sayısı | Bu özelliğe tanımlı seçim değerlerinin sayısı (metin/sayı tiplerinde 0). |
| Sıra | Mağaza filtre alanındaki gösterim sırası; küçük değer üstte. |
| Filtrede | `Evet` (yeşil) = mağaza ürün listesi filtrelerinde gösterilir; `Hayır` = filtre dışı. |
| Durum | `Aktif` / `Pasif`. |
| › | Satırın detaya açıldığını gösteren ok. |

| Filtre | Ne yapar |
|---|---|
| `Tümü` / `Aktif` anahtarı | `Aktif` seçiliyken yalnız aktif özellik tipleri listelenir; `Tümü` pasifleri de gösterir. Varsayılan `Tümü`. |

- Listede arama kutusu ve sayfalama yoktur; tüm kayıtlar tek sayfada, sunucudan geldiği sırada listelenir.
- **Satıra tıklayınca** o özellik tipinin detay sayfası açılır (`/catalog/attribute-types/<id>`).
- Hiç kayıt yoksa tabloda "Özellik tipi bulunamadı" yazısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Özellik Tipi | Liste, sağ üst | "Yeni Özellik Tipi" penceresi açılır; Kaydet sonrası doğrudan yeni kaydın detay sayfasına gidilir. | `catalog.platform.manage` |
| Kaydet (Özellik Ayarları) | Detay, ayar kartının sağ üstü | Veri tipi, sıra, filtre, durum ve ad çevirileri kaydedilir; "Kaydedildi" yazısı 2-3 sn görünür. Hiçbir alan değişmediyse buton pasiftir. | `catalog.platform.manage` |
| Değer Ekle | Detay, Değerler kartı | "Değer Ekle" penceresi açılır; sıra alanı otomatik olarak (mevcut değer sayısı × 10) ile dolu gelir. | `catalog.platform.manage`; yalnız Seçim Listesi / Çoklu Seçim tiplerinde |
| `N ürün` rozeti | Değer satırı, Kullanım sütunu | O değeri kullanan ürünlerin listesi açılır (ad, kod, "Ürün özelliği" ya da "Varyant" etiketi, Aktif/Pasif). Bir ürüne tıklayınca ürün kartına gidilir. | Herkes (kullanım > 0 ise) |
| Düzenle (kalem) | Değer satırı, sağ | "Değer Düzenle" penceresi açılır. | `catalog.platform.manage` |
| Sil (çöp kutusu) | Değer satırı, sağ | ⚠️ "Değeri Sil" onayı açılır; onaylanınca değer silinir. Buton **yalnız hiç üründe kullanılmayan** değerlerde görünür. | `catalog.platform.manage`; Kullanım = 0 |
| Geri Dön | Detay (kayıt bulunamadığında) | Listeye döner. | — |

## Form alanları

### Yeni Özellik Tipi penceresi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Veri Tipi | Evet | `Seçim Listesi` (varsayılan), `Çoklu Seçim`, `Metin`, `Sayı`, `Evet/Hayır`. Seçim tipleri değer havuzuna sahiptir; diğerlerinde değer girilmez, ürün kartında serbest girilir. |
| Sıra | Hayır | Tam sayı; filtre alanındaki gösterim sırası (küçük değer üstte). Varsayılan 0. |
| Filtrede kullanılsın | Hayır | İşaretliyse bu özellik mağaza ürün listesi filtrelerinde gösterilir. Varsayılan işaretsiz. |
| Ad | Evet (kaynak dil) | Çok dilli alan — dil sekmeleri (TR/EN…). Türkçe ad koddan üretim için de kullanılır. |
| Otomatik Kod | — (salt okunur) | Türkçe addan canlı üretilen ön izleme (örn. "Kumaş Türü" → `kumas_turu`). Kayıt sonrası değiştirilemez; aynı kod zaten varsa sonuna `_2`, `_3`… eklenir. |

Kaydet butonu, ad girilip geçerli bir kod üretilene kadar pasiftir. Hata olursa pencerede "Hata oluştu. Lütfen tekrar deneyin." görünür.

### Özellik Ayarları kartı (detay)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Veri Tipi | Evet | Açılır liste; sonradan değiştirilebilir. Seçim tipinden metin/sayıya geçilirse Değerler kartı gizlenir (değerler silinmez). |
| Sıra | Hayır | Tam sayı; filtre sırası. |
| Filtre → Filtrede kullanılsın | Hayır | Onay kutusu. Salt okunur görünümde `Filtrede` / `Filtre dışı` rozeti olarak gösterilir. |
| Durum → Aktif | Hayır | Onay kutusu; kaldırılırsa özellik pasife alınır (listede `Pasif`). |
| Ad (Çeviriler) | Evet (kaynak dil) | Çok dilli ad. |

### Değer Ekle / Değer Düzenle pencereleri
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Sıra | Hayır | Tam sayı; değerlerin ürün kartı ve filtredeki sırası. Eklemede otomatik önerilir. |
| Durum → Aktif | Hayır | Yalnız düzenlemede. Pasif değer yeni seçimlerde sunulmaz. |
| Ad | Evet (en az bir dil) | Çok dilli; **otomatik BÜYÜK HARFE çevrilir** (örn. "Kırmızı" → "KIRMIZI"). Aynı dilde aynı ad zaten varsa turuncu uyarı çıkar: `"X" bu özellik tipinde zaten mevcut.` ve Kaydet pasifleşir. |
| Hex Kodu | Hayır | **Yalnız kodu `filtre_rengi` olan özellik tipinde** görünür. `#RRGGBB` biçiminde renk kodu (örn. `#FF0000`); girildikçe yanında renk yuvarlağı ön izlenir. Mağaza renk filtresindeki renk kutucuğu buradan gelir. |

Kaydet, en az bir dilde ad girilene kadar pasiftir. Sunucudan dönen hata (örn. "Bu değer zaten mevcut") pencerenin altında kırmızı yazıyla gösterilir.

## Detay sayfası — Değerler tablosu
| Sütun | Anlamı |
|---|---|
| Ad | Değerin Türkçe adı (büyük harf). |
| Renk | Yalnız `filtre_rengi` tipinde: renk yuvarlağı + hex kodu; tanımsızsa `—`. |
| Kullanım | Değeri kullanan ürün sayısı (`N ürün` rozeti, tıklanabilir); kullanılmıyorsa `—`. |
| Sıra | Sıra numarası. |
| Durum | `Aktif` / `Pasif`. |
| (işlemler) | Düzenle ve (kullanım 0 ise) Sil butonları — yalnız yetkili kullanıcıda. |

Metin, Sayı ve Evet/Hayır tiplerinde Değerler kartı hiç görünmez. Değer yoksa "Henüz değer eklenmemiş" yazısı görünür.

## Durumlar ve iş kuralları
- **Aktif / Pasif (özellik tipi):** Pasif tip listede `Tümü` seçiliyken görünür, `Aktif` seçiliyken gizlenir. Pasife alma silme değildir; mevcut ürün verisi korunur.
- **Aktif / Pasif (değer):** Pasif değer mevcut ürünlerde kalır, yeni seçimlerde sunulmaz.
- **Kod değişmez:** Kod Türkçe addan bir kez üretilir; ad sonradan değiştirilse bile kod aynı kalır. Çakışmada `_2`, `_3`… son eki eklenir.
- **Filtrede kullanılsın:** Mağaza ürün listesindeki filtre grupları yalnız bu işareti taşıyan tiplerden oluşur; grupların sırası **Sıra** alanındandır (örn. cinsiyet 10 › marka 20 › beden 30 › filtre rengi 40 …). Tek seçeneği kalan filtre grubu mağazada gösterilmez.
- **Renk filtresi:** Mağazadaki renk kutucuklu filtre `filtre_rengi` tipinin değerleri ve Hex Kodu alanıyla çalışır. Ham "renk" ekseni filtrede kullanılmaz; ürünlerin renkleri bu kürasyonlu renk değerlerine eşlenir.
- **Değer silme:** Yalnız hiçbir üründe kullanılmayan değer silinebilir; kullanılan değer için Sil butonu görünmez, sunucu da "Bu değer ürünlerde kullanılıyor; silinemiyor." hatası verir. Silme geri alınamaz ⚠️.
- **Mükerrer değer:** Aynı dilde aynı ad (büyük/küçük harf farkı gözetilmeden) ikinci kez eklenemez.
- **Veri tipi bir tip özelliğidir, varyant ekseni olup olmadığı ise grup özelliğidir:** Aynı özellik (örn. Renk) bir ürün grubunda varyant ekseni, başka bir grupta sıradan ürün özelliği olabilir. Bu ayar [Ürün Grupları](/rehber/katalog/urun-gruplari/) sayfasında yapılır.

## Adım adım

### Yeni özellik tipi oluşturma
1. Sol menüden **Katalog › Özellik Tipleri**'ne girin.
2. Sağ üstte **Yeni Özellik Tipi**'ne tıklayın.
3. **Veri Tipi**'ni seçin (seçim listesi olacaksa `Seçim Listesi` ya da `Çoklu Seçim`).
4. Gerekirse **Sıra** girin ve mağaza filtresinde görünecekse **Filtrede kullanılsın**'ı işaretleyin.
5. **Ad** alanına Türkçe adı (ve varsa diğer dilleri) yazın; **Otomatik Kod** ön izlemesini kontrol edin.
6. **Kaydet**'e tıklayın — yeni kaydın detay sayfası açılır.

### Seçim değerleri ekleme
1. Detay sayfasında **Değerler** kartında **Değer Ekle**'ye tıklayın.
2. **Ad**'ı yazın (otomatik büyük harfe dönüşür); gerekirse **Sıra**'yı düzenleyin.
3. Tip `filtre_rengi` ise **Hex Kodu** girin (örn. `#1E90FF`).
4. **Kaydet**'e tıklayın; aynı ad varsa uyarı alırsınız.

### Bir özelliği mağaza filtresine açma / kapatma
1. Listede ilgili satıra tıklayıp detaya girin.
2. **Özellik Ayarları** kartında **Filtrede kullanılsın** kutusunu işaretleyin veya kaldırın; gerekirse **Sıra** verin.
3. **Kaydet**'e tıklayın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Değer ekleme penceresinde Sıra, mevcut değer sayısının 10 katı olarak önerilir (0, 10, 20…). Aralık bırakmak sonradan araya değer sokmayı kolaylaştırır.

> **İpucu:** Bir değerin hangi ürünlerde kullanıldığını görmek için Kullanım sütunundaki `N ürün` rozetine tıklayın; "Varyant" etiketi değerin varyant ekseni olarak, "Ürün özelliği" etiketi ürün düzeyinde kullanıldığını gösterir.

> **Dikkat:** Sil butonunu göremiyorsanız değer en az bir üründe kullanılıyordur. Önce ürünlerden kaldırmanız ya da değeri **Pasif** yapmanız gerekir.

> **Dikkat:** "Özellik tipi bulunamadı." yazısı görüyorsanız adresteki kayıt silinmiş ya da yanlıştır; **Geri Dön** ile listeye dönün.

> **Not:** Yetkiniz yoksa (Salt Okunur) butonlar görünmez; filtre ve durum alanları rozet olarak gösterilir. Yetki için sistem yöneticinize başvurun.

## İlgili sayfalar
- [Ürün Grupları](/rehber/katalog/urun-gruplari/) — özelliklerin gruplara atanması, varyant ekseni / ana eksen
- [Ürün Kartları](/rehber/katalog/urun-kartlari/) — değerlerin ürün ve varyantlara girilmesi
- [Katalog Ayarları](/rehber/katalog/katalog-ayarlari/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/) — çok dilli alanlar ve ortak kalıplar
