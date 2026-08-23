---
title: Üyeler
route: /crm/members
group: Müşteriler
order: 10
summary: Siteye/uygulamaya kayıt olan üyelerin listelendiği; üye detayında grup, aktiflik, cüzdan, adresler, siparişler ve oturumların izlendiği ekran.
---

## Ne işe yarar
Üyeler ekranı, mağazanızın müşteri hesaplarını (kayıtlı üyeler ve misafir alışveriş kayıtları) gösterir. Müşteri hizmetleri bir üyeyi arayıp detayında üye grubunu değiştirir, hesabı pasife alır, cüzdan bakiyesine manuel hareket ekler; adreslerini, son siparişlerini ve giriş oturumlarını inceler. Üyeler panelden oluşturulmaz; siteden/uygulamadan kayıt olur ya da misafir siparişle açılır.

## Ekran yerleşimi
![Üyeler listesi — sekmeler, arama ve tablo](img/crm-members.webp)
1. **Başlık satırı** — "Üyeler" ve toplam kayıt sayısı.
2. **Sekmeler** — `Aktif` (varsayılan) / `Tümü`.
3. **Arama satırı** — "Ad, e-posta veya telefon ara…" kutusu ve **Ara** butonu.
4. **Tablo** — satıra tıklayınca üye detayı açılır.
5. **Sayfalama** — sayfa başına 20 kayıt; ← Önceki / sayfa no / Sonraki →.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| AD SOYAD | Üyenin adı soyadı. |
| E-POSTA | E-posta adresi (yoksa —). |
| TELEFON | Telefon (yoksa —). |
| ÜYELİK | `Kayıtlı` (şifreli hesap açmış) / `Misafir` (misafir alışveriş kaydı). |
| DURUM | `Aktif` / `Pasif`. |
| KAYIT | Kayıt tarihi. |
| Detay → | Detaya gidildiğini belirtir. |

| Filtre | Ne yapar |
|---|---|
| Aktif / Tümü sekmesi | Aktif'te yalnız aktif üyeler; Tümü'nde pasifler de listelenir. Sekme değişince 1. sayfaya dönülür. |
| Arama kutusu + Ara | Ad, soyad, e-posta ya da telefon içinde arar; Enter ya da **Ara** ile uygulanır. |

Sonuç yoksa "Üye bulunamadı." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Ara | Liste, arama satırı | Yazılan metinle liste yenilenir. | — |
| Satır tıklama | Tablo | `/crm/members/:id` üye detayı açılır. | — |
| ← | Detay başlığı | Listeye döner. | — |
| Kaydet | Detay → Üyelik Yönetimi | Üye grubu ve Aktif işareti kaydedilir; "Kaydedildi ✓" görünür. | — |
| Hareket Ekle | Detay → Cüzdan | Cüzdana manuel hareket yazılır; bakiye ve son hareketler yenilenir. | Tutar > 0 ve Açıklama dolu |
| Sipariş satırı | Detay → Son Siparişler | İlgili sipariş detayına gider. | — |

## Form alanları
### Üyelik Yönetimi kartı
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Üye Grubu | Evet | Açılır listede tüm üye grupları (pasifler dahil). Grup bulunamazsa "Üye grubu bulunamadı." hatası. |
| Aktif (pasif üye giriş yapamaz) | — | İşaret kaldırılıp kaydedilince üye siteye/uygulamaya giriş yapamaz; listede `Pasif` görünür. |
| Duyuru tercihleri | — | Salt okunur satır: e-posta ✓/✗ · SMS ✓/✗ · telefon ✓/✗. Üye kendi belirler; panelden değiştirilemez. |

Ad, soyad, e-posta, telefon, doğum tarihi, vergi bilgileri bu ekranda **düzenlenmez**; üye kendi profilinden günceller.

### Cüzdan kartı — hareket formu
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Yön | Evet | **Bakiye ekle (+)** ya da **Bakiye düş (−)**. |
| Tutar (₺) | Evet | Sıfırdan büyük; kuruş girilebilir (ör. `49,90`). |
| Açıklama | Evet | Neden yapıldığı; boşsa buton pasif kalır ("Açıklama zorunludur (manuel düzeltme izlenebilir olmalı)."). |

## Detay sayfası
![Üye detayı — özet kartları, Üyelik Yönetimi, Cüzdan, Adresler, Son Siparişler, Oturumlar](img/crm-members-detay.webp)
*(1) Başlık ve rozetler · (2) Özet kartları · (3) Üyelik Yönetimi · (4) Cüzdan · (5) Adresler · (6) Son Siparişler · (7) Oturumlar*

**Başlık:** ad soyad; `Aktif`/`Pasif`; doğrulama rozetleri **Tel ✓**, **E-posta ✓**, **TCKN ✓** (yalnız doğrulanmışsa). Altında e-posta · telefon · Kayıt tarihi · Son giriş zamanı.

