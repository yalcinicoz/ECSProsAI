---
title: Ürün Detayı
route: /catalog/products/:code
group: Katalog
order: 12
summary: Bir ürün kartının tüm sekmeleri — genel bilgiler ve fiyat, özellikler, varyantlar ve barkod, alt özellikler, stok, satış kanalı fiyatları, görseller/videolar, etiketler ve SEO.
---

## Ne işe yarar
Ürün Detayı, tek bir ürün kartının yönetildiği ana ekrandır. Katalog sorumlusu ürünün adını/açıklamasını, fiyatını,
KDV'sini, özelliklerini, varyantlarını (renk/beden), barkodlarını, görsellerini, kanal bazlı fiyatlarını, etiketlerini
ve SEO bilgilerini burada tamamlar; ürünü satışa açar ya da kapatır. Yeni açılan her ürün kartı sizi doğrudan bu
sayfaya getirir. Stok miktarı burada yalnızca **izlenir**; stok girişi Stok modülünden yapılır.

## Ekran yerleşimi
![Ürün detayı — üst başlık (görsel, ad, satış rozeti, Kaydet) ve sekme şeridi; Genel sekmesi açık](img/catalog-products-detay.webp)
1. **Üst başlık** — solda ürünün kapak görseli (yoksa "IMG"), "Ürünler › <kod>" yol bağlantısı, ürün adı ve `Satışta` /
   `Satış Kapalı` rozeti, altında "kod · grup adı · N varyant" satırı. Sağda **Kaydet** butonu ve kayıt sonucu
   (`Kaydedildi` yeşil / `Hata!` kırmızı).
2. **Sekme şeridi** — Genel · Özellikler · Varyantlar (N) · Alt Özellikler · Stok · Satış Kanalları · Görseller ·
   Etiketler · SEO. **Alt Özellikler** sekmesi yalnızca ürün grubunda eksen alt özelliği (örn. bedene bağlı ölçü)
   tanımlıysa görünür.
3. **İçerik alanı** — seçili sekmenin formu/tablosu. Genel sekmesinde sağda ek bir yan panel vardır.

## Butonlar ve aksiyonlar (üst başlık)

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Sağ üst | **Genel** sekmesindeki alanları (adlar, açıklamalar, alış/satış fiyatı, KDV, tedarikçi, tedarikçi ürün kodu) kaydeder. Diğer sekmelerin kendi Kaydet butonları vardır. | Panele giriş yeterli. |
| Ürünler (yol bağlantısı) | Sol üst | Ürün Kartları listesine döner. | — |
| Sekme başlıkları | Şerit | Sekmeyi değiştirir; kaydedilmemiş Genel değişiklikleri sekme değişince kaybolmaz, ancak sayfadan çıkınca kaybolur. | — |

## Sekmeler

### Genel
![Genel sekmesi — Temel Bilgiler, Çok Dilli İçerik ve sağda Satış Durumu / Kayıt Bilgisi / Tehlikeli Alan](img/catalog-products-detay.webp)

**Temel Bilgiler kartı**

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ürün Kodu | — | Salt okunur; oluşturma sırasında belirlenir, değiştirilemez. |
| Ürün Grubu | — | Salt okunur; ürünün grubu. Grup değiştirilemez. |
| Alış Fiyatı (₺) | Hayır | Maliyet. Boş bırakılabilir. Değişiklikler fiyat geçmişine yazılır. |
| Satış Fiyatı (₺) | Evet | Ürünün ana (baz) satış fiyatı. Altında "Tüm varyantlara baz fiyat olarak yansır" notu: yeni eklenen varyantlar bu fiyatla açılır; kanal fiyatlarında varyantın kendi fiyatı yoksa bu fiyat esas alınır. Tek varsayılan varyantlı üründe (varyant ekseni olmayan grup) fiyat değişince varyanta da otomatik yazılır. Çok varyantlı üründe mevcut varyant fiyatları **değişmez**; onlar Varyantlar sekmesinden düzenlenir. |
| KDV (%) | Evet | Açılır liste: `%0`, `%1`, `%8`, `%10`, `%18`, `%20`. Varsayılan `%18`. |
| Tedarikçi | Hayır | Aranabilir liste; yalnız **aktif, tedarikçi tipli cari hesaplar** "Ünvan (kod)" biçiminde listelenir. Tedarikçi yoksa önce Cari modülünde cari hesap açılmalıdır. |
| Tedarikçi Ürün Kodu | Hayır | Tedarikçinin kendi ürün kodu; tedarikçi gönderimlerinde eşleşme anahtarıdır. |

