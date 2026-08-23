---
title: Kanal Kategorileri
route: /storefront/channel-categories
group: Vitrin
order: 10
summary: Satış kanalına özgü ürün listeleme kategorilerinin (menü, filtre ve banner hedefi) listelendiği, oluşturulduğu ve detayında doldurma/listeleme/grup ayarlarının yapıldığı ekran.
---

## Ne işe yarar
Vitrin bölümündeki sayfalar sol menüde **Katalog** bölümünün altında yer alır; sitede müşterinin gördüğü yüzü
(kategori sayfaları, menü, ürün kartı, ana sayfa blokları) yönetir. **Kanal kategorisi**, bir satış kanalında
(örn. web siteniz) ürünlerin listelendiği sayfadır: menü öğesi, banner/link hedefi ve site içi filtre bu
kategoriler üzerinden çalışır. Ürün gruplarından farklı olarak kanala özeldir; aynı ürün grubu farklı kanallarda
farklı kategorilerde gösterilebilir. Katalog/pazarlama sorumlusu yeni bir listeleme sayfası açarken, kural tabanlı
bir koleksiyon ("Son 30 günde eklenenler") kurarken ya da menüde gösterilecek kategorileri hazırlarken kullanır.

## Ekran yerleşimi
![Kanal Kategorileri listesi](img/storefront-channel-categories.webp)
1. **Başlık ve Yeni Kategori butonu** — buton, kanal seçilmeden pasiftir.
2. **Satış Kanalı seçici** — aranabilir açılır liste ("Kanal adı (Firma adı)"). Seçim tarayıcı oturumunda
   hatırlanır ve Menü Yerleşimi / Ürün Kartı sayfalarına da taşınır.
3. **Kapsam uyarısı** (sarı şerit) — yayında olup hiçbir ürün grubundan sorumlu olmayan kategori sayısı.
4. **Kategori tablosu** — seçili kanalın kategorileri; satıra tıklayınca detay açılır.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| Kategori | Kategori adı; varsa rozet etiketi (ör. `Yeni`) adın yanında; altında URL (`/erkek-spor`). |
| Dolum | Doldurma türü: `Manuel` (ürünler elle eklenir), `Filtre` (kural tabanlı otomatik), `Karma` (filtre + elle eklenen sabitler). |
| Gruplar | Kategorinin sorumlu olduğu ürün grubu sayısı; hiç yoksa `⚠ Tanımsız`. |
| Durum | `Yayında` (sitede görünür), `Taslak`, `Arşiv`. Sitede yalnız yayındaki kategoriler listelenir. |

| Filtre | Ne yapar |
|---|---|
| Satış Kanalı | Zorunlu; liste yalnız seçili kanalın kategorilerini gösterir. Kanal seçilmeden tablo görünmez. |

- Arama kutusu ve sayfalama yoktur; kanalın tüm kategorileri tek listede gelir.
- Liste düz gösterilir; üst/alt kategori ilişkisi bu tabloda gösterilmez.
- Kanalda kategori yoksa "Bu kanalda henüz kategori yok" yazısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Kategori | Sağ üst | "Yeni Kanal Kategorisi" penceresi açılır; **Oluştur** ile kategori `Taslak` durumda ve `Manuel` dolum tipiyle oluşturulur, doğrudan detay sayfasına geçilir. | Kanal seçili olmalı; Ad zorunlu. |
| İptal | Pencere altı | Pencereyi kapatır, girilenler silinir. | — |
| Satır tıklama | Tablo | Kategori detayı açılır. | — |

### Yeni Kanal Kategorisi penceresi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ad (TR) | Evet | Kategorinin adı; yazdıkça URL alanı otomatik üretilir (Türkçe karakterler sadeleştirilir: "Erkek Spor" → `erkek-spor`). |
| URL | Hayır | Sitede `/` sonrası görünen adres. Yalnız küçük harf, rakam ve tire kabul edilir; elle değiştirdikten sonra addan otomatik üretim durur. Boş bırakılırsa isimden üretilir. |

## Detay sayfası
![Kanal kategorisi detayı — Genel sekmesi](img/storefront-channel-categories-detay.webp)
1. **Başlık şeridi** — geri oku, kategori adı, URL, durum rozeti (`Yayında`/`Taslak`/`Arşiv`), listeleme rozeti
   (`Renk Bazlı`/`Model Bazlı`) ve sağda **kapsam rozeti** (`Kapsam tam` yeşil ya da `N grup kapsam dışı` sarı).
2. **Sekmeler** — Genel · Gruplar · Ürünler · SEO.
3. **Sekme içeriği** — form ya da tablo; her sekmenin kendi **Kaydet** butonu vardır.

