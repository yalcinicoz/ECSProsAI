---
title: Proje Talepleri
route: /requests
group: Genel
order: 20
summary: Personelin proje ile ilgili isteklerini (yeni özellik, hata, iyileştirme, veri işi) girdiği, atadığı, durumunu izlediği ve yorumla takip ettiği talep listesi + talep detayı.
---

## Ne işe yarar
Proje Talepleri, panel kullanan tüm personelin yazılım/proje ekibinden istediklerini tek yerde toplar: yeni bir özellik
isteği, fark edilen bir hata, bir iyileştirme önerisi ya da bir veri işi. Talep açan kişi isteğini yazar, ek dosya
(ekran görüntüsü, PDF) iliştirir; değerlendiren kişi talebi bir sorumluya atar, termin verir ve durumunu adım adım
ilerletir. Her talep `TLP-2026-0001` biçiminde bir numara alır; yorumlar, atama ve durum değişiklikleri tek bir
**Süreç Akışı** altında zaman sırasıyla görünür. Bu sürümde özel bir yetki aranmaz — panele girebilen herkes talep
görür, açar, günceller ve durum değiştirebilir.

## Ekran yerleşimi
![Proje Talepleri listesi — durum sekmeleri, filtre şeridi ve tablo](img/requests.webp)
*(1) Başlık ve + Yeni Talep · (2) Durum sekmeleri (sayaçlı) · (3) Filtre şeridi · (4) Tablo · (5) Sayfalama*

1. **Başlık satırı** — "Proje Talepleri" başlığı, altında "Personelin proje ile ilgili istekleri — girin, izleyin,
   güncelleyin" açıklaması; sağda **+ Yeni Talep** butonu.
2. **Durum sekmeleri** — `Tümü` ve sekiz durum sekmesi; her sekmenin yanında parantez içinde o durumdaki talep sayısı.
   Dar ekranda sekme şeridi yatay kaydırılır.
3. **Filtre şeridi** — Kategori açılır listesi, Öncelik açılır listesi, **Herkes / Benim taleplerim / Bana atananlar**
   üçlü düğmesi, arama kutusu ve **Ara** butonu.
4. **Tablo** — talepler, en yeni en üstte.
5. **Sayfalama** — tablo altında `← Önceki  1 / N  Sonraki →` (yalnız birden çok sayfa varsa görünür).

![Yeni Talep penceresi](img/requests--yeni-talep-modal.webp)

## Liste ve filtreler
**Sütunlar**

| Sütun | Anlamı |
|---|---|
| KOD | Talep numarası (`TLP-yyyy-nnnn`), sabit genişlikli yazıyla. |
| BAŞLIK | Talebin kısa özeti. Yorum varsa yanında 💬 ve yorum sayısı görünür. |
| KATEGORİ | Yeni Özellik / Hata / İyileştirme / Veri İşi / Diğer. |
| ÖNCELİK | Rozet: `Düşük` (gri) · `Normal` (mavi) · `Yüksek` (turuncu) · `Kritik` (kırmızı). |
| DURUM | Durum rozeti (aşağıda "Durumlar ve iş kuralları"). |
| TALEP EDEN | Talebi açan kullanıcının adı (otomatik; girişteki hesabınızdan alınır). |
| ATANAN | Sorumlu kişi; atanmamışsa `—`. |
| TERMİN | Hedef tarih (gg.aa.yyyy); boşsa `—`. Tarih geçmişse ve talep kapanmamışsa **kırmızı kalın** ve yanında ⚠ görünür. |
| TARİH | Talebin oluşturulduğu gün. |

**Durum sekmeleri** (`Tümü` + sekiz durum): `Yeni`, `Değerlendirmede`, `Planlandı`, `Yapılıyor`, `Testte`, `Tamamlandı`,
`Reddedildi`, `İptal`. Sekme sayaçları diğer filtrelerden bağımsızdır (tüm taleplerin durum dağılımını gösterir); `Tümü`
sayacı sekizinin toplamıdır.

**Filtreler**

