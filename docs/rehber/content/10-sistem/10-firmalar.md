---
title: Firmalar
route: /settings/firms
group: Sistem
order: 10
summary: Paneldeki firmaların (tüzel kişilik) listesi, firma bilgileri, firmaya bağlı satış kanalları ve dış servis entegrasyonlarının (kargo, SMS, e-posta, ödeme, takip servisleri vb.) yönetildiği ekran.
---

## Ne işe yarar
Firma, panelde tüm yapının çatısıdır: satış kanalları (web sitesi, pazaryeri mağazası, mobil uygulama, mağaza/POS) ve dış servis sözleşmeleri (kargo, SMS, e-posta, ödeme, görsel arama, reklam/analitik takibi) bir firmaya bağlıdır. Bu ekranı sistem yöneticisi kullanır: yeni firma açmak, vergi/iletişim bilgilerini güncellemek, firmanın kanallarını görmek ve **Entegrasyonlar** bölümünden servis kimlik bilgilerini girmek için. Günlük operasyonda bir firmanın bir servisle yeni sözleşme yapması (örneğin yeni bir kargo firması) bu ekrandan başlar; kargo bölgeleri, kanal ödeme yöntemleri gibi diğer ekranlar burada tanımlı entegrasyonlara dayanır.

## Ekran yerleşimi
![Firmalar listesi](img/settings-firms.webp)
1. **Başlık satırı** — "Firmalar" başlığı, kayıt sayısı ve sağda **Yeni Firma** butonu.
2. **Firma tablosu** — her satır bir firma; satıra tıklayınca firma detay sayfası açılır. Satır sonunda **Düzenle** butonu ve sağa ok işareti vardır.

![Firma detay sayfası — firma bilgileri, satış kanalları ve entegrasyonlar](img/settings-firms-detay.webp)
1. **Üst bağlantı yolu** — "Firmalar › Firma adı"; "Firmalar" bağlantısı listeye döner. Sağ üstte **← Geri** bağlantısı.
2. **Firma bilgileri kartı** — ad, rozetler (`Ana Firma`, `Aktif`/`Pasif`), kod, vergi dairesi, vergi no, telefon, e-posta, adres (salt okunur; düzenleme liste sayfasındaki **Düzenle** ile yapılır).
3. **Satış Kanalları** bölümü — firmanın kanal tablosu ve **Kanal Ekle** butonu.
4. **Entegrasyonlar** bölümü — firmanın servis sözleşmeleri tablosu ve **Entegrasyon Ekle** butonu.

## Liste ve filtreler
Liste sayfasında arama/filtre kutusu yoktur; tüm firmalar (pasifler dahil) tek tabloda listelenir. Sayfalama yoktur.

| Sütun | Anlamı |
|---|---|
| KOD | Firmanın kısa kodu (küçük harf, boşluksuz). Oluşturulduktan sonra değiştirilemez. |
| AD | Firma adı (çok dilli; tabloda Türkçe ad, yoksa ilk dolu dil gösterilir). |
| VERGİ NO | Vergi numarası; boşsa `—`. |
| TELEFON | Telefon; boşsa `—`. |
| ANA | Firma "ana firma" olarak işaretliyse `Ana` rozeti. |
| DURUM | `Aktif` / `Pasif` rozeti. |
| (son sütun) | **Düzenle** butonu (satır tıklamasından bağımsız, düzenleme penceresini açar) ve detay oku. |

Satıra tıklayınca firma detay sayfası (`/settings/firms/:id`) açılır.

### Detay sayfası — Satış Kanalları tablosu
| Sütun | Anlamı |
|---|---|
| KOD | Kanal kodu (oluşturulurken kanal adından otomatik üretilir). |
| AD | Kanal adı. |
| PLATFORM | Kanalın platform tipi (Web Sitesi, Trendyol, Mobil Uygulama vb.); pazaryeri tipiyse yanında `Pazaryeri` rozeti. |
| FİYATLAMA | `Belirtilmemiş`, `Manuel` ya da `× çarpan` (örn. `× 1.1`). |
| DURUM | `Aktif` / `Pasif`. |
| (son sütun) | **Düzenle** — kanal formunu açar. |

Boş durumda "Henüz satış kanalı eklenmemiş." yazısı görünür. Kanal formunun alanları [Satış Kanalları](/rehber/sistem/satis-kanallari/) sayfasında anlatılır; detaydan açılan form aynı formdur (firma seçimi önceden dolu gelir).

