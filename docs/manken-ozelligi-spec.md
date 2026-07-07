# Manken Özelliği - Uygulama Spesifikasyonu

## Bağlam / Problem

E-ticarete konu ürünlerin %1'inden azında manken bilgisi izleme ihtiyacı var. Bu nadir
ihtiyaç için ayrı, ilişkisel olarak ürün/varyant şemasına sıkı bağlı bir yapı (örn.
`product_mannequin` ilişki tablosu, zorunlu FK zincirleri) kurmak orantısız bir
karmaşıklık yaratır. Bunun yerine mevcut ürün özellik (varyant özelliği) altyapısı
yeniden kullanılacak; manken bilgisi arşivi için de organik/FK bağı olmayan ayrı,
hafif bir tablo tutulacak.

## Genel Yaklaşım

1. Mevcut "ürün özelliği" mekanizmasına yeni bir özellik eklenir: **Manken**.
   - Bu özellik, tekstil ürünlerinde renk gibi ana varyant seviyesine eklenir.
   - Özellik yeni SKU/varyant kombinasyonu **üretmez** — sadece bilgilendirici bir
     alandır (renk gibi "varyant üretici" değil).
2. Manken özelliğinin değeri **JSON dizisi** olarak tutulur (serbest metin değil).
3. Ayrı, ürün/varyant şemasına FK ile bağlı olmayan bir **manken arşiv tablosu**
   oluşturulur (sadece manken bilgilerini tutan referans/master data tablosu).
4. Ürün resmi yükleme ekranında, kullanıcı manken tablosundan seçim yapar; seçilen
   bilgiler otomatik olarak JSON yapısına dönüştürülüp özellik değerine yazılır
   (manuel serbest metin girişini önlemek, tutarlılığı artırmak için).
5. Manken tablosundaki bilgi, özellik değerine **snapshot (o anki hâliyle donmuş
   kopya)** olarak yazılır. Manken tablosunda sonradan yapılan güncellemeler,
   geçmişte oluşturulmuş ürün kayıtlarındaki metni **etkilemez**.
6. Sistem genelinde geçerli olan kural: **hiçbir veri fiziksel olarak silinmez,
   tüm silme işlemleri soft-delete'tir.** Bu, manken tablosu için de geçerlidir ve
   JSON içine gömülü `mankenId` referanslarının hiçbir zaman "yetim" kalmamasını
   garanti eder — bu nedenle FK constraint zorunluluğu yoktur.

## Veri Modeli

### 1. Manken Tablosu (yeni, bağımsız/arşiv tablo)

Ürün/varyant şemasına organik (FK) bağı yoktur. Sadece manken bilgilerinin
merkezi kaydını tutar.

Önerilen alanlar:
- `id` (PK)
- `ad` (manken adı/kodu)
- `boy` (örn. cm)
- `kilo` (örn. kg)
- `beden` (örn. S, M, L)
- `aktif` (boolean — soft-delete durumu; sistem genel soft-delete kuralına uygun)
- `olusturmaTarihi`, `guncellemeTarihi`

> Not: Alan isimleri/tipleri projenin mevcut konvansiyonuna göre uyarlanabilir.
> Önemli olan: bu tablo bağımsız, hafif bir referans/lookup tablosudur.

### 2. Ürün Özelliği: "Manken"

Mevcut özellik altyapısı üzerinden tanımlanır (renk gibi ana varyanta eklenen
bir özellik). Değer tipi: **JSON**.

JSON yapısı:

```json
[
  {
    "mankenId": "abc",
    "aciklama": "ön ve arka görünümler",
    "mankenDetay": "S beden, 45 kg"
  }
]
```

Alan açıklamaları:
- `mankenId`: Manken tablosundaki kaydın ID'si (FK constraint YOK, sadece
  uygulama seviyesinde tutarlı bir referans).
- `aciklama`: Bu manken bilgisinin hangi görünüm(ler) için geçerli olduğu
  (örn. "ön ve arka görünümler", "detay çekimi"). Nadiren birden fazla manken
  kullanılan durumlar için vardır (aynı renk varyantının farklı fotoğraflarında
  farklı manken kullanılması — çok nadir bir senaryo).
- `mankenDetay`: Manken tablosundaki bilgilerin (boy, kilo, beden vb.) o anki
  hâliyle donmuş (snapshot) metin kopyası. Manken tablosu sonradan güncellense
  bile bu alan değişmez.

Çoğu üründe dizi tek elemanlı olacaktır; birden fazla manken kullanımı istisnai
bir durumdur ve şema buna zaten izin verir (ek bir migration gerekmez).

### 3. Uygulama Seviyesi Validasyon

Veritabanı JSON alanının iç yapısını zorlamayacağı için, kayıt anında uygulama
tarafında validasyon eklenmelidir:
- `mankenId` zorunlu ve manken tablosunda mevcut (aktif ya da pasif) bir kayda
  karşılık gelmeli.
