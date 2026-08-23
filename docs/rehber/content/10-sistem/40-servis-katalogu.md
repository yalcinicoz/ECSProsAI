---
title: Servis Kataloğu
route: /settings/integration-services
group: Sistem
order: 40
summary: Firmaların sözleşme yapabileceği dış servislerin (kargo, SMTP e-posta, SMS, ödeme, pazaryeri, e-fatura, sosyal giriş, görsel arama, reklam/analitik takip servisleri) tanımlandığı ve her servisin firma entegrasyon formu şemasının yönetildiği ekran; yalnız platform yönetimine açıktır.
---

## Ne işe yarar
Servis Kataloğu, sistemin tanıdığı dış servislerin ana listesidir. Firmalar › firma detayı › **Entegrasyonlar**'daki "Servis" açılır listesi ve o servis seçilince çizilen form alanları buradan gelir. Kataloğa yeni bir kargo firması, ödeme sağlayıcı ya da takip servisi eklemek, bir servisin form alanlarını (hangi kimlik bilgisi şifreli saklanacak, hangisi zorunlu) belirlemek ve kargo servislerinde kargo kodu kuralını tanımlamak için kullanılır.

> **Dikkat:** Bu sayfa yalnız **`definition.manage`** yetkisine sahip kullanıcılara açıktır (platform yönetimi). Yetkisi olmayan kullanıcıda sol menüde görünmez; adres doğrudan açılırsa "Bu sayfa yalnız platform yönetimine açıktır." mesajı gösterilir. Katalog, firma kullanıcıları tarafından değil geliştirici/platform ekibi tarafından doldurulur; firmalar yalnız katalogdan servis **seçer**.

## Ekran yerleşimi
![Servis Kataloğu listesi](img/settings-integration-services.webp)
1. **Başlık satırı** — "Servis Kataloğu" başlığı ve açıklaması; sağda **Tüm tipler** servis tipi filtresi ve **Yeni Servis** butonu.
2. **Tablo** — her satır bir servis; satır sonunda **Düzenle** butonu.
3. **Pencere** — "Yeni Servis Tanımı" / "Servis Düzenle — ad": servis tipi, kullanılabilirlik, çok dilli ad, otomatik kod, logo ve takip linki, kargo kodu kuralları (yalnız Kargo tipinde) ve **Entegrasyon Alan Şeması** editörü.

## Liste ve filtreler
| Filtre | Ne yapar |
|---|---|
| Tüm tipler (açılır liste) | Listeyi seçilen servis tipine daraltır (Kargo, E-Posta (SMTP), Görsel Arama, Pazaryeri, e-Fatura Entegratörü, Ödeme, Sosyal Giriş (OAuth), SMS, ERP, Analytics (GA4), Tag Manager (GTM), Reklam (Google Ads), Merchant Center (Feed), Search Console, Meta Pixel / CAPI, TikTok Pixel / Events API, Pinterest Tag / CAPI, Microsoft Ads (UET), Microsoft Clarity, Diğer). |

| Sütun | Anlamı |
|---|---|
| KOD | Servis kodu; Türkçe addan otomatik üretilir, sonradan değiştirilemez. |
| AD | Servis adı (çok dilli; Türkçe gösterilir). |
| TİP | Servis tipi rozeti (yukarıdaki etiketlerle). |
| ŞEMA ALANLARI | Şemadaki ilk 3 alan etiketi (kimlik bilgisi alanları sarı, ayar alanları gri), fazlası `+N`; şema yoksa `—`. |
| DURUM | `Kullanılabilir` / `Kapalı`. Kapalı servis firmalara sunulmaz. |
| (son sütun) | **Düzenle** butonu. |

Boş durumda "Servis bulunamadı." yazar. Satır tıklaması detay açmaz; sayfalama yoktur.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Servis | Sağ üst | "Yeni Servis Tanımı" penceresi açılır. | `definition.manage`; kaynak dilde ad girilmeden **Oluştur** pasif. |
| Düzenle | Satır sonu | "Servis Düzenle — ad" penceresi açılır; servis tipi kilitlidir. | `definition.manage` |
| Oluştur | Yeni pencere altı | Kaydeder, pencere kapanır. | Kod benzersiz olmalı ("Bu kodda bir servis zaten mevcut."). |
| Kaydet | Düzenleme penceresi altı | Kaydeder; pencere **kapanmaz**, "Kaydedildi" rozeti birkaç saniye görünür. | — |
| Kapat / İptal | Pencere altı | Pencereyi kapatır. | — |
| Alan Ekle | Şema editörü | Şemaya boş alan satırı ekler. | — |
| Çöp kutusu ikonu | Şema alanı satırı | Alanı şemadan kaldırır. | ⚠️ Firma entegrasyon formundan alan kaybolur; firmaların girdiği mevcut değerler silinmez, "Şema Dışı" bölümünde serbest satır olarak görünmeye devam eder. |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Servis Tipi | Evet | Yukarıdaki tip listesi; varsayılan Kargo. Düzenlemede "(değiştirilemez)". |
| Kullanılabilir | Hayır | Kapatılırsa servis `Kapalı` olur ve firmalar yeni entegrasyonda seçemez. |
| Ad (çok dilli) | Kaynak dilde evet | Dil sekmeli alan. Örn. "Yurtiçi Kargo", "Google Analytics 4". |
| Otomatik Kod | Otomatik (yalnız yeni) | Türkçe addan üretilir; kayıt sonrası değiştirilemez. |
| Logo URL | Hayır | `https://…` biçiminde logo adresi. |
| Takip Linki Şablonu (kargo) | Hayır | Kargo takip adresi şablonu; `{trackingNumber}` yer tutucusu takip numarasıyla değiştirilir. Örn. `https://…?code={trackingNumber}`. |
| Kargo Kodu Stratejisi | Hayır (yalnız Kargo tipinde) | `— (varsayılan: serbest)`, `Serbest — paket no + önek`, `Kurallı — uzunluk/karakter kontrolü`, `Tahsisli aralık (PTT tarzı)`, `Dış kod — taşıyıcı/pazaryeri verir`. Tahsisli aralıkta kodlar firma entegrasyonuna tanımlı barkod aralığından atanır; aralık tükenince kod üretimi açık hata verir. |
| En Az Uzunluk / En Çok Uzunluk | Hayır (Kargo) | Kargo kodu uzunluk sınırları (sayı). |
| Karakter Kümesi | Hayır (Kargo) | `— serbest`, `Yalnız rakam`, `Harf + rakam`. |
| Entegrasyon Alan Şeması | Hayır | Firma detayındaki entegrasyon formu bu alanlardan üretilir. **Kimlik Bilgileri** bölümündeki alanlar veritabanında şifreli saklanır. |