### Detay sayfası — Entegrasyonlar tablosu
| Sütun | Anlamı |
|---|---|
| SERVİS | Servis kataloğundaki servis adı ve yanında servis tipi rozeti (örn. `cargo`, `sms`, `email`, `payment`). |
| İSİM | Sözleşmeye verdiğiniz serbest ad (örn. "Yurtiçi Kargo — 2026 Sözleşmesi"); boşsa `—`. |
| PLATFORM | Kaydın kapsamı: belirli bir kanal adı ya da `Tüm platformlar` (firma geneli). |
| DÖNEM | Başlangıç – bitiş tarihi (`GG.AA.YYYY – GG.AA.YYYY`); ikisi de boşsa `—`. |
| SÖZLEŞME DURUMU | `Taslak` / `Aktif` / `Süresi Doldu` / `İptal Edildi` rozeti. |
| AKTİF | Kaydın kullanımda olup olmadığı: `Aktif` / `Pasif`. |
| (son sütun) | **Düzenle** — entegrasyon formunu açar. |

Boş durumda "Henüz entegrasyon eklenmemiş." yazısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Firma | Liste sayfası, sağ üst | "Yeni Firma" penceresi açılır. | Kod ve kaynak dildeki ad dolmadan **Oluştur** pasiftir. |
| Düzenle | Liste satırı sonu | "Firma Düzenle" penceresi açılır (kod değiştirilemez; **Aktif** kutusu yalnız burada görünür). | — |
| Satır tıklama | Liste | Firma detay sayfasına gider. | — |
| Oluştur / Kaydet | Firma penceresi alt kısmı | Firmayı kaydeder, pencere kapanır, liste yenilenir. | — |
| İptal | Pencere alt kısmı | Değişiklikleri atar, pencereyi kapatır. | — |
| ← Geri / Firmalar | Detay sayfası üstü | Listeye döner. | — |
| Kanal Ekle | Detay › Satış Kanalları | "Satış Kanalı Ekle" penceresi açılır; firma seçili gelir. | — |
| Düzenle (kanal) | Kanal satırı | "Kanal Düzenle — …" penceresi açılır. | — |
| Entegrasyon Ekle | Detay › Entegrasyonlar | "Entegrasyon Ekle" penceresi açılır. | Servis seçilmeden **Oluştur** pasiftir. |
| Düzenle (entegrasyon) | Entegrasyon satırı | "Entegrasyon Düzenle — …" penceresi açılır; servis değiştirilemez. | — |
| + Satır Ekle | Entegrasyon formu, serbest alan bölümleri | İlgili bölüme boş bir anahtar/değer satırı ekler. | — |
| ✕ (satır sil) | Serbest alan satırı sonu | O satırı formdan kaldırır (kaydedince o anahtar silinir). | — |
| ⓘ bilgi ikonu | Şema alanı etiketinin yanı | Alanın açıklamasını (değer nereden bulunur vb.) balon olarak gösterir; dışarı tıklayınca kapanır. | Yalnız açıklaması tanımlı alanlarda görünür. |

> **Not:** Firma ve entegrasyon kayıtları için silme butonu yoktur; kullanımdan kaldırmak için **Aktif** kutusunu kapatın.

## Form alanları

### Firma formu (Yeni Firma / Firma Düzenle)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (yalnız oluştururken) | Küçük harf, boşluksuz; büyük harf yazsanız da küçültülür. Örn. `main`, `firma-a`. **Sonradan değiştirilemez**; düzenleme penceresinde görünmez. |
| Vergi Dairesi | Hayır | Örn. "Kadıköy VD". |
| Vergi No | Hayır | Örn. `1234567890`. |
| Telefon | Hayır | Örn. `+90 212 000 0000`. |
| E-posta | Hayır | Örn. `info@firma.com`. |
| Adres | Hayır | Tam adres (çok satırlı). |
| Ana firma | Hayır | İşaretli firma listede `Ana` rozeti alır; kanal formlarında firma adının yanında "(Ana Firma)" yazar. |
| Aktif | Hayır (yalnız düzenlerken) | Kapatılınca firma `Pasif` olur. |
| Ad (çok dilli) | Kaynak dilde evet | Alt kısımdaki dil sekmeli alan; kaynak dil (varsayılan dil) zorunlu, diğer diller boş bırakılabilir. |