### Genel sekmesi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ad | Evet (kaynak dil) | Çok dilli ad (`TR`/`EN` bayraklı sekmeler). Kaynak dil zorunlu, diğerleri boş bırakılabilir. |
| Google ürün kategorisi (Merchant Center feed) | Hayır | Google taksonomi kimliği (`2271`) ya da tam yolu (`Apparel & Accessories > Clothing > Dresses`). Boşsa üst kategorininki kullanılır; hiçbiri yoksa ürün akışına yazılmaz ve Google kendisi sınıflandırır. |
| URL | Evet | `/` sonrası adres; yalnız küçük harf, rakam ve tire. |
| Dolum Tipi | Evet | `Manuel — ürünler elle eklenir`, `Filtre — kural tabanlı otomatik`, `Karma — filtre + sabitler`. Filtre/Karma seçilince aşağıda **Filtre Tanımı** bölümü açılır. |
| Durum | Evet | `Taslak`, `Yayında`, `Arşiv`. |
| Listeleme Tipi | Evet | `Renk (Ana Varyant) Bazlı Liste`: her renk varyantı ayrı kart (moda/tekstil için varsayılan). `Model Bazlı Liste`: her ürün grubu tek kart; grubu temsil eden **vitrin ürünü** Gruplar sekmesinden seçilir, seçilmezse ilk aktif ürün kullanılır. Seçime göre yeşil/mavi açıklama kutusu görünür. |
| Filtre Tanımı | Filtre/Karma'da | Kural kurucu (aşağıda). |
| Badge Etiketi | Hayır | Kategori adının yanında görünen kısa rozet: `Yeni`, `İndirim`… |
| Sıra | Hayır | Sayısal sıra; kategori seçiminde eşit derinlikteki kategoriler arasında öncelik belirler. |
| Görsel URL | Hayır | Kategori görseli (`https://…`); menü ve kategori kapsüllerinde kullanılır. |

**Filtre Tanımı** (Dolum Tipi Filtre veya Karma iken) — her bölüm boş bırakılırsa o koşul uygulanmaz; hiçbir koşul
yoksa açıklama "Tüm ürünler" olur:

| Bölüm | Ne yapar |
|---|---|
| Ürün Grupları | Bir ya da daha çok ürün grubu seçilir (çip olarak listelenir, × ile kaldırılır). |
| Tedarikçi | Tedarikçi cari hesapları. |
| Temel Fiyat | Min/maks temel satış fiyatı (₺). |
| Platform Fiyatı | Kanala özel satış fiyatı aralığı. |
| Min. İndirim | En az % indirim oranı. |
| KDV Oranı | Min/maks KDV (%). |
| Ürün Durumu | `Tümü` / `Sadece Aktif` / `Sadece Pasif`. |
| Stok Miktarı | Tüm depolar toplamı (adet) aralığı. |
| Oluşturma Tarihi | "Son N gün" ya da tarih aralığı — yeni eklenen ürünler için. |
| Resim Güncelleme Tarihi | "Son N gün" ya da tarih aralığı — fotoğrafı güncellenen ürünler. |
| Etiketler | Ürün etiketleri; en az biri eşleşmeli. Yazarak ya da listeden seçerek eklenir. |
| Özellik Filtreleri | Özellik tipi + değerleri (renk, beden, cinsiyet vb.); birden çok özellik satırı eklenebilir. |

**Kaydet** (sekme altı) — tüm Genel alanlarını yazar. Listeleme Tipi değiştiyse Ürünler sekmesi de yeniden yüklenir.

### Gruplar sekmesi
Kategorinin ürünlerini göstermekten **sorumlu olduğu ürün grupları** burada atanır; kapsam hesabı buradan yapılır.

| Alan / Buton | Ne olur |
|---|---|
| Sorumlu Ürün Grupları listesi | Atanmış her grup bir satırdır; **Kaldır** ile listeden çıkarılır. |
| Vitrin ürünü (yalnız Model Bazlı listelemede) | Her grup satırının altında; grubu temsil edecek ürün seçilir (`Otomatik (ilk aktif ürün)` varsayılan). **Temizle** otomatiğe döndürür. |
| Grup Ekle | Aranabilir listeden grup seçilince satır eklenir (aynı grup ikinci kez eklenemez). |
| Kaydet | Yalnız değişiklik yapıldığında görünür; grup listesini ve vitrin ürünlerini yazar. |

**Kanal Kapsam Özeti** kartı: `Kanalda Aktif Grup` (kanala atanmış grup sayısı) · `Kapsanan` (en az bir yayındaki
kategorinin sorumlu olduğu) · `Kapsam Dışı` (hiçbir yayındaki kategoriye bağlı olmayan — sarı). Kapsam dışı grup,
ürünlerinin sitede hiçbir kategoride listelenmeyebileceği anlamına gelir.

