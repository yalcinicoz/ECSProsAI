---
title: Kanal Ürünleri
route: /storefront/channel-products
group: Vitrin
order: 40
summary: Ürünlerin seçili satış kanalında satılıp satılmayacağının (kanala al / çıkar) ve satışın anlık ya da tarih penceresiyle durdurulmasının tek tek veya toplu yönetildiği ekran.
---

## Ne işe yarar
Kataloğunuzdaki ürünlerin **hangi kanalda satılacağını** belirler. Ürünler varsayılan olarak kanaldadır; bu
ekrandan bir ürünü kanaldan çıkarabilir (sitede hiç görünmez, adresi ana sayfaya yönlenir) ya da satışını belirli
bir tarih penceresi için durdurabilirsiniz (stok/tedarik sorununda geçici kapatma). Katalog/satış sorumlusu
sezon kapanışında, tedarik kesildiğinde veya kanal bazlı ürün seçimi yaparken kullanır. Kanal ürünlerine tarih
aralıklı "öne çıkar (Sponsorlu)" bayrağı ise ürün kartı detay sayfasından verilir; bu ekranda yoktur.

## Ekran yerleşimi
![Kanal Ürünleri — kanal seçici, filtre çubuğu ve ürün tablosu](img/storefront-channel-products.webp)
1. **Başlık** ve açıklama.
2. **Satış Kanalı seçici** — aranabilir liste ("Kanal (Firma)"); seçim oturumda hatırlanır.
3. **Filtre çubuğu** — arama kutusu + **Ara**, Durum açılır listesi.
4. **Toplu işlem çubuğu** — yalnız en az bir satır seçiliyken görünür.
5. **Ürün tablosu** ve altta sayfalama (30 satır/sayfa).

Filtre çubuğunun üstündeki **Listeleme özeti** satırı kanalın tamamı için durum ve en sık sebep sayılarını
gösterir (örn. `Yayında 5.792` · `Engelli 11.309` · "Kanal stoğu yok: 10.972"). Sayılar birkaç dakikalık
önbellekten gelir; sebebe göre filtreleme ileride eklenecektir.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| (onay kutusu) | Satır seçimi; başlıktaki kutu sayfadaki tüm satırları seçer/bırakır. |
| (görsel) | Ürünün ana görseli. |
| Ürün | Ürün adı ve altında kodu. |
| Kanal Durumu | `Kanalda` (yeşil), `Durduruldu — gg.aa.yyyy kadar` (sarı; bitiş yoksa yalnız `Durduruldu`), `Kanaldan çıkarıldı` (gri). |
| Listeleme | Ürünün bu kanalda **fiilen** yayında/satışta olup olmadığı: `Yayında` (yeşil) · `Hazır` (mavi, pazaryerine yüklenebilir) · `Bekliyor` (yükleme kuyruğunda) · `Eksik bilgi` (sarı) · `Engelli` (gri; çıkarıldı/durduruldu/satış kapalı/stok yok) · `Hatalı` (kırmızı, yükleme hatası) · `Düşürüldü`. Rozetin altında kısa sebepler yazar (örn. "Kanal stoğu yok · Kategori eşlemesi yok"). |
| İşlem | Satır bazlı hızlı bağlantı: kanaldaysa **Çıkar**, değilse **Kanala al**. |

| Filtre | Ne yapar |
|---|---|
| Ara (kod veya ad) | Yazıp **Ara**'ya basın ya da Enter; ürün kodu veya adında arar. |
| Durum | `Tümü`, `Kanalda`, `Kanaldan Çıkarılan`, `Durdurulan`. |