- Kartın sağ üstündeki **Fiyat Geçmişi** butonu sağdan kayan bir panel açar: `TARİH / KİŞİ`, `KAYNAK` (mavi
  `Satış Fiyatı` / `Alış Fiyatı` rozeti ya da sarı **kanal kodu** + varyant SKU'su), `ESKİ`, `YENİ`, `DEĞİŞİM`
  (tutar ve yüzde; artış kırmızı, düşüş yeşil). Panel `Esc` ile, karartılmış alana tıklayarak ya da sağ üstteki
  simgeyle kapanır. Kayıt yoksa "Henüz fiyat değişikliği kaydı bulunmuyor." yazar.

**Çok Dilli İçerik kartı**
- Kart başlığının sağında her dil için bir rozet vardır: o dilde ürün adı girildiyse `✓ TR` (yeşil), girilmediyse gri `EN`.
- Altında dil sekmeleri (Türkçe, English …). **Kaynak dil** (varsayılan dil) sekmesinde üç alan alt alta gelir;
  diğer dillerde solda kaynak metin salt okunur, sağda çeviri alanı ("Çeviri girin…") yan yana gösterilir.

| Alan | Zorunlu | Açıklama |
|---|---|---|
| Ürün Adı | Kaynak dilde evet | Sitede ve listelerde görünen ad. Dolu alan yeşil işaretlenir. |
| Kısa Açıklama | Hayır | Kart altı / liste açıklaması; SEO meta açıklaması boşsa bu kullanılır. |
| Açıklama | Hayır | Ürün detay sayfasındaki uzun açıklama. |

**Sağ yan panel**

| Bölüm | İçerik |
|---|---|
| Satış Durumu | `Satışta` (yeşil) / `Satış Kapalı` (kırmızı) rozeti. |
| Kayıt Bilgisi | Oluşturulma tarihi, Son Güncelleme (varsa), Varyant sayısı. |
| Tehlikeli Alan | **Satışı Kapat / Satışa Aç** ve **Ürünü Sil** butonları (aşağıda). |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Fiyat Geçmişi | Temel Bilgiler başlığı | Fiyat değişim panelini açar. | — |
| Satışı Kapat / Satışa Aç | Tehlikeli Alan | Onay penceresi açılır: "… ürünü satışa kapatılacak — tüm satış kanallarında satışı durur" ya da "satışa açılacak (tüm kanallarda)". Onaylayınca ürünün genel satış anahtarı anında değişir (Kaydet'e gerek yok). Satışa açarken ürünün **hiç varyantı yoksa** ⚠️ "satılabilir birim varyant olduğu için sitede satın alınamaz" ve **satış fiyatı 0 ise** ⚠️ "fiyatsız ürün satışa açılıyor" uyarıları gösterilir (engellemez). | — |
| Ürünü Sil ⚠️ | Tehlikeli Alan | "Ürünü Sil" onay penceresi: "<ürün> ürünü ve tüm varyantları silinecek. Bu işlem geri alınamaz." **Kalıcı Olarak Sil** ile ürün, varyantları ve özellikleri silinir, listeye dönülür. Geçmiş siparişler etkilenmez ama ürün panelde bir daha görünmez. | — |

### Özellikler
![Özellikler sekmesi — grubun ürün özellikleri için seçim listeleri](img/catalog-products-detay--ozellikler-sekmesi.webp)
Ürün grubunda tanımlı, varyant ekseni **olmayan** özellikler (örn. Kumaş, Desen, Marka) burada girilir. Bu değerler
ürünün tüm varyantları için ortaktır.

| Alan | Zorunlu | Açıklama |
|---|---|---|
| Her özellik için bir açılır liste | Grup tanımında zorunluysa `*` | Seçenekler özellik tipinin değer havuzundan gelir; `— Seçiniz —` ile boş bırakılabilir. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Kart başlığı sağı | Tüm özellik seçimlerini topluca kaydeder; `Kaydedildi` / `Hata!` gösterir. | — |

- Grupta hiç özellik yoksa: "Bu ürün grubunda henüz özellik tanımlı değil. Ürün Grupları sayfasından özellik
  ekleyebilirsiniz." bilgisi görünür.
- Bir özelliğin değer listesi boşsa önce **Özellik Tipleri** sayfasından değer ekleyin.

### Varyantlar
![Varyantlar sekmesi — renk sekmeleri, varyant tablosu, Barkod Oluştur ve + Varyant Ekle](img/catalog-products-detay--varyantlar-sekmesi.webp)
Ürünün satılabilir birimleri. Her satır bir SKU'dur. Sekme başlığında varyant sayısı görünür.

| Sütun | Anlamı |
|---|---|
| SKU | Varyant kodu. Otomatik üretilir: `ÜRÜNKODU-RENK-BEDEN` (her eksen değerinin ilk 6 harfi büyük harfle); çakışırsa sonuna 4 karakterlik ek gelir. Varsayılan varyantta SKU = ürün kodu. Panelden düzenlenemez. |
| ÖZELLİKLER | Eksen değerleri rozet olarak; **birincil eksen** (genelde renk) dolu renkli, diğerleri açık renkli. Eksensiz varyantta `—`. |
| BARKOD | Satır içi metin kutusu. Alandan çıkınca otomatik kaydedilir. Aynı barkod başka varyantta varsa `'…' barkodu başka bir varyanta atanmış.` hatası döner. Boşsa "Barkod girilmedi". |
| FİYAT (₺) | Varyantın kendi satış fiyatı; satır içi düzenlenir, alandan çıkınca kaydedilir (`✓` kısa süre görünür). Her değişiklik fiyat geçmişine yazılır. |
| STOK | Tüm depolardaki **kullanılabilir** (rezerve düşülmüş) toplam miktar; salt okunur. |
| DURUM | `Aktif` / `Pasif` rozeti; tıklayınca durum değişir (anında kaydedilir). Pasif varyant sitede satılmaz. |
| 🗑 | Varyantı sil — onay penceresi açar. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Varyant Ekle | Kart başlığı sağı | "Varyant Ekle" penceresi açılır (aşağıda). | Grupta en az bir varyant ekseni olmalı; yoksa buton görünmez. |
| Barkod Oluştur | Kart başlığı sağı | Barkodu boş olan tüm varyantlara, barkod seri sayacından ardışık **EAN-13** barkodlar üretip yazar ve kaydeder. Dolu barkodlara dokunmaz. | En az bir varyant olmalı. |
| ⚙ (Barkod Seri Ayarı) | Barkod Oluştur'un yanı | "Barkod Seri Ayarı" penceresi: **Seri Başlangıç Değeri** (en az 1) girilir; altında "İlk üretilecek EAN-13: …" önizlemesi. Kaydet ile sayaç güncellenir. Bu ayar **tüm mağaza için ortaktır**; üretilen barkodlar sayacı ilerletir. | — |
| Default Varyant Oluştur | Boş durum (eksensiz grup) | Grupta varyant ekseni yokken 0 varyantlı ürün için özniteliksiz tek varyant açar (SKU = ürün kodu). | Grup eksensiz ve varyant yok. |
| Renk sekmeleri | Tablonun üstü | Birincil eksende 2+ değer varsa varyantlar değer bazında sekmelere ayrılır: "Kırmızı (4)", "Siyah (4)". | — |
| Varyantı Sil ⚠️ | Satır sonu 🗑 | "Bu varyant kalıcı olarak silinecek. Devam etmek istiyor musunuz?" → **Sil**. | — |

**Varyant Ekle penceresi**
![Varyant Ekle penceresi — eksen değerleri seçimi ve oluşturulacak kombinasyon listesi](img/catalog-products-detay--varyant-ekle-modal.webp)
- Her varyant ekseni (birincil eksen önce) için değerler **çip** olarak listelenir; tıklayarak seçersiniz, başlıkta
  "(n seçili)" yazar. Üründe zaten kullanılan değerler açılışta seçili gelir.
- Altta "Oluşturulacak kombinasyonlar (N)" kutusunda tüm kombinasyonlar ("Kırmızı / S", "Kırmızı / M" …) listelenir.
- **N Kombinasyon Ekle** butonu yalnızca en az bir kombinasyon varken aktiftir (aksi halde "Kombinasyon Seçin").
- Zaten var olan kombinasyonlar **atlanır**, yalnız yeniler eklenir. Yeni varyantlar ürünün satış fiyatı ve alış
  fiyatıyla, `Aktif` olarak açılır. Pasif kombinasyon seçeneği yoktur — seçtiğiniz her kombinasyon oluşturulur.
- Bir eksenin değer listesi boşsa "Bu özellik tipinde değer tanımlı değil." uyarısı görünür.

> **Dikkat:** Eksensiz açılmış bir üründe ilk gerçek kombinasyon eklendiğinde mevcut **varsayılan varyant otomatik
> pasife çekilir** (silinmez; geçmiş sipariş/stok kaydı ona bağlı olabilir).

### Alt Özellikler
![Alt Özellikler sekmesi — eksen değeri sütunları ve alt özellik satırlarından oluşan ölçü tablosu](img/catalog-products-detay--alt-ozellikler-sekmesi.webp)
Yalnızca ürün grubunda bir varyant eksenine bağlı **alt özellikler** (örn. Beden eksenine bağlı Göğüs, Bel, Boy ölçüsü)
tanımlıysa görünür. Değerler **eksen değeri başına** girilir ve tüm renklerde paylaşılır ("tüm renkler için aynı beden
değerleri paylaşılır").

| Öğe | Açıklama |
|---|---|
| Sütunlar | Üründe kullanılan eksen değerleri (S, M, L …) renkli rozet olarak. |
| Satırlar | Alt özellikler (ALT ÖZELLİK sütunu); grup tanımında zorunluysa `*`. |
| Hücreler | Serbest metin/sayı kutusu (örn. `92`). Birden fazla eksenin alt özelliği varsa her eksen ayrı tablo başlığıyla listelenir. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Kart başlığı sağı | Tüm hücreleri topluca kaydeder. | Üründe en az bir varyant olmalı ("Alt özellik değeri girmek için önce varyant oluşturun."). Yazma yetkisi: ürün yönetimi izni (`catalog.products.manage`). |

### Stok
![Stok sekmesi — Toplam/Rezerve/Kullanılabilir özet kartları ve depo bazlı stok tablosu](img/catalog-products-detay--stok-sekmesi.webp)
Salt okunur izleme ekranıdır.

| Öğe | Anlamı |
|---|---|
| Toplam | Tüm varyant ve depolardaki fiziksel miktar toplamı. |
| Rezerve | Onaylanmış siparişler için ayrılmış miktar (sarı). |
| Kullanılabilir | Toplam − Rezerve; satışa sunulabilecek miktar. |

| Sütun | Anlamı |
|---|---|
| DEPO | Depo adı. |
| VARYANT | Varyant SKU'su. |
| TOPLAM / REZERVE / KULLANILABİLİR | İlgili depo-varyant satırının miktarları. Rezerve 0'dan büyükse sarı gösterilir. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Stok Hareketi | Tablo başlığı sağı | Stok modülündeki Stoklar sayfasına götürür; giriş/çıkış/düzeltme orada yapılır. | — |

Stok kaydı yoksa "Stok kaydı bulunamadı." yazar.

### Satış Kanalları
![Satış Kanalları sekmesi — kanal seçim çipleri, Öne Çıkar paneli ve varyant bazlı kanal fiyat tablosu](img/catalog-products-detay--satis-kanallari-sekmesi.webp)
Ürünün her satış kanalındaki fiyatı ve öne çıkarma durumu burada yönetilir.

**Kanal seçimi** — Üstte firmalara göre gruplanmış kanal çipleri: 🌐 web sitesi kanalları, 🛒 pazaryeri kanalları. Yalnız
aktif kanallar listelenir; hiç kanal yoksa "Aktif satış kanalı bulunamadı. Önce Ayarlar → Satış Kanalları'ndan kanal
oluşturun." uyarısı görünür.

**Öne Çıkar paneli (seçili kanal için)**

| Alan / Buton | Açıklama |
|---|---|
| Durum rozeti | `Sponsorlu — aktif` (şu an öne çıkıyor) · `Planlı / süresi dolmuş` (tarih girilmiş ama aktif değil) · `Pasif`. |
| Başlangıç | Tarih; boşsa bugün kabul edilir. |
| Bitiş (boş = süresiz) | Tarih; boş bırakılırsa süresiz. |
| Öne Çıkar | Kaydeder. Ürün bu kanalın listelerinde varsayılan sırada öne alınır ve kartta "Sponsorlu" rozeti görünür. |
| Kaldır | Öne çıkarmayı tamamen kaldırır (yalnız tarih girilmişse görünür). |

**Fiyat tablosu** — Başlıkta kanal adı, platform tipi ve **kanal kuralı** rozeti:
- `Kanal kuralı: ×1.20 — tüm varyantlara uygulanır`: kanal tanımında çarpan var; varyant özel fiyat girmedikçe ana fiyat × çarpan uygulanır.
- `Kanal kuralı: Manuel (varyant bazlı)`: her varyanta elle fiyat girilir.
- `Kanal kuralı: Firmadan alınıyor`: kanalın kendi kuralı yok; ürün fiyatı geçerli.

| Sütun | Anlamı |
|---|---|
| VARYANT | SKU ve eksen değerleri. |
| ANA FİYAT | Varyantın kendi fiyatı (0 ise ürünün satış fiyatı); salt okunur. |
| FİYAT TİPİ | `Üründen al` (varyant için kanal fiyatı yok — kanal kuralı/ürün fiyatı geçerli) · `Manuel` (sabit tutar) · `Çarpan` (ana fiyat × katsayı). Tip değişince tutar alanı sıfırlanır. |
| KANAL FİYATI | Manuel'de ₺ tutar; Çarpan'da `×` katsayı; Üründen al'da hesaplanan fiyat (kanal çarpanı varsa yeşil ve `×1.20` rozetli, yoksa ana fiyat italik). |
| LİSTE FİYATI | Üstü çizili gösterilecek "eski/karşılaştırma" fiyatı; boş bırakılabilir. Ana fiyattan yüksekse sitede indirim yüzdesi rozeti çıkar. |
| AKTİF | Onay kutusu; kapatılırsa varyant bu kanalda satılmaz. |
| Kaydet | Satır bazlı kaydetme; kaydedilince `Kaydedildi` görünür. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Tümünü Kaydet | Tablo başlığı sağı | Tüm satırları tek seferde kaydeder. | Kaydedilmemiş satır olmalı. |
| Toplu fiyat → Tüm Varyantlara Uygula | Tablonun üst şeridi | Seçilen tipi (Manuel/Çarpan) ve tutarı tüm satırlara yazar; **kaydetmez** — ardından Tümünü Kaydet'e basın. Satırları tek tek düzeltmeye devam edebilirsiniz. | Tutar girilmiş olmalı. |

- **Düşük fiyat uyarısı:** Kanal fiyatı ana fiyatın altına düşen varyantlar sarı zeminle ve ⚠️ simgesiyle işaretlenir;
  üstte "N varyant için kanal fiyatı ana fiyatın altında. Kaydedebilirsiniz, ancak bu kanalda zarar satışı oluşabilir."
  uyarısı çıkar. Kayıt **engellenmez**.
- Kanal fiyat değişiklikleri de **Fiyat Geçmişi** panelinde (sarı kanal rozetiyle) listelenir.
- Ürünün varyantı yoksa "Bu ürünün varyantı bulunmuyor." yazar.

### Görseller
![Görseller sekmesi — Resimler alt sekmesi: set/varyant seçimi, yükleme alanı ve mevcut resimler](img/catalog-products-detay--gorseller-sekmesi.webp)
İki alt sekmesi vardır: **Resimler** ve **Videolar**. Görseller **resim setleri** (örn. "Varsayılan", "Manken", "Ürün")
altında tutulur; hiç set tanımlı değilse "Henüz resim seti tanımlanmamış. Katalog Ayarları sayfasından resim seti
oluşturun." uyarısı görünür.

**Resimler alt sekmesi**

| Öğe | Açıklama |
|---|---|
| Set: çipleri | Yükleme yapılacak resim seti; varsayılan set seçili gelir. |
| Ana Varyant: çipleri | Grupta birincil eksen (renk) varsa `Ürün Geneli` + her renk değeri çip olarak; seçilen renk için yüklenen resimler o rengin tüm bedenlerine aittir. |
| Varyant: listesi | Birincil eksen yoksa ama varyant varsa SKU listesi (`Ürün Geneli` seçeneğiyle). |
| Resim Yükle kartı | "Tıkla veya sürükle & bırak" alanı — JPG, PNG, WEBP; çoklu seçim. Seçilen dosyalar küçük önizleme olarak listelenir; üzerine gelince ad/boyut ve kaldırma (×) butonu çıkar. |
| Mevcut resimleri arşivle (bu set + varyant için) | Varsayılan **işaretli**. İşaretliyken yükleme onaylandığında aynı set + aynı varyant (veya ürün geneli) için mevcut aktif resimler **arşive** alınır (silinmez). |
| Yükle (N) | Yüklemeyi başlatır; ilerleme çubuğu "Yükleniyor… 3 / 5" gösterir. Bitince "Resimler başarıyla yüklendi." |
| Mevcut Resimler | Toplam adet; üstte varyant/renk sekmeleri (her sekmede resim sayısı rozeti; hiç resmi olmayan sekmede turuncu nokta "Resim yüklenmemiş"). Her set ayrı başlık altında listelenir; resmi olmayan sette "Resim yok". |
| Resim kartı | `Ana` rozeti kapak resmini gösterir (kenarlığı renkli). Üzerine gelince: ⭐ **Ana görsel yap** / ⭐ Ana görsel (kaldır) ve 🗑 **Arşivle**. |

Kurallar:
- Dosya adları otomatik verilir: `ÜRÜNKODU_SETKODU_VARYANT_xx.uzantı`; yerel dosya adı korunmaz.
- Yüklenen ilk resim, o set + varyant için henüz kapak yoksa otomatik **Ana** (kapak) olur; varyanta yüklemede aynı
  zamanda varyant kapağı olur.
- Arşivlenen resim sitede görünmez ama kaybolmaz; ileride panelden geri alınabilir (bu ekranda geri alma butonu yoktur).
- Sitede görseller kart/detay/zoom boyutlarında otomatik sunulur; ayrıca boyut üretmeniz gerekmez. Yüksek çözünürlüklü
  dikey (ürün) fotoğraf yüklemeniz yeterlidir.
- Üst başlıktaki kapak görseli, varyant kapaklarından türetilir ve yükleme sonrası bir sonraki sayfa açılışında güncellenir.

**Videolar alt sekmesi**
![Görseller sekmesi — Videolar alt sekmesi: video yükleme, URL ile video ekleme ve mevcut videolar](img/catalog-products-detay--videolar-sekmesi.webp)

| Öğe | Açıklama |
|---|---|
| Set: çipleri | Videonun bağlanacağı set. |
| Video Yükle kartı | "Tıkla veya sürükle & bırak" — MP4, WEBM, MOV; çoklu seçim. Seçilenler ad + MB boyutuyla listelenir, × ile kaldırılır. |
| Mevcut videoları arşivle (bu set için) | Varsayılan **işaretsiz**. İşaretlenirse onay sırasında bu setin mevcut videoları arşive alınır. |
| Yükle (N) | Yüklemeyi başlatır; ilerleme çubuğu; "Videolar başarıyla yüklendi." |
| URL ile Video Ekle | `https://…/urun.mp4` gibi **doğrudan video adresi** (mp4/webm) girilip **Ekle**'ye basılır. Dosya yüklemeye alternatiftir; kayıt anında aktif olur. Geçersiz adreste "Geçerli bir video adresi girin (http/https)." hatası. |
| Mevcut Videolar | Oynatıcıyla listelenir (mp4/webm/ogg oynatılabilir; diğer uzantılarda simge). Sağdaki 🗑 **Arşivle**. Boşsa "Bu set için henüz video yüklenmemiş." |

Ürün videosu, sitede ürün kartında video rozeti ve detay galerisinde video karesi olarak görünür.

### Etiketler
![Etiketler sekmesi — etiket çipleri ve giriş kutusu](img/catalog-products-detay--etiketler-sekmesi.webp)
Etiketler arama, filtreleme ve segmentasyonda kullanılır.

| Alan | Zorunlu | Açıklama |
|---|---|---|
| Etiket kutusu | Hayır | Yazıp **Enter** ya da **virgül** ile eklenir. Küçük harfe çevrilir, boşluklar `-` olur (örn. `Yaz Koleksiyonu` → `yaz-koleksiyonu`). Aynı etiket ikinci kez eklenmez. Kutu boşken **Backspace** son etiketi siler; çipteki × ile tek etiket kaldırılır. Kutudan çıkınca yarım kalan metin de etikete dönüşür. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Kart başlığı sağı | Etiket listesini kaydeder; `Kaydedildi` / `Hata oluştu`. | Listede değişiklik olmalı (yoksa pasif). |

### SEO
![SEO sekmesi — URL slug, dil sekmeli meta alanları ve Google önizleme](img/catalog-products-detay--seo-sekmesi.webp)

| Alan | Zorunlu | Açıklama / kurallar |
|---|---|---|
| URL Slug | Hayır | Ürün sayfasının adresi: `/urun/<slug>`. Boşken Türkçe addan **otomatik** üretilir (Türkçe karakterler dönüştürülür, yalnız küçük harf-rakam-tire) ve sağda `Otomatik` etiketi görünür. Elle yazılınca yalnız `a-z`, `0-9`, `-` kabul edilir; **Otomatiğe döndür** bağlantısıyla tekrar otomatiğe alınır. Mağaza genelinde benzersiz olmalıdır. |
| Meta Başlık | Hayır | Dil sekmeli; sayaç `n/60`, 60'ı aşınca kırmızı. Boşsa ürün adı kullanılır. |
| Meta Açıklama | Hayır | Dil sekmeli; sayaç `n/160`. Boşsa kısa açıklama kullanılır. |
| Anahtar Kelimeler | Hayır | Virgülle ayrılmış liste. |

- Dil sekmelerinde (TR/EN…) o dilde meta başlık ya da açıklama girildiyse küçük bir nokta görünür.
- Sağdaki **Google Önizleme** kartı, seçili dil için adres, başlık ve açıklamanın arama sonucunda nasıl görüneceğini ve
  uzunluk sayaçlarını gösterir.

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Formun altı | Slug ve meta alanlarını kaydeder. Başarısızsa "Hata — slug çakışıyor olabilir" (sunucu mesajları: "Bu slug başka bir ürün tarafından kullanılıyor." / "Geçerli bir URL üretilemedi — slug harf/rakam içermeli."). | — |
| Otomatiğe döndür | URL Slug başlığı | Elle girilen slug'ı siler, otomatik slug'a döner. | Slug elle girilmiş olmalı. |

## Durumlar ve iş kuralları
| Durum / kural | Açıklama |
|---|---|
| `Satışta` / `Satış Kapalı` | Ürünün **genel** satış anahtarı; kapalıyken hiçbir kanalda satılmaz. Yeni ürün kapalı doğar. Tehlikeli Alan'dan değişir. |
| Varyant `Aktif` / `Pasif` | Pasif varyant sitede seçilemez/satılamaz; Varyantlar sekmesinde rozete tıklayarak değişir. |
| Kanal `AKTİF` kutusu | Varyantın o kanalda satılıp satılmayacağı; ürün genel anahtarı açık olsa bile kanalda kapatılabilir. |
| Satılabilir birim = varyant | Sepet, stok ve sipariş her zaman varyanta bağlıdır. Eksensiz grupta varsayılan varyant otomatik açılır; eksenli grupta varyant eklenene kadar ürün taslaktır. |
| Varsayılan varyant ↔ kombinasyon | Özniteliksiz varsayılan varyant ile öznitelikli varyant aynı anda aktif olamaz; ilk kombinasyon eklenince varsayılan pasife çekilir. |
| Fiyat sahipliği | Kanal fiyatı ve sitede gösterilen fiyat **varyant** fiyatından okunur; varyant fiyatı 0 ise ürünün satış fiyatı kullanılır. Tek varsayılan varyantlı üründe Genel'deki satış fiyatı varyanta senkron yazılır. |
| Fiyat geçmişi | Ürün satış/alış fiyatı, varyant fiyatı ve kanal fiyatı değişiklikleri tarih/kişi ile kaydedilir; Genel → Fiyat Geçmişi'nde görülür. |
| Barkod | Elle girilebilir ya da "Barkod Oluştur" ile EAN-13 üretilir; aynı üründe ikisi karışık olabilir. Barkod benzersizdir. |
| Görsel durumu | Yüklenen resim/video **aktif**; "arşivle" ile **arşiv**e alınır (silinmez); yarım kalan yükleme iptal sayılır. |

## Adım adım

**Yeni üründe varyant + barkod + fiyat tamamlama**
1. **Genel** sekmesinde satış fiyatı ve KDV'yi girip üstteki **Kaydet**'e basın.
2. **Varyantlar** sekmesine geçin, **+ Varyant Ekle**'ye tıklayın; renk ve beden değerlerini seçin, "N Kombinasyon Ekle"ye basın.
3. Farklı fiyatlı varyant varsa **FİYAT (₺)** hücresini düzenleyin; alandan çıkınca kaydedilir.
4. **Barkod Oluştur**'a basın; boş barkodlar otomatik dolar. Elinizdeki barkodları ilgili hücrelere yazabilirsiniz.
5. **Görseller** sekmesinde renk çipini seçip resimleri yükleyin; ilk resim kapak olur.
6. **Satış Kanalları** sekmesinde kanalı seçip gerekirse fiyat tipini/liste fiyatını girin, **Tümünü Kaydet**'e basın.
7. **Genel** sekmesi → Tehlikeli Alan → **Satışa Aç**.

**Bir kanalda tüm varyantlara %20 zamlı fiyat verme**
1. **Satış Kanalları** sekmesinde ilgili kanal çipini seçin.
2. "Toplu fiyat" şeridinde tipi **Çarpan**, değeri `1.2` yazın, **Tüm Varyantlara Uygula**'ya basın.
3. Satırları kontrol edin (ana fiyatın altına düşen varsa sarı uyarı görünür) ve **Tümünü Kaydet**'e basın.

**Ürünü geçici olarak satıştan kaldırma**
1. **Genel** → sağdaki **Tehlikeli Alan** → **Satışı Kapat**.
2. Açılan pencerede **Satışı Kapat**'ı onaylayın; rozet `Satış Kapalı` olur, tüm kanallarda satış durur.
3. Tek bir kanalda durdurmak içinse **Satış Kanalları** sekmesinde ilgili satırların **AKTİF** kutusunu kaldırıp kaydedin.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Üst başlıktaki **Kaydet** yalnızca Genel sekmesini kaydeder. Özellikler, Alt Özellikler, Etiketler, SEO ve
> kanal fiyatlarının kendi Kaydet butonları vardır; Varyantlar sekmesindeki barkod/fiyat hücreleri ise alandan çıkınca
> kendiliğinden kaydedilir.

> **Dikkat:** "Ürünü Sil" ve "Varyantı Sil" geri alınamaz. Satışı durdurmak istiyorsanız silmek yerine **Satışı Kapat**
> ya da varyantı **Pasif** yapın; geçmiş sipariş ve stok kayıtları bu kayıtlara bağlıdır.

> **Dikkat:** "Mevcut resimleri arşivle" kutusu varsayılan olarak işaretlidir. Mevcut resimlere **ek** resim yüklemek
> istiyorsanız yüklemeden önce kutunun işaretini kaldırın; aksi halde aynı set + varyanttaki eski resimler arşive gider.

> **Not:** "+ Varyant Ekle" butonu görünmüyorsa ürün grubunda varyant ekseni tanımlı değildir. Ürün Grupları sayfasından
> gruba eksen ekleyin ya da eksensiz ürün için "Default Varyant Oluştur"u kullanın.

> **Not:** Tedarikçi listesi boşsa Cari modülünde "Tedarikçi" tipinde aktif cari hesap yoktur; önce orada oluşturun.

> **Not:** Görseller sekmesi "Henüz resim seti tanımlanmamış" diyorsa Katalog Ayarları'ndan en az bir resim seti
> (biri varsayılan) tanımlanmalıdır.

## İlgili sayfalar
- [Ürün Kartları](/rehber/katalog/urun-kartlari/)
- [Yeni Ürün Kartı](/rehber/katalog/urun-olusturma/)
- [Toplu Resim Yükleme](/rehber/katalog/toplu-resim-yukleme/)
- [Tedarikçi Gönderimleri](/rehber/katalog/tedarikci-gonderimleri/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/)
- [Katalog Ayarları](/rehber/katalog/katalog-ayarlari/)
- [Stoklar](/rehber/stok/stok-takibi/)
- [Cari Hesaplar](/rehber/cari/cari-kartlar/)