**Özet kartları (8):**
| Kart | Anlamı |
|---|---|
| Sipariş | Üyenin toplam sipariş sayısı. |
| Favori | Favoriye aldığı ürün sayısı. |
| Koleksiyon | Oluşturduğu koleksiyon sayısı. |
| Yorum | Yazdığı yorum sayısı. |
| Kayıtlı Arama | Kaydettiği arama sayısı. |
| Stok Alarmı | Aktif "gelince haber ver" kaydı sayısı. |
| Cüzdan | Cüzdan bakiyesi (₺). |
| Puan | Kullanılabilir sadakat puanı. |

Değer alınamazsa kartta "—" görünür.

**Cüzdan (bakiye ₺)** kartı: son hareketler listesi — `+`/`−` tutar, tip (`Manuel düzeltme`, `İade` ya da diğer), açıklama, tarih · hareket sonrası bakiye. Hareket yoksa "Henüz cüzdan hareketi yok." Altta hareket formu (yukarıda).

**Adresler (N)** (salt okunur): adres başlığı, **Varsayılan** rozeti, alıcı adı · telefon, adres satırı ve mahalle / ilçe / il. Yoksa "Kayıtlı adres yok."

**Son Siparişler (N)**: en son 10 sipariş — sipariş no, tutar, durum rozeti, tarih; tıklanınca sipariş detayı. Yoksa "Sipariş yok."

**Oturumlar (son 10)** (salt okunur): `Açık`/`Kapalı` rozeti, giriş zamanı, IP adresi, cihaz/tarayıcı bilgisi (kısaltılmış; üzerine gelince tamamı). Yoksa "Oturum kaydı yok." Panelden oturum kapatma yoktur.

## Durumlar ve iş kuralları
- **Aktif / Pasif:** pasif üye giriş yapamaz; kayıtları (sipariş, adres, cüzdan) silinmez. Silme işlemi panelde yoktur.
- **Kayıtlı / Misafir:** misafir kaydı şifresiz oluşur; üye sonradan kayıt olursa Kayıtlı'ya döner.
- **Doğrulama rozetleri:** Tel ✓ ve E-posta ✓ üye tarafındaki doğrulama adımlarıyla oluşur. **TCKN ✓**, üyenin girdiği kimlik numarasının biçim ve kontrol basamağı doğrulamasından geçtiğini gösterir (resmî nüfus sorgusu değildir).
- **Cüzdan = cari hesap:** üyenin cüzdanı Cari Kartlar'da Sahip = Üye olan bir hesaptır (kod `M-000001` biçiminde, ilk hareketle otomatik açılır). Bakiye yalnız hareketle değişir; hareketler silinmez/düzeltilmez (yanlış hareket ters yönlü yeni hareketle dengelenir). **Bakiye düş** işlemi bakiyeyi aşarsa "Yetersiz bakiye: mevcut 50,00 TRY, istenen borç 80,00." hatası verir.
- Siparişten iade "cüzdana" tamamlandığında bakiye otomatik artar ve `İade` tipiyle listelenir.
- **Üye grubu** değişikliği anında geçerlidir; grubun kuralları için [Üye Grupları](/rehber/musteriler/uyelik-gruplari/).
- Şifre sıfırlama / değiştirme bu ekranda yoktur.

## Adım adım
**Bir üyeyi pasife alma**
1. Listede üyeyi arayın ve satıra tıklayın.
2. **Üyelik Yönetimi** kartında **Aktif** işaretini kaldırın.
3. **Kaydet**'e tıklayın; "Kaydedildi ✓" görünür ve başlıktaki rozet `Pasif` olur.

**Cüzdana bakiye ekleme (ör. jest/telafi)**
1. Üye detayında **Cüzdan** kartına gidin.
2. **Yön** = Bakiye ekle (+), **Tutar**'ı ve **Açıklama**'yı yazın (ör. `Geciken teslimat telafisi`).
3. **Hareket Ekle**'ye tıklayın; hareket listede, bakiye kart başlığında güncellenir.

**Üyeyi başka gruba taşıma**
1. Üye detayında **Üye Grubu** listesinden yeni grubu seçin.
2. **Kaydet**'e tıklayın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Pasif üyeyi bulamıyorsanız listede **Tümü** sekmesine geçin; varsayılan görünüm yalnız aktifleri gösterir.

> **Dikkat:** Manuel cüzdan hareketi geri alınamaz; hatalı işlemi ters yönde yeni bir hareketle (aynı tutar, açıklamasına "düzeltme" yazarak) dengeleyin.

> **Dikkat:** "Hareket kaydedilemedi." / "Yetersiz bakiye" hatası: düşülmek istenen tutar bakiyeden büyüktür; önce bakiyeyi kontrol edin.

> **Not:** Özet kartlarında "—" görünmesi verinin henüz alınamadığını gösterir; sayfayı yenileyin.

## İlgili sayfalar
- [Üye Grupları](/rehber/musteriler/uyelik-gruplari/)
- [Cari Kartlar](/rehber/cari/cari-kartlar/)