| Filtre | Ne yapar |
|---|---|
| Durum sekmesi | Seçili durumdaki talepleri listeler; `Tümü` hepsi. Sekme değişince 1. sayfaya dönülür. |
| Kategori (açılır liste) | `Tüm kategoriler` ya da tek kategori. |
| Öncelik (açılır liste) | `Tüm öncelikler` ya da tek öncelik. |
| Herkes / Benim taleplerim / Bana atananlar | `Herkes`: filtre yok. `Benim taleplerim`: talep eden sizsiniz. `Bana atananlar`: atanan sizsiniz. Tek seçim; seçili düğme marka renginde dolgulu görünür. |
| Arama kutusu + **Ara** | "Kod, başlık veya açıklama ara…" — yazdıkça değil, **Enter** ya da **Ara** butonuyla uygulanır. Kod, başlık ve açıklama metninde büyük/küçük harf duyarsız "içinde geçen" araması yapar (ör. `0012`, `kargo`). |

Sıralama sabittir: en yeni oluşturulan en üstte; sütun başlıklarına tıklayarak sıralama değiştirilemez. Sayfa boyutu 20.
**Satıra tıklayınca** talebin detay sayfası (`/requests/{id}`) açılır. Liste boşsa "Talep bulunamadı. Sağ üstten yeni
talep girebilirsiniz." yazısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| **+ Yeni Talep** | Liste, sağ üst | "Yeni Talep" penceresi açılır (alanlar aşağıda). Kaydedince pencere kapanır, liste tazelenir ve doğrudan yeni talebin detay sayfasına gidilir. | Panele giriş yeterli |
| **Talebi Oluştur** | Yeni Talep penceresi | Talep `Yeni` durumunda kaydedilir, numara verilir, Süreç Akışı'na "talebi oluşturdu" kaydı düşer (ekler bu kayda bağlanır). | Başlık dolu olmalı; boşken buton pasif |
| **Vazgeç** | Yeni Talep / Düzenle / Durum pencereleri | Pencereyi kapatır, hiçbir şey kaydedilmez. | — |
| **+ Dosya ekle (görsel/PDF)** | Yeni Talep penceresi, Ekler alanı | Dosya seçici açılır; seçilen dosya hemen yüklenir ve adı bağlantı olarak listelenir (tıklayınca yeni sekmede açılır). Birden çok dosya tek tek eklenir. Yükleme sürerken etiket "Yükleniyor…" olur. | JPEG/PNG/WebP/GIF/PDF; en fazla 10 MB |
| **Ara** | Filtre şeridi | Arama kutusundaki metni uygular, 1. sayfaya döner. | — |
| **← Önceki / Sonraki →** | Tablo altı | Sayfa değiştirir; ilk/son sayfada ilgili buton pasiftir. | Birden çok sayfa |
| **Satır** | Tablo | Detay sayfasını açar. | — |

Detay sayfasındaki butonlar için aşağıdaki **Detay sayfası** bölümüne bakın.