Satır tıklamak detay açmaz; işlemler satırdaki bağlantı ve toplu çubukla yapılır. Kanal, arama ya da durum
değişince seçim sıfırlanır. Sonuç yoksa "Kayıt bulunamadı." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Çıkar / Kanala al | Satırdaki İşlem sütunu | Tek ürünü kanaldan çıkarır / kanala geri alır; anında uygulanır, onay sorulmaz. | Kanal seçili. |
| Filtreye uyan tümünü seç (N) | Toplu çubuk | Yalnız sayfadakiler değil, arama+durum filtresine uyan **tüm** ürünleri seçer. | En az bir satır seçili. |
| Temizle | Toplu çubuk | Seçimi boşaltır. | — |
| Kanala Al | Toplu çubuk | Seçili ürünleri kanala alır. | — |
| Kanaldan Çıkar | Toplu çubuk | ⚠️ Seçili ürünleri kanaldan çıkarır; sitede görünmez olur. Onay sorulmaz; geri almak için Kanala Al. | — |
| Satışı Durdur | Toplu çubuk | "Satışı Durdur — N ürün" penceresi açılır (aşağıda). | — |
| Satışı Başlat | Toplu çubuk | Seçili ürünlerdeki durdurma penceresini kaldırır; satış hemen açılır. | — |
| Sayfalama | Tablo altı | 30'arlı sayfalar arasında geçiş. | — |

### Satışı Durdur penceresi
| Alan | Zorunlu | Açıklama / kurallar |
|---|---|---|
| Başlangıç (opsiyonel) | Hayır | Tarih-saat; boşsa durdurma **hemen** başlar. |
| Bitiş (opsiyonel) | Hayır | Tarih-saat; boşsa **süresiz** durur. Bitiş geçince satış otomatik yeniden açılır. |
| Durdur | — | Uygular; hata olursa "İşlem başarısız — tarih aralığını kontrol edin." (bitiş başlangıçtan önce olamaz). |
| Vazgeç | — | Pencereyi kapatır. |

## Durumlar ve iş kuralları
- Üç durum vardır ve birbirinden bağımsız iki ayar üretir: **kanal seçimi** (kanalda / kanaldan çıkarıldı) ve
  **durdurma penceresi** (başlangıç–bitiş).
- `Kanaldan çıkarıldı`: ürün sitede listelenmez, aranmaz, sepete eklenemez; ürün adresi ana sayfaya yönlenir.
  Kalıcı bir karar gibi düşünün (sezon dışı, kanalda satılmayacak ürün).
- `Durduruldu`: ürün kanaldadır ama pencere boyunca satışta değildir; pencere bitince kendiliğinden `Kanalda`
  olur. Geçici sorunlar için uygundur.
- Ürün varsayılan olarak kanaldadır; bir ürün için hiç işlem yapılmadıysa `Kanalda` görünür.
- Değişiklikler sitede kısa süre içinde (önbellek tazelenince) etkili olur.

## Adım adım
**Bir tedarikçinin ürünlerini geçici olarak satıştan kaldırma**
1. Satış Kanalı'nı seçin; arama kutusuna ortak kod ön ekini yazıp **Ara**.
2. Başlık kutusuyla sayfayı seçin, gerekiyorsa **Filtreye uyan tümünü seç (N)**.
3. **Satışı Durdur** → Bitiş tarihini girin → **Durdur**.
4. Sorun çözülünce aynı ürünleri seçip **Satışı Başlat** deyin (ya da bitişi bekleyin).

**Sezon sonu ürünleri kanaldan çıkarma**
1. Durum filtresi `Kanalda` + arama ile listeyi daraltın.
2. Ürünleri seçin → **Kanaldan Çıkar**.
3. Yanlışlıkla çıkanları `Kanaldan Çıkarılan` filtresinde bulup **Kanala al** ile geri alın.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Toplu işlemler onay sormaz; "Filtreye uyan tümünü seç" ile binlerce ürün seçilebilir — sayıyı
> kontrol etmeden Kanaldan Çıkar'a basmayın.

> **İpucu:** Geçici durumlarda **Satışı Durdur** (bitiş tarihli) tercih edin; Kanaldan Çıkar ürünü tamamen
> siteden kaldırır.

> **Not:** Satış durdurulmuş ürünler kanalda kalır; "Durdurulan" filtresiyle listelenir ve sayaçta görünür.

## İlgili sayfalar
- [Kanal Kapsamı](/rehber/vitrin/kanal-kapsami/) — bu kanalda hangi ürünlerin söz konusu olduğu (listeye gelmeyen ürün kapsam dışı olabilir)
- [Kanal Kategorileri](/rehber/vitrin/kanal-kategorileri/)
- [Ürün Kartları](/rehber/katalog/urun-kartlari/)
- [Stok Takibi](/rehber/stok/stok-takibi/)
