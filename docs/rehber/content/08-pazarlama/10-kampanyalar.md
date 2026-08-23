---
title: Kampanyalar
route: /promotion/campaigns
group: Pazarlama
order: 10
summary: Kanal bazlı kampanyaların (indirim, al X öde Y, kargo vb.) listelendiği, oluşturulduğu, düzenlendiği ve kopyalandığı ekran; detay sayfası Genel / Parametreler / Ürün Kapsamı sekmelerinden oluşur.
---

## Ne işe yarar
Kampanyalar, mağazanızın bir kanalında belirli tarihler arasında geçerli olan indirim kurallarıdır. Pazarlama ekibi
burada bir **kampanya tipi** seçer (ör. "İndirim", "Al X, Y Bedava/İndirimli"), tipin parametrelerini doldurur,
kampanyanın hangi ürünleri kapsayacağını belirler ve kampanyayı yayına alır. Kampanya aktifken sitede ürün
kartlarında/detayda kampanya bandı (rozet) görünür, ürün-bazlı indirimler kampanyalı fiyat olarak gösterilir ve
sepet/ödeme adımında indirim satırı olarak hesaplanır. Kampanyalar kupon akışından ayrıdır; müşteri kod girmez,
koşullar sağlanınca kendiliğinden uygulanır.

## Ekran yerleşimi
![Kampanyalar listesi](img/promotion-campaigns.webp)
1. **Başlık ve sayaç** — "Kampanyalar" ve listedeki kayıt sayısı; sağda **+ Yeni Kampanya** butonu.
2. **Sekmeler** — `Yayında` / `Tümü`.
3. **Tablo** — kampanya satırları; satıra tıklayınca detay sayfası açılır.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Kampanyanın benzersiz kodu (oluşturulduktan sonra değiştirilemez). |
| AD | Kampanya adı (Türkçe). |
| TİP | Seçilen kampanya tipinin adı (bkz. [Kampanya Tipleri](/rehber/pazarlama/kampanya-tipleri/)). |
| KAPSAM | Ürün doldurma tipi: `Tüm ürünler`, `Manuel`, `Filtre`, `Karma`. |
| TARİH | Başlangıç → bitiş; bitiş boşsa `süresiz`. |
| ÖNCELİK | Sayısal öncelik; aynı ürüne birden çok kampanya denk gelirse yüksek olan kazanır. |
| DURUM | `Aktif` (yeşil) / `Pasif` (gri) rozeti — "Aktif" kutusunun durumu. |
| (son sütun) | "Düzenle →" ipucu; satırın tamamı tıklanabilir. |

| Sekme / Filtre | Ne yapar |
|---|---|
| `Yayında` | Yalnız **şu an yayında** olanları listeler: Aktif işaretli **ve** başlangıç tarihi geçmiş **ve** bitişi dolmamış kampanyalar. |
| `Tümü` | Tüm kampanyalar (pasif, süresi dolmuş ve ileri tarihli olanlar dahil). Varsayılan sekmedir. |