### Şema editörü — her alan için
| Sütun / kutu | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| ANAHTAR | Evet | İç ad; boşluklar alt çizgiye, harfler küçüğe çevrilir. Örn. `api_key`, `measurementId`, `cariKodu`. |
| TİP | Evet | `Metin`, `Şifre`, `Sayı`, `Tarih`, `Evet/Hayır`. |
| BÖLÜM | Evet | `Kimlik Bilgileri` → şifreli saklanır, firma formunda maskeli görünür; `Ayarlar` → açık saklanır. |
| ZOR. | Hayır | İşaretliyse firma entegrasyon formunda `*` ile zorunlu olur; boş bırakılırsa "Zorunlu alan(lar) boş: …" hatasıyla kayıt reddedilir (Evet/Hayır tipi hariç). |
| Etiket (dil sekmeli) | Önerilir | Firma formunda görünen ad; boşsa anahtar görünür. |

> **Not:** Bazı servislerin alanlarında, firma formunda ⓘ ikonuyla açılan yardım metni (değerin nereden bulunacağı) tanımlıdır; bu yardım metinleri sistemle birlikte gelir, bu ekrandaki editörde ayrı bir kutusu yoktur.

## Durumlar ve iş kuralları
- Durum: `Kullanılabilir` / `Kapalı`.
- Servis kodu ve tipi kayıt sonrası kilitlidir (sistemin servis çözümlemesi tipe bağlıdır).
- Kargo kodu stratejisi yalnız `free`, `pattern`, `range`, `external` değerlerini alır; aksi halde "Geçersiz kargo kod stratejisi…" hatası döner.
- Sistem kurulumuyla gelen servisler: kargo (Aras, Yurtiçi, Sürat, PTT, DHL Kargo (MNG), HepsiJet, Kolay Gelsin, UPS), SMTP E-Posta, GES Telekom SMS, PayTR, Nebim, pazaryerleri (Trendyol, Hepsiburada, n11, Amazon, Çiçeksepeti, Pazarama), Google/Facebook ile Giriş, Görsel Arama ve takip servisleri (GA4, GTM, Google Ads, Merchant Center, Search Console, Meta, TikTok, Pinterest, Microsoft Ads, Clarity).
- Şema değişikliği mevcut firma kayıtlarındaki değerleri silmez; şemadan çıkan anahtarlar firma formunda "Şema Dışı" bölümünde kalır.
- Katalog bir **tanım** listesidir: veri aktarımları/eşlemeler buraya kayıt ekleyemez; yalnız bu ekrandan eklenir.

## Adım adım
### Yeni kargo servisi tanımlama
1. Sistem › **Servis Kataloğu**'nda **Yeni Servis**'e tıklayın; **Servis Tipi** = Kargo.
2. Kaynak dilde **Ad** yazın; isteğe bağlı **Logo URL** ve **Takip Linki Şablonu** girin.
3. **Kargo Kodu Stratejisi** ve uzunluk/karakter kurallarını seçin.
4. **Alan Ekle** ile kullanıcı adı/şifre (Kimlik Bilgileri, Şifre tipi, zorunlu) ve gerekli ayar alanlarını ekleyin.
5. **Oluştur**'a tıklayın. Firmalar artık Entegrasyon Ekle'de bu servisi görür.

### Bir servise zorunlu alan ekleme
1. Satırda **Düzenle**; **Alan Ekle** ile alanı tanımlayın, **ZOR.** kutusunu işaretleyin.
2. **Kaydet**; "Kaydedildi" rozetini görünce **Kapat**.
3. Mevcut entegrasyonlar bir sonraki düzenlemede bu alan dolmadan kaydedilemez.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Tip filtresini seçili bırakıp **Yeni Servis**'e basarsanız form yine Kargo ile açılır; tipi formda ayrıca seçin.

> **Dikkat:** Servis tipi kayıt sonrası değiştirilemez; yanlış tiple oluşturulmuş servisi **Kullanılabilir** kutusunu kapatarak devre dışı bırakın ve doğru tipte yenisini açın (silme yoktur).

> **Not:** Bir firmada kargo entegrasyonu görünmüyorsa önce servisin `Kullanılabilir` olduğundan emin olun.

## İlgili sayfalar
- [Firmalar](/rehber/sistem/firmalar/) (Entegrasyonlar bölümü)
- [Platform Tipleri](/rehber/sistem/platform-tipleri/)
- [Kargo Bölgeleri](/rehber/siparis/kargo-bolgeleri/)
- [Takip & Reklam](/rehber/pazarlama/takip-ve-reklam/)