- `mankenDetay` zorunlu, string.
- `aciklama` tercihen sabit bir seçim listesinden gelmeli (örn. "ön görünüm",
  "arka görünüm", "detay", "yandan") — serbest metin yazım tutarsızlığını
  (örn. "ön-arka" vs "Ön ve Arka Görünüm") önlemek için.
- Kod, JSON'da gelecekte eklenebilecek eksik/yeni alanlara karşı toleranslı
  yazılmalı (geriye dönük uyumluluk).

## Resim Yükleme Akışı

Ürün resmi yüklenirken:
1. Kullanıcıya manken tablosundan (aktif kayıtlar) bir seçim listesi sunulur.
2. Kullanıcı bir veya birden fazla manken seçer, her biri için görünüm
   açıklaması (`aciklama`) seçer.
3. Seçimler otomatik olarak yukarıdaki JSON formatına dönüştürülür ve ilgili
   varyantın Manken özellik değerine yazılır (serbest metin girişi yoktur,
   tutarlılık için tamamen seçim tabanlıdır).

## Raporlama

İki temel rapor senaryosu, karmaşık JOIN gerektirmeden, **iki adımlı sorgu**
deseniyle karşılanır:

### Rapor 1: Tarih Aralığına Göre Manken Kullanım Özeti
1. Belirtilen tarih aralığında çekilmiş ürün resimlerinin ait olduğu ürün
   varyantları filtrelenir.
2. Bu varyantların Manken özellik değerlerindeki JSON'lardan `mankenId`'ler
   çıkarılır (DB'nin JSON fonksiyonlarıyla, örn. PostgreSQL'de
   `jsonb_array_elements`).
3. `mankenId` başına **distinct ürün sayısı** hesaplanır (bkz. "Sayım Tanımı"
   aşağıda).
4. Elde edilen `mankenId` listesi, ikinci adımda manken tablosuna ayrı bir
   sorgu ile detaylandırılır (ad, boy, kilo, beden vb.).

### Rapor 2: "Bu Mankenin Ürünleri" Raporu
1. Manken tablosundan bir `mankenId` seçilir.
2. Ürün özellik değerleri tablosunda, JSON içinde bu `mankenId`'yi içeren
   kayıtlar sorgulanır.
3. Sonuç, ilgili ürün/varyant listesi olarak döner.

### Sayım Tanımı (önemli)
"Mankenin görev aldığı ürün sayısı" = **distinct ürün sayısı**.
- Aynı manken bir ürünün birden fazla fotoğrafında (ön/arka) geçse bile 1 ürün
  olarak sayılır.
- JSON'dan `mankenId` çıkarma işlemi satır patlamasına (fan-out) yol açabileceği
  için, sorguda mutlaka `COUNT(DISTINCT ürünId)` kullanılmalı, `COUNT(*)`
  **kullanılmamalıdır** — aksi hâlde bir üründe aynı manken birden fazla kez
  geçtiğinde sayım yanlış şişer.
- Ürün mü yoksa ürün-varyant mı sayılacağına (örn. bir tişörtün kırmızısı ve
  mavisi ayrı mı sayılsın) karar verilmeli; varsayılan olarak **ürün bazında**
  sayım öneriliyor.

## Performans Notları
- JSON alanından `mankenId` sorgulaması sık kullanılacaksa (özellikle Rapor 2
  için), veritabanı motoruna uygun bir JSON index eklenmesi değerlendirilmeli
  (örn. PostgreSQL'de JSONB + GIN index).
- Gruplama/sayma işleminin veritabanı tarafında mı (DB'nin JSON fonksiyonları)
  yoksa uygulama tarafında mı yapılacağı, veri hacmine göre belirlenmeli;
  büyük veri hacminde DB tarafında yapılması önerilir.

## Silme Politikası
- Sistem genelinde tüm silme işlemleri **soft-delete**'tir (bu proje için zaten
  genel bir kural).
- Manken tablosunda bir kayıt "silindiğinde" fiziksel olarak kaldırılmaz,
  yalnızca `aktif = false` yapılır.
- Bu sayede JSON içindeki `mankenId` referansları her zaman çözülebilir kalır,
  ekstra bir referans bütünlüğü önlemine gerek yoktur.

## Yapılacaklar (Implementation Checklist)

- [ ] Manken tablosu migration'ı (bağımsız tablo, ürün/varyant şemasına FK yok)
- [ ] Manken tablosu için CRUD (ekle/düzenle/soft-delete/listele) ekranı veya API
- [ ] Mevcut ürün özellik altyapısına "Manken" özelliğinin tanımlanması
      (varyant üretici değil, bilgilendirici özellik olarak)
- [ ] Manken özellik değeri için JSON şema validasyonu (uygulama seviyesinde)
- [ ] Resim yükleme ekranına manken seçim adımının eklenmesi (seçimden JSON
      üretimi)
- [ ] `aciklama` alanı için sabit seçim listesi (enum) tanımlanması
- [ ] Rapor 1: Tarih aralığı → manken kullanım özeti sorgusu
- [ ] Rapor 2: Manken → ürün listesi sorgusu
- [ ] JSON alanı için gerekiyorsa DB index'i (performansa göre)