### Entegrasyon formu (Entegrasyon Ekle / Düzenle)
![Entegrasyon Ekle penceresi — servis seçimi, kapsam, şifreli kimlik bilgileri ve ayarlar](img/settings-firms-detay--entegrasyonlar.webp)

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Servis | Evet (yalnız eklerken) | Aranabilir liste; Servis Kataloğu'ndaki servisler "Servis adı (tip)" biçiminde listelenir. Seçilen servisin alan şeması formun Kimlik Bilgileri / Ayarlar bölümlerini belirler. Düzenlemede değiştirilemez. |
| İsim | Hayır | Sözleşmeyi tanıyacağınız serbest ad. Örn. `Yurtiçi Kargo — 2026 Sözleşmesi`. |
| Platform | Hayır | `Tüm platformlar (firma geneli)` ya da firmanın bir kanalı. **Kanala özel kayıt, firma geneline tercih edilir.** |
| Durum | Hayır | Sözleşme durumu: Taslak (varsayılan), Aktif, Süresi Doldu, İptal Edildi. |
| Başlangıç Tarihi / Bitiş Tarihi | Hayır | Sözleşme dönemi; tabloda DÖNEM sütununda görünür. |
| Kimlik Bilgileri (API) — şema alanları | Şemada `*` işaretliyse evet | Sarı arka planlı bölüm. Servisin kataloğunda "Kimlik Bilgileri" bölümüne konmuş alanlar (API anahtarı, şifre, cari kodu vb.). **Şifreli saklanır**; kayıttan sonra değerler maskeli (`•••`) görünür. Değiştirmek için maskenin üzerine yeni değeri yazın; maskeli bırakılan alan aynen korunur. |
| Şema Dışı Kimlik Bilgileri | Hayır | Yalnız bu kayıtta, şemada tanımlı olmayan eski anahtarlar varsa görünür; anahtar/değer satırlarıyla düzenlenir. Servisin şeması yoksa bölümün adı "Kimlik Bilgileri (API)" olur ve tüm bilgiler serbest satırlarla girilir. |
| Ayarlar — şema alanları | Şemada `*` işaretliyse evet | Gri bölüm. Şifrelenmeyen ayarlar (ölçüm kimliği, kapsam seçenekleri, açık/kapalı anahtarlar vb.). Alan tipine göre metin, sayı, tarih, şifre kutusu ya da onay kutusu görünür. |
| Şema Dışı Ayarlar | Hayır | Şemada olmayan mevcut anahtarlar; şemasız serviste bölümün adı "Ayarlar" olur. |
| Sözleşme Şartları | Hayır | Serbest anahtar/değer satırları: komisyon %, desi fiyatı, mesaj birim ücreti gibi servis tipine göre değişen ticari şartlar. |
| Aktif | Hayır (yalnız düzenlerken) | Kapalıysa kayıt kullanılmaz (örneğin kargo seçeneklerinde ve kargo bölgelerinde görünmez). |

Doğrulama: zorunlu (`*`) şema alanlarından biri boşsa kayıt yapılmaz ve formun altında kırmızı **"Zorunlu alan(lar) boş: …"** mesajı alan adlarıyla birlikte görünür (aynı kontrol sunucuda da yapılır). Onay kutusu tipindeki alanlar zorunlu sayılmaz.

## Durumlar ve iş kuralları
- **Firma durumu:** `Aktif` / `Pasif` (Ana firma rozeti ayrıca).
- **Sözleşme durumu** bilgilendirme amaçlıdır: `Taslak` → `Aktif` → `Süresi Doldu` ya da `İptal Edildi`; geçişler elle yapılır, otomatik değişmez. Servisin kullanılıp kullanılmayacağını **Aktif** kutusu belirler.
- **Kapsam çözümleme:** bir kanal için servis aranırken önce o kanala özel kayıt, yoksa firma geneli (`Tüm platformlar`) kayıt kullanılır. Örneğin SMS servisi firma geneli tanımlanır, belirli bir kanalın farklı kargo sözleşmesi kanala özel eklenir.
- **Aynı servise birden çok sözleşme** açılabilir (örneğin aynı kargo firmasıyla tahsilatlı ve tahsilatsız iki ayrı cari); **İsim** alanı bunları ayırt etmek içindir.
- **Kimlik bilgileri** veritabanında şifreli tutulur; ekranda hiçbir zaman açık görünmez. Maskeyi değiştirmeden kaydetmek saklı değeri korur.
- **Kargo bölgeleri bağı:** firmada aktif kargo entegrasyonu yoksa Kargo Bölgeleri ekranı sarı uyarıyla "önce firma entegrasyonları sayfasından bir kargo servisi ekleyin" der ve buraya bağlantı verir.
- **Takip/reklam servisleri** (GA4, Google Ads, Meta, TikTok vb.) de birer entegrasyon kaydıdır; Pazarlama › Takip & Reklam ekranı kimlik bilgilerinin buradan girilmesini ister.
- Servis listesi **Servis Kataloğu**'ndan gelir; kataloğa yalnız platform yönetimi servis ekleyebilir.