### Ürünler sekmesi
Üstte sayaç (`N ürün` / Model Bazlı'da `N model`) ve butonlar:

| Buton | Görünme koşulu | Ne olur |
|---|---|---|
| Sync | Dolum tipi Filtre/Karma | Filtre tanımı çalıştırılır; kategorideki otomatik ürün listesi baştan kurulur. Elle **Hariç** işaretlenen ürünler korunur, diğer eski kayıtlar silinip eşleşen ürünler sırayla yeniden eklenir. Bitince "Sync tamamlandı — kategoride N ürün listelenecek" mesajı gelir. Manuel kategoride çalışmaz. |
| Ürün Ekle | Renk Bazlı listeleme | "Ürün Ekle" penceresi (aşağıda). Model Bazlı'da buton yoktur; vitrin ürünleri Gruplar sekmesinden yönetilir. |
| Çöp kutusu (satır) | Renk Bazlı listeleme | Ürünü kategoriden çıkarır (onay sorulmaz). |

| Sütun | Anlamı |
|---|---|
| Ürün | Küçük görsel, ad ve ürün kodu. Hariç tutulan satır soluk gösterilir. |
| Sıra / Grup | Renk Bazlı'da sıra numarası; Model Bazlı'da ürünün grubu. |
| Tip | `Dahil` / `Hariç` (Renk Bazlı) ya da `Vitrin` (Model Bazlı). |

Sayfalama: 20 satır; 20'den fazla kayıt varsa altta `←` / `Sayfa N` / `→`.

**Ürün Ekle penceresi**
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Ürün | Evet | Aranabilir ürün listesi ("Ad (Kod)"). |
| Sıra | Hayır | Listeleme sırası (0 = en başa yakın). |
| Hariç tut (filtre sonucundan çıkar) | Hayır | İşaretlenirse ürün filtre eşleşse bile kategoride gösterilmez; Sync bu kaydı korur. Karma kategorilerde otomatik sonuçtan tekil ürün düşürmek için kullanılır. |

### SEO sekmesi
Bu sürümde sekme yalnız "SEO alanları yakında eklenecek." notunu gösterir; meta başlık/açıklama ve paylaşım
görseli alanları henüz panelden düzenlenemez.

## Durumlar ve iş kuralları
- Durum rozetleri: `Taslak` → `Yayında` → `Arşiv` (istenen yöne elle değiştirilir). Sitede yalnız `Yayında`
  kategoriler listelenir, menüde görünür ve URL'den açılır.
- Kapsam: kanala atanmış her ürün grubunun en az bir **yayındaki** kategorinin sorumluluğunda olması beklenir;
  aksi halde listede `⚠ Tanımsız`, detayda `N grup kapsam dışı` uyarısı görünür.
- Dolum: `Manuel` kategoride ürünler yalnız Ürün Ekle ile gelir; `Filtre`/`Karma` kategoride liste Sync ile
  kurulur; `Karma`da ayrıca elle eklenen/hariç tutulan kayıtlar korunur.
- Listeleme tipi değişimi ürün listesinin görünümünü değiştirir (renk kartları ↔ grup başına tek kart); kaydettikten
  sonra Ürünler sekmesi yeniden yüklenir.
- Filtre tanımında "Ürün Durumu" seçilmemişse Sync yalnız aktif ürünleri alır.
- Kanal kategorisi sayfalarının ürün listesi sitede önbelleklenir; değişiklikler birkaç dakika içinde yansır.

## Adım adım
**Kural tabanlı yeni kategori açma**
1. Satış Kanalı'nı seçin, **Yeni Kategori**'ye tıklayın; Ad girin, URL'yi kontrol edin, **Oluştur**.
2. Açılan detayda Genel sekmesinde Dolum Tipi'ni `Filtre` yapın, Filtre Tanımı'nda koşulları seçin
   (ör. Ürün Grupları + Son 30 günde eklenen), Listeleme Tipi'ni belirleyin, **Kaydet**.
3. Gruplar sekmesinde kategorinin sorumlu olduğu ürün gruplarını ekleyin, **Kaydet**.
4. Ürünler sekmesinde **Sync**'e basın; sayacı kontrol edin.
5. Genel sekmesinde Durum'u `Yayında` yapıp **Kaydet**; menüde göstermek için Menü Yerleşimi'ne ekleyin.

**Otomatik listeden tek ürün düşürme**
1. Ürünler sekmesinde **Ürün Ekle** → ürünü seçin → **Hariç tut** kutusunu işaretleyin → **Ekle**.
2. Sonraki Sync'lerde bu ürün listeye geri gelmez.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Seçili kanal tarayıcı oturumunda hatırlanır; Menü Yerleşimi ve Ürün Kartı sayfaları aynı kanalla açılır.

> **Dikkat:** Sync mevcut otomatik listeyi tamamen yeniden kurar; elle eklediğiniz sıra numaraları (Hariç olmayan
> kayıtlar) sıfırlanır.

> **Dikkat:** Ürün silme (çöp kutusu) onay sormaz; silinen ürün Filtre/Karma kategoride bir sonraki Sync ile geri
> gelebilir — kalıcı dışlamak için Hariç tut kullanın.

> **Not:** "Bu kanalda henüz kategori yok" görüyorsanız kanal yeni olabilir; önce kategorileri açın, sonra Menü
> Yerleşimi'nde üst menüyü oluşturun.

## İlgili sayfalar
- [Menü Yerleşimi](/rehber/vitrin/menu-yerlesimi/)
- [Kanal Ürünleri](/rehber/vitrin/kanal-urunleri/)
- [Vitrin Yönetimi](/rehber/vitrin/vitrin-yonetimi/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