## Form alanları
**Yeni Talep penceresi** (ve aynı alanlarla **Düzenle** penceresi)

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Başlık | Evet | Talebin kısa özeti; pencere açılınca imleç buradadır. Boşsa **Talebi Oluştur / Kaydet** pasif kalır; yalnız boşluk girilirse sunucu "Talep başlığı zorunludur." der. Örnek: `Sipariş listesine kargo firması sütunu` |
| Kategori | Evet (varsayılan: Yeni Özellik) | `Yeni Özellik` · `Hata` · `İyileştirme` · `Veri İşi` · `Diğer`. Liste sabittir, panelden yeni kategori eklenemez. |
| Öncelik | Evet (varsayılan: Normal) | `Düşük` · `Normal` · `Yüksek` · `Kritik`. |
| Termin (opsiyonel) | Hayır | Tarih seçici. Boş bırakılabilir; girilirse listede ve detayda gösterilir, geçince gecikme uyarısı çıkar. |
| Açıklama | Hayır | Serbest metin; satır sonları korunur. Yer tutucu: "Ne isteniyor, neden gerekli? Mümkünse örnek/senaryo ekleyin." Boş bırakılırsa detayda "Açıklama girilmemiş." yazar. |
| Ekler | Hayır | Yalnız Yeni Talep penceresinde vardır (Düzenle'de ek alanı yoktur; sonradan ek, detaydaki yorum kutusundan gönderilir). Dosya türü/boyut sınırı yukarıdaki gibi; uygun olmayan dosyada "Yalnızca JPEG, PNG, WebP, GIF veya PDF yükleyebilirsiniz." ya da "Dosya en fazla 10 MB olabilir." mesajı görünür. |

## Sekmeler
Liste sayfasında yalnız durum sekmeleri vardır (yukarıda). Detay sayfasında sekme yoktur; bilgiler kartlara bölünmüştür.

## Detay sayfası
![Talep detayı — sol: başlık/açıklama + Süreç Akışı, sağ: Durum İşlemleri + Bilgiler](img/requests-detay.webp)
*(1) Kırıntı (Proje Talepleri / TLP-…) · (2) Talep kartı · (3) Süreç Akışı ve yorum kutusu · (4) Durum İşlemleri · (5) Bilgiler*

1. **Kırıntı** — "Proje Talepleri" bağlantısı (listeye döner) ve talep kodu.
2. **Talep kartı** — başlık; sağ üstte **Düzenle** (talep kapanmamışsa); altında üç rozet: durum, öncelik, kategori;
   en altta açıklama metni.
3. **Süreç Akışı (N)** — talebin tüm geçmişi: oluşturma, durum değişiklikleri, atamalar, güncellemeler ve yorumlar;
   parantezdeki sayı kayıt adedidir. Her satırda kullanıcının baş harfleri, adı, yapılan iş ve tarih-saat; yorum metni ve
   📎 ek bağlantıları gri kutuda gösterilir. Altında **Yorum yazın…** kutusu, **+ Ek** düğmesi ve **Gönder**.
4. **Durum İşlemleri** — bulunulan durumdan gidilebilecek durumların her biri için bir buton. Kapanmış talepte
   "Talep kapandı — başka durum geçişi yok." yazar.
5. **Bilgiler** — Atanan (açılır liste), Talep Eden, Termin, Oluşturulma, Kapanış.

![Durum değiştirme penceresi](img/requests-detay--durum-modal.webp)

**Süreç Akışı satır türleri**

| Kayıt | Nasıl görünür |
|---|---|
| Oluşturma | "**Ad Soyad** talebi oluşturdu." — açılışta eklenen dosyalar bu satırın altında 📎 olarak listelenir. |
| Durum değişikliği | "durumu `Eski` → `Yeni` olarak değiştirdi." — durum penceresinde not yazıldıysa altında gri kutuda. |
| Atama | "talebi **Ad** kişisine atadı (önceki: …)." ya da "atamayı kaldırdı (önceki: …)." |
| Güncelleme | "talep bilgilerini güncelledi." (Düzenle penceresiyle kaydedilen her değişiklik; hangi alanın değiştiği yazılmaz). |
| Yorum | "yorum yazdı:" ve altında metin + varsa 📎 ekler. |

**Detay sayfası butonları**

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| **Proje Talepleri** (kırıntı) | Sayfa üstü | Listeye döner. | — |
| **Düzenle** | Talep kartı, sağ üst | "`TLP-… — Düzenle`" penceresi: Başlık, Kategori, Öncelik, Termin, Açıklama değiştirilir; **Kaydet** ile Süreç Akışı'na "güncelledi" kaydı düşer. | Talep kapanmamış olmalı (`Tamamlandı`/`Reddedildi`/`İptal` durumunda buton görünmez; sunucu da "Kapanmış talep düzenlenemez." der) |
| **Durum butonları** (ör. **Değerlendirmede**, **Reddedildi**, **İptal**) | Durum İşlemleri kartı | Tıklanınca "Durum: Eski → Yeni" penceresi açılır; **Not (opsiyonel)** kutusu ve **Onayla**. Onaylanınca durum değişir, not Süreç Akışı'na yazılır. İleri yönlü geçişler dolgulu (birincil), `Reddedildi`/`İptal` gri (ikincil) butondur. `Testte` iken görünen **Yapılıyor (teste dönüş)** butonu talebi geliştirmeye geri gönderir. | Yalnız izinli geçişler buton olarak çıkar (bkz. durum makinesi) |
| **Onayla** | Durum penceresi | Geçişi uygular. ⚠️ `Tamamlandı`, `Reddedildi` ve `İptal` **geri alınamaz** — bu durumlardan çıkış yoktur. | — |
| **Atanan** (açılır liste) | Bilgiler kartı | Aktif panel kullanıcılarını listeler; seçim anında kaydedilir (ayrı kaydet butonu yok) ve Süreç Akışı'na atama kaydı düşer. `— Atanmadı —` seçilirse atama kaldırılır. | Talep kapanmamış olmalı (kapanmışsa liste pasif; sunucu "Kapanmış talebe atama yapılamaz." der) |
| **+ Ek** | Yorum kutusu altı | Dosya seçer ve yükler; yüklenen dosyalar 📎 olarak kutunun yanında birikir, **Gönder** ile yoruma bağlanır. | JPEG/PNG/WebP/GIF/PDF; en fazla 10 MB |
| **Gönder** | Yorum kutusu altı | Yorumu (ve eklenmiş dosyaları) Süreç Akışı'na ekler; kutu temizlenir. | Metin **veya** en az bir ek olmalı; ikisi de boşsa buton pasif. Kapanmış talebe de yorum yazılabilir. |

## Durumlar ve iş kuralları
**Durum rozetleri ve geçişler**

| Durum (rozet) | Anlamı | Buradan gidilebilecek durumlar |
|---|---|---|
| `Yeni` (mavi) | Talep yeni açıldı, henüz bakılmadı. | `Değerlendirmede`, `Reddedildi`, `İptal` |
| `Değerlendirmede` (turuncu) | İnceleniyor; yapılıp yapılmayacağına karar verilecek. | `Planlandı`, `Reddedildi`, `İptal` |
| `Planlandı` (marka rengi) | Yapılmasına karar verildi, sıraya alındı. | `Yapılıyor`, `İptal` |
| `Yapılıyor` (turuncu) | Üzerinde çalışılıyor. | `Testte`, `İptal` |
| `Testte` (mavi) | Geliştirme bitti, doğrulanıyor. | `Tamamlandı`, `Yapılıyor` (teste dönüş), `İptal` |
| `Tamamlandı` (yeşil) | Kapandı — iş bitti. | — (son durum) |
| `Reddedildi` (kırmızı) | Kapandı — yapılmayacak. | — (son durum) |
| `İptal` (gri) | Kapandı — talep geri çekildi/geçersiz. | — (son durum) |

Ana akış: `Yeni` → `Değerlendirmede` → `Planlandı` → `Yapılıyor` → `Testte` → `Tamamlandı`. Testten `Yapılıyor`'a geri
dönüş vardır; onun dışında geriye gidiş yoktur. `Reddedildi` yalnız `Yeni` ve `Değerlendirmede`den, `İptal` kapanmamış
her durumdan seçilebilir. Geçiş kuralları sunucuda da doğrulanır; ekranda olmayan bir geçiş denenirse "… durumundan …
durumuna geçilemez." hatası alınır.

**Diğer kurallar**
- **Numara serisi:** her talep `TLP-<yıl>-<sıra>` kodu alır (ör. `TLP-2026-0001`); sıra yıl içinde 1'den başlar, her yıl
  başında sıfırlanır, 4 hane doldurulur. Kod değiştirilemez ve aynı yıl içinde tekrar etmez.
- **Kapanış tarihi:** talep `Tamamlandı`, `Reddedildi` veya `İptal` durumuna geçtiği anda **Kapanış** alanına o anın
  tarih-saati yazılır.
- **Kapanmış talep:** düzenlenemez, atanamaz, durumu değişmez; yalnız yorum ve ek eklenebilir (okunur kayıt gibi davranır).
- **Gecikme:** termin tarihi bugünden önceyse ve talep kapanmamışsa listede tarih kırmızı + ⚠, detayda "⚠ Gecikti"
  görünür. Kapanmış taleplerde gecikme uyarısı gösterilmez.
- **Talep Eden ve kullanıcı adları** giriş yapan hesaptan otomatik alınır; elle seçilmez.
- **Ekler** sunucuda ay bazlı klasörde saklanır ve bağlantıyla açılır; yüklenen dosya panelden silinemez.
- **Yetki:** bu sürümde ayrı bir "talep yönetimi" izni yoktur; panele girebilen herkes tüm talepleri görür ve her işlemi
  yapabilir (başkasının talebini düzenlemek/atamak/kapatmak dahil).

## Adım adım
**Yeni talep açma**
1. Sol menüden **Genel → Proje Talepleri**'ne gidin, sağ üstte **+ Yeni Talep**'e tıklayın.
2. **Başlık** yazın; **Kategori** ve **Öncelik** seçin (gerekirse **Termin** verin).
3. **Açıklama**ya ne istendiğini, neden gerektiğini ve varsa örnek senaryoyu yazın.
4. Ekran görüntüsü/PDF varsa **+ Dosya ekle (görsel/PDF)** ile yükleyin (her dosya için tekrar tıklayın).
5. **Talebi Oluştur**'a tıklayın. Talep `Yeni` durumunda kaydedilir, numarasını alır ve detay sayfası açılır.

**Talebi değerlendirip sorumluya atama**
1. Listede `Yeni` sekmesini açın, ilgili satıra tıklayın.
2. Sağdaki **Durum İşlemleri** kartında **Değerlendirmede**'ye tıklayın; isterseniz not yazıp **Onayla**.
3. **Bilgiler → Atanan** listesinden sorumluyu seçin (anında kaydedilir).
4. Karar verilince **Planlandı** (yapılacak) ya da **Reddedildi** (yapılmayacak — red gerekçesini nota yazmanız önerilir).

**Talebi ilerletip kapatma**
1. Sorumlu işe başlayınca **Yapılıyor**, bitirince **Testte**'ye alın.
2. Test olumluysa **Tamamlandı**; sorun çıkarsa **Yapılıyor (teste dönüş)** ile geri gönderin ve yorum kutusuna bulguyu
   yazın (gerekirse ekran görüntüsü ekleyin).
3. `Tamamlandı` sonrası talep kilitlenir; yalnız yorum eklenebilir.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Kendi açtığınız talepleri izlemek için filtre şeridinde **Benim taleplerim**, üzerinize düşenler için
> **Bana atananlar** düğmesini kullanın; sekmelerle birleştirilebilir (ör. `Yapılıyor` + Bana atananlar).

> **İpucu:** Arama kutusu yazdıkça değil **Enter**/**Ara** ile çalışır; sonuç gelmiyorsa Ara'ya bastığınızdan emin olun.
> Açıklama içinde de arar — "kargo" yazınca başlığında geçmese bile açıklamasında geçen talepler gelir.

> **Dikkat:** `Tamamlandı`, `Reddedildi` ve `İptal` son durumlardır; onayladıktan sonra talep bir daha açılamaz. Yanlış
> kapatıldıysa yeni bir talep açıp açıklamasına eski talebin kodunu yazın.

> **Dikkat:** Yorum kutusuna dosya yükledikten sonra **Gönder**'e basmazsanız ek talebe bağlanmaz; sayfadan ayrılınca
> kaybolur.

> **Not:** "Kapanmış talep düzenlenemez." / "Kapanmış talebe atama yapılamaz." mesajları talebin son durumda olduğunu
> gösterir; bu normaldir. "Talep bulunamadı." mesajı geçersiz ya da silinmiş bir talep adresi açıldığında görünür;
> "← Talep listesine dön" bağlantısıyla listeye dönün.

> **Not:** Kategori listesi sabittir; yeni kategori (ör. "Eğitim") eklemek panelden mümkün değildir, ihtiyaç olursa
> `Diğer` kategorisini kullanın ve başlıkta belirtin.

## İlgili sayfalar
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
- [Giriş](/rehber/genel/giris/)
- [Dashboard](/rehber/genel/dashboard/)
- [Kullanıcılar ve Roller](/rehber/sistem/kullanicilar/)