## Adım adım
### Yeni firma oluşturma
1. Sistem › **Firmalar** sayfasında **Yeni Firma**'ya tıklayın.
2. **Kod** girin (küçük harf, boşluksuz), alt kısımda kaynak dilde **Ad** yazın.
3. Gerekirse vergi/iletişim bilgilerini doldurun, "Ana firma" ise işaretleyin.
4. **Oluştur**'a tıklayın. Firma listede görünür; satıra tıklayarak detayına geçin.

### Firmaya kargo (ya da başka bir servis) entegrasyonu ekleme
1. Firma satırına tıklayıp detay sayfasını açın; **Entegrasyonlar** bölümünde **Entegrasyon Ekle**'ye tıklayın.
2. **Servis** listesinden servisi seçin (örn. "Yurtiçi Kargo (cargo)"). Form, servisin alanlarıyla yeniden çizilir.
3. **İsim** verin; **Platform**'u seçin (firma geneli ya da tek kanal); **Durum** ve sözleşme tarihlerini girin.
4. Sarı **Kimlik Bilgileri (API)** bölümündeki zorunlu alanları ve **Ayarlar** alanlarını doldurun; ⓘ ikonundan alan açıklamalarını okuyun.
5. Gerekirse **Sözleşme Şartları**'na komisyon/desi gibi satırlar ekleyin.
6. **Oluştur**'a tıklayın. Hata mesajı gelirse eksik alanları tamamlayın.
7. Kargo servisi eklediyseniz Sipariş Yönetimi › Kargo Bölgeleri ekranında öncelik sırasını düzenleyin.

### Kimlik bilgisini (API anahtarı/şifre) değiştirme
1. Entegrasyon satırında **Düzenle**'ye tıklayın.
2. Maskeli (`•••`) görünen alanın üzerine yeni değeri yazın; değiştirmeyeceğiniz alanlara dokunmayın.
3. **Kaydet**'e tıklayın. Dokunulmayan maskeli alanlar eski değerini korur.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Servis seçilince form sıfırdan çizilir; servisi yanlış seçtiyseniz **Servis** listesinden değiştirin — girilen şema değerleri yeni şemaya göre yeniden başlatılır.

> **İpucu:** Platform alanında "Tüm platformlar (firma geneli)" seçmek çoğu servis için yeterlidir; yalnız kanala özel sözleşme/kimlik gerekiyorsa kanal seçin.

> **Dikkat:** "Zorunlu alan(lar) boş: …" mesajı alan etiketleriyle gelir; onay kutuları bu kontrole girmez. Mesaj sunucudan da gelebilir (aynı kural).

> **Dikkat:** Kimlik bilgileri şifreli saklandığı için unutulan bir anahtar ekrandan geri okunamaz; değeri servis sağlayıcıdan yeniden alıp üzerine yazmanız gerekir.

> **Not:** "Entegrasyon servisi bulunamadı." / "Platform bulunamadı veya bu firmaya ait değil." hataları, seçilen servis kataloğundan kaldırılmışsa ya da seçilen kanal başka firmaya aitse görülür; sayfayı yenileyip yeniden deneyin.

## İlgili sayfalar
- [Satış Kanalları](/rehber/sistem/satis-kanallari/)
- [Platform Tipleri](/rehber/sistem/platform-tipleri/)
- [Servis Kataloğu](/rehber/sistem/servis-katalogu/)
- [Bildirim Şablonları](/rehber/sistem/bildirim-sablonlari/)
- [Kargo Bölgeleri](/rehber/siparis/kargo-bolgeleri/)
- [Takip & Reklam](/rehber/pazarlama/takip-ve-reklam/)