Liste önceliğe göre (yüksekten düşüğe) sıralanır; sayfalama yoktur. Liste tüm kanalların kampanyalarını birlikte
gösterir; kampanyanın kanalı detay sayfasındaki **Platform** alanında görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Kampanya | Liste sağ üst | Boş detay sayfası ("Yeni Kampanya") açılır. | Panele giriş yeterli. |
| Satır tıklama | Tablo | Kampanyanın detay sayfası açılır (`/promotion/campaigns/{id}`). | — |
| Kaydet | Detay sağ üst | Üç sekmedeki bilgiler birlikte kaydedilir; başarılıysa listeye dönülür. Hata varsa sayfanın üstünde kırmızı mesaj çıkar (ör. "'X' kampanya kodu zaten mevcut.", "Platform seçilmedi.", "'Eşik değeri' alanı zorunlu."). | Platform, Kampanya Tipi, Kod ve Ad dolu olmalı; tipin zorunlu parametreleri dolu olmalı. |
| Kopyala | Detay sağ üst (yalnız kayıtlı kampanyada) | Yeni kampanya kodu sorulur (öneri: `KOD-KOPYA`); kampanyanın ayarları, parametreleri, kapsamı ve manuel ürün listesi kopyalanır, kopya **Pasif** olarak açılır ve detayına gidilir. | Yeni kod benzersiz olmalı. |
| Ekle | Ürün Kapsamı → Manuel Ürünler | Kutuya yazılan ürün kodlarını (virgül/boşluk/noktalı virgülle ayrılmış) listeye ekler; bulunamayan kodlar kırmızı uyarıyla listelenir. | Doldurma tipi Manuel veya Karma. |
| Dosyadan Yükle | Ürün Kapsamı → Manuel Ürünler | `.txt`/`.csv` (kodlar virgül, boşluk ya da satır sonuyla ayrılmış) veya `.xlsx` (kodlar ilk sütunda) dosyasından ürün kodlarını ekler. | Doldurma tipi Manuel veya Karma. |
| Kaldır | Manuel ürün satırı | Ürünü kapsam listesinden çıkarır (Kaydet'e kadar kalıcı değildir). | — |
| Bant Rengi çipleri | Genel sekmesi | Sitedeki kampanya bandının/rozetinin arka plan rengini seçer. | — |

> **Dikkat:** Kampanya silme butonu yoktur. Kampanyayı kaldırmak için Genel sekmesinde **Aktif** kutusunu
> kaldırıp kaydedin ya da bitiş tarihi verin.

## Detay sayfası
![Kampanya detayı — Genel sekmesi](img/promotion-campaigns-detay.webp)
*(1) Başlık: "Yeni Kampanya" ya da "Kampanya: KOD" + seçili tipin adı · (2) Kopyala / Kaydet · (3) Sekmeler · (4) Form kartı*

Sekmeler: **Genel**, **Parametreler**, **Ürün Kapsamı**. Ürün Kapsamı sekmesi yalnız seçilen tip ürün seçimi
gerektiriyorsa görünür (ör. Kargo Kampanyası ve Resimli Yorum Kampanyası'nda görünmez).

### Genel
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Platform | Evet | Kampanyanın geçerli olduğu kanal. Liste "Kanal adı (Firma adı)" biçimindedir; bir kampanya tek kanala aittir. |
| Kampanya Tipi | Evet | Tip listesi Kampanya Tipleri ekranından gelir. Tip değiştirilince Parametreler sıfırlanır. |
| Kod | Evet | Benzersiz kampanya kodu (ör. `YAZ25`). Yalnız yeni kampanyada yazılabilir; kayıt sonrası kilitlenir. |
| Ad | Evet | Kampanyanın görünen adı (ör. "Yaz İndirimi %25"). Sitede bant metni olarak da kullanılır. |
| Açıklama | Hayır | Serbest açıklama. |
| Etiket/Rozet | Hayır | Sitede kartta görünecek kısa etiket (ör. "Süper Fırsat"). |
| Öncelik | Hayır | Tam sayı; büyük değer önceliklidir. Varsayılan 0. |
| Bant Rengi | Hayır | Renk çipleri: Varsayılan (marka rengi), Kırmızı — indirim, Turuncu — fırsat, Amber — sınırlı süre, Yeşil — kargo, Mavi — sepet/bilgi, Mor — özel, Pembe — sezon, Siyah — premium. |
| Başlangıç | Evet | Kampanyanın başladığı gün (varsayılan bugün). |
| Bitiş (boş = süresiz) | Hayır | Son geçerli gün; boşsa süresiz. |
| Aktif | — | İşaretli değilse kampanya tarih aralığında bile uygulanmaz. Yeni kampanyada varsayılan işaretli. |

### Parametreler
Alanlar seçilen tipin şablonundan üretilir; etiketlerin yanında birim (`adet`, `₺`) ve zorunlu yıldızı görünür,
bazı alanlar başka bir alanın değerine göre gizlenir/gösterilir. Kaydet'te zorunlu alanlar sunucuda da doğrulanır.
Tip seçilmeden sekme "Önce Genel sekmesinden kampanya tipi seçin." der.

| Tip | Alanlar |
|---|---|
| İndirim (Kapsam+Koşul+Fayda) | İndirim nereye (Sepet toplamına / Kapsamdaki ürünlere) · Koşul (eşik) (Koşulsuz / Sepet tutarı ≥ / Sepet adedi ≥ / Kapsam tutarı ≥ / Kapsam adedi ≥) · Eşik değeri (koşul varsa) · İndirim şekli (Yüzde / Tutar) · İndirim değeri · En çok indirim (₺) (yalnız yüzdede, isteğe bağlı tavan). |
| Al X, Y Bedava/İndirimli | Tam fiyat ödenecek adet (X) · İndirimli/bedava adet (Y) · Y ürünlerine uygulanan (Bedava %100 / Yüzde indirim / Sabit fiyat-tutar) · Y indirim değeri (bedava değilse) · Aynı üründen olmalı (Evet/Hayır) · En ucuz olan indirimli (Evet/Hayır). Örn. 3 al 2 öde → X=2, Y=1; 1 alana 1 bedava → X=1, Y=1; ikincisi %50 → X=1, Y=1, Yüzde 50. |
| Grup Al → Grup Hediye/İndirimli | Alım koşulu (Adet ≥ / Tutar ≥) · Alım eşiği · Hediye/indirimli adet · Hediye grubuna uygulanan (Bedava / Yüzde / Tutar) · Hediye indirim değeri. |
| Kombin İndirimi | Kombin minimum ürün (en az 2) · Kombin fiyatı (Sabit paket fiyatı / Yüzde indirim / Tutar indirim) · Kombin değeri. |
| Kargo Kampanyası | Koşul (Koşulsuz / Sepet tutarı ≥) · Sepet eşiği (₺) · Ödeme yöntemi kısıtı (Tümü / Kredi kartı) · Kargo indirimi (Ücretsiz / Yüzde / Tutar) · İndirim değeri. |
| Resimli Yorum Kampanyası | Ödül (Kupon kodu / Sonraki alışverişe % / Sonraki alışverişe ₺) · Ödül değeri. |

Alan türleri: sayısal alanlar sayı kutusu, Evet/Hayır alanları onay kutusu ("Evet"), seçim alanları açılır liste.
Alanın altında varsa yardım metni görünür (ör. "Yüzde indirimde tavan tutar (opsiyonel).").

### Ürün Kapsamı
| Alan | Zorunlu | Açıklama / kurallar |
|---|---|---|
| Doldurma tipi | Evet | `Tüm ürünler` (kampanya kanalın tüm ürünlerine uygulanır) · `Manuel — ürünler elle eklenir` · `Filtre — kural tabanlı` · `Karma — filtre + manuel`. Kategori ekranındaki ürün ilişkilendirmeyle aynı mantıktır. |
| FİLTRE KURALLARI | Filtre/Karma'da | Kural oluşturucu bölümleri: Ürün Grupları, Tedarikçi, Temel Fiyat, Platform Fiyatı, Min. İndirim, KDV Oranı, Ürün Durumu, Stok Miktarı, Oluşturma Tarihi, Resim Güncelleme Tarihi, Etiketler, Özellik Filtreleri (renk, beden, cinsiyet vb.). Altta "Otomatik açıklama" satırı kuralı özetler. |
| MANUEL ÜRÜNLER (n) | Manuel/Karma'da | Kod kutusu + **Ekle**, **Dosyadan Yükle**; liste satırında ürün adı + kodu ve **Kaldır**. Bulunamayan kodlar "Bulunamayan kodlar (n): …" uyarısıyla gösterilir. |

Kapsam kaydedildiğinde filtreye uyan ürünler kampanyaya bağlanır; kapsam dışı ürünlerde kampanya görünmez.

## Durumlar ve iş kuralları
- **Yayında olma koşulu:** `Aktif` işaretli + başlangıç ≤ bugün + (bitiş boş ya da ≥ bugün). Bu üçü sağlanmadan
  kampanya sitede görünmez ve hesaplanmaz.
- **Tek kanal:** kampanya yalnız seçilen Platform'da geçerlidir. Başka kanalda aynı kampanya için **Kopyala** kullanıp
  kopyada Platform'u değiştirin.
- **Ürün başına tek etkin kampanya:** bir ürüne kapsamı uyan birden çok kampanya varsa **en yüksek öncelikli** olan
  seçilir; böylece çift indirim olmaz. Öncelik eşitse sıra garanti edilmez — farklı öncelik verin.
- **Ürün-bazlı fiyat mı, sepette mi?** "İndirim" tipinde *Kapsamdaki ürünlere* + *Koşulsuz* + yüzde/tutar seçildiğinde
  kampanyalı birim fiyat kartta ve ürün detayında gösterilir ve sipariş kalemine yansır. Diğer tüm durumlar
  (al X öde Y, koşullu/sepet toplamına indirimler vb.) kartta yalnız kampanya bandı olarak görünür; indirim sepette
  hesaplanır ve sepet/ödeme özetinde "Kampanya İndirimi" satırı olarak çıkar.
- **Sepet seviyesi indirimin kalemlere dağıtımı:** sepette hesaplanan kampanya indirimi, kapsama giren sepet
  kalemlerine satır tutarı oranında (ağırlıklı) dağıtılır ve sipariş kalemlerine yazılır. Böylece iade tutarı kalem
  bazında gerçekten ödenen fiyattan hesaplanır. "En ucuz olan indirimli" ayarı yalnız toplam indirim tutarını
  belirler, dağıtımı değiştirmez.
- **Kopya pasif başlar:** Kopyala ile üretilen kampanya `Pasif` açılır; kontrol edip Aktif işaretleyin.
- **Kod değişmez:** kampanya kodu kayıt sonrası kilitlidir; yanlış kodla kaydedildiyse Kopyala ile yeni kod verip
  eskisini pasife alın.
- **Kupon ile ilişki:** kampanyalar kuponlardan bağımsızdır; sepette ikisi de varsa ikisi de kendi kuralına göre
  hesaplanır ve ayrı satırlarda gösterilir.

## Adım adım
**Seçili ürünlere %25 indirim kampanyası**
1. **+ Yeni Kampanya** → Genel: Platform'u seçin, Kampanya Tipi "İndirim (Kapsam+Koşul+Fayda)", Kod `YAZ25`,
   Ad "Yaz İndirimi %25", Etiket "Süper Fırsat", Bant Rengi Kırmızı, tarihleri girin, Aktif işaretli kalsın.
2. Parametreler: İndirim nereye = Kapsamdaki ürünlere, Koşul = Koşulsuz, İndirim şekli = Yüzde, İndirim değeri `25`.
3. Ürün Kapsamı: Doldurma tipi = Filtre → Ürün Grupları'ndan "Elbise" seçin (ya da Manuel ile kodları yapıştırın).
4. **Kaydet**. Listede `Yayında` sekmesinde görünmeli; sitede kartlarda kampanyalı fiyat ve kırmızı bant çıkar.

**3 al 2 öde kampanyası**
1. Tip "Al X, Y Bedava/İndirimli" seçin; Parametreler: X = `2`, Y = `1`, Y ürünlerine uygulanan = Bedava, Aynı
   üründen olmalı ve En ucuz olan indirimli işaretli kalsın.
2. Ürün Kapsamı'nda ürünleri belirleyin, kaydedin. Kartta yalnız bant görünür; indirim sepette 3 ürün eklenince
   "Kampanya İndirimi" satırı olarak düşer.

**Kampanyayı başka kanala taşıma**
1. Kampanyayı açın → **Kopyala** → yeni kod yazın.
2. Açılan kopyada Platform'u değiştirin, tarihleri kontrol edin, **Aktif** işaretleyip **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** `Yayında` sekmesi boş ama kampanya listede `Aktif` görünüyorsa başlangıç tarihi ileri ya da bitiş
> geçmiş demektir — TARİH sütununu kontrol edin.

> **Dikkat:** "'… ' alanı zorunlu." mesajı Parametreler sekmesindeki bir alanın boş olduğunu söyler; sekmeye dönüp
> doldurun. "Platform seçilmedi." mesajında Genel sekmesinden kanal seçin.

> **Dikkat:** Kapsamı "Tüm ürünler" olan yüksek öncelikli bir kampanya, aynı kanaldaki diğer ürün kampanyalarını
> gölgeler (ürün başına tek kampanya). Genel kampanyalara düşük, özel kampanyalara yüksek öncelik verin.

> **Not:** Dosyadan yüklemede Excel'in yalnız **ilk sütunu** okunur; başlık satırı varsa "Bulunamayan kodlar"
> listesinde görünür, sorun değildir.

## İlgili sayfalar
- [Kampanya Tipleri](/rehber/pazarlama/kampanya-tipleri/)
- [Kuponlar](/rehber/pazarlama/kuponlar/)
- [Hediye Kartı](/rehber/pazarlama/hediye-karti/)
