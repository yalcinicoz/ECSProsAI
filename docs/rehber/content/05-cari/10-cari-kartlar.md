---
title: Cari Kartlar
route: /accounts
group: Cari
order: 10
summary: Müşteri ve tedarikçi cari hesaplarının listelendiği, oluşturulduğu, para birimi hesaplarının ve hareket dökümünün izlendiği ekran.
---

## Ne işe yarar
Cari Kartlar, mağazanızın para ilişkisi olan tüm tarafların (müşteriler, tedarikçiler, pazaryeri satıcıları) hesap kartlarını tutar. Her kartın altında para birimi bazında **hesaplar** (defterler) ve bu hesaplara yazılan **hareketler** bulunur; bakiye yalnız hareketlerle değişir. Üyelerin cüzdanları da birer cari hesap olarak burada görünür (Sahip = Üye). Muhasebe/finans ve satın alma ekipleri tedarikçi kartlarını, müşteri hizmetleri üye cüzdan bakiyelerini bu ekrandan izler.

> **Not:** Eski "Finans > Tedarikçiler" bağlantısı artık bu listeye, **Tip = Tedarikçi** filtresiyle yönlenir; ayrı bir tedarikçi ekranı yoktur.

## Ekran yerleşimi
![Cari Kartlar listesi — filtre kartı ve tablo](img/accounts.webp)
1. **Başlık satırı** — "Cari Kartlar", toplam kayıt sayısı ve sağda **+ Yeni Cari** butonu.
2. **Filtre kartı** — Ara kutusu, Tip, Sahip, Grup, Durum açılır listeleri; filtre seçiliyken **Sıfırla** bağlantısı.
3. **Tablo** — cari kartları; satıra tıklayınca detay sayfası açılır.
4. **Sayfalama** — sayfa başına 30 kayıt; «, ‹ Önceki, sayfa numaraları, Sonraki ›, ».

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Cari kodu (oluştururken verilir, büyük harf). |
| ÜNVAN | Firma/kişi adı. Kart bir üyenin cüzdan hesabıysa yanında mavi **Üye** rozeti; altında iletişim kişisi (varsa). |
| TİP | `Müşteri` / `Tedarikçi` / `Her İkisi` rozeti. Tedarikçi kartı pazaryeri satıcısıysa ek **Pazaryeri** rozeti. |
| GRUP | Bağlı cari grubu (yoksa —). |
| VERGİ NO | Vergi numarası / TC (yoksa —). |
| ŞEHİR | Şehir (yoksa —). |
| DURUM | `Aktif` / `Pasif`. |
| › | Detaya gidildiğini gösteren ok. |

| Filtre | Ne yapar |
|---|---|
| Ara | Ünvan, kod, vergi no veya e-posta içinde arar. Enter'a basınca ya da büyüteç butonuna tıklayınca uygulanır. |
| Tip | Tümü / Müşteri / Tedarikçi / Her İkisi. |
| Sahip | Tümü / **Harici Cari** (elle açılan kartlar) / **Üye (cüzdan)** (üyelerin otomatik açılan cüzdan hesapları). |
| Grup | Cari grubuna göre süzer (aktif-pasif tüm gruplar listelenir). |
| Durum | Tümü / Aktif / Pasif. |
| Sıfırla | Tüm filtreleri ve aramayı temizler; yalnız bir filtre seçiliyken görünür. |

Satıra tıklayınca cari detayı açılır. Sonuç yoksa "Cari bulunamadı." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Cari | Liste başlığı | `/accounts/new` oluşturma sayfası açılır. | Panele giriş |
| Satır tıklama | Tablo | Cari detay sayfası açılır. | — |
| Sıfırla | Filtre kartı | Filtreler temizlenir, 1. sayfaya dönülür. | En az bir filtre seçili |
| ‹ Cari Kartlar | Oluşturma ve detay sayfası üstü | Listeye geri döner. | — |
| Oluştur | Yeni Cari Kart formu | Kart kaydedilir ve detay sayfasına geçilir. | Kod ve Ünvan dolu |
| İptal | Yeni Cari Kart formu | Kaydetmeden listeye döner. | — |
| Düzenle | Detay başlığı | Bilgi kartı form haline gelir. | — |
| Kaydet / İptal | Düzenleme formu | Değişiklikler kaydedilir / vazgeçilir. | — |
| + Hesap Ekle | Detay → Hesaplar bölümü | "Yeni Hesap Ekle" penceresi açılır (yeni para birimi hesabı). | — |
| Ekle / İptal | Yeni Hesap Ekle penceresi | Hesap açılır / pencere kapanır. | Para birimi seçili |
| ← Önceki / Sonraki → | Detay → Hareketler altı | Hareket dökümünde sayfa değiştirir (25 hareket/sayfa). | 25'ten fazla hareket |

## Form alanları
Aynı form hem **Yeni Cari Kart** sayfasında hem de detaydaki **Düzenle** modunda kullanılır.

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (yalnız oluştururken) | Yazdıkça büyük harfe çevrilir. Örn. `C-0001`. Benzersiz olmalı; aynı kod varsa "'C-0001' cari kodu zaten mevcut." hatası. Sonradan değiştirilemez. |
| Ünvan | Evet | Firma veya kişi adı. |
| Cari Tipi | Hayır (varsayılan Müşteri) | Müşteri / Tedarikçi / Her İkisi. |
| Grup | Hayır | Cari grubu; "— Grup yok —" bırakılabilir. |
| Satıcı Tipi | Hayır | Yalnız Tedarikçi veya Her İkisi seçiliyken görünür. **Normal tedarikçi (ürün temini)** ya da **Pazaryeri satıcısı (panel + API)**. Pazaryeri satıcısı kendi ürününü sitede satar; satıcı paneli ve API erişimi alır. |
| Vergi No / TC | Hayır | Kimlik Bilgileri bölümü. |
| Vergi Dairesi | Hayır | Kimlik Bilgileri bölümü. |
| İletişim Kişisi | Hayır | Ad Soyad; listede ünvanın altında görünür. |
| Telefon | Hayır | İletişim bölümü. Örn. `+90 555 000 00 00`. |
| E-posta | Hayır | Aramada kullanılır. |
| Adres | Hayır | Açık adres (çok satırlı). |
| Şehir | Hayır | Listede ŞEHİR sütunu. |
| Ülke | Hayır (varsayılan `TR`) | Ülke kodu. |
| Kredi Limiti | Hayır (varsayılan 0) | Finansal bölümü; sayı. Detayda 0 ise "—" görünür. Bu ekran limit aşımı kontrolü yapmaz; bilgi alanıdır. |
| Para Birimi | Hayır (varsayılan `TRY`) | TRY / USD / EUR / GBP. Kartın varsayılan para birimi; oluşturulurken bu para biriminde ilk hesap otomatik açılır. |
| Notlar | Hayır | İç notlar. |
| Aktif | — | Yalnız düzenleme modunda görünür; işareti kaldırınca kart `Pasif` olur. |

### Yeni Hesap Ekle penceresi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Para Birimi | Evet | TRY, USD, EUR, GBP, CHF, JPY, CAD, AUD. Henüz hesabı olmayan ilk para birimi önceden seçili gelir. Aynı para biriminde ikinci hesap açılamaz: "Bu cari için 'USD' hesabı zaten mevcut." |
| Açıklama | Hayır | Örn. `Ana EUR hesabı`. |

## Detay sayfası
![Cari detayı — bilgi kartı, Hesaplar ve Hareketler](img/accounts-detay.webp)
*(1) Başlık: ünvan, kod, tip rozeti, Pazaryeri rozeti, Aktif/Pasif, "Grup: …" · (2) Bilgi kartı / düzenleme formu · (3) Hesaplar tablosu · (4) Hareketler dökümü*

**Bilgi kartı** (salt okunur): Satıcı Tipi (yalnız tedarikçilerde), İletişim Kişisi, Telefon, E-posta, Adres, Şehir, Ülke, Vergi No / TC, Vergi Dairesi, Kredi Limiti (tutar + para birimi), Notlar, Oluşturulma tarihi. Boş alanlar "—" ile gösterilir.

**Hesaplar** tablosu:
| Sütun | Anlamı |
|---|---|
| PARA BİRİMİ | Hesabın para birimi (TRY, USD…). |
| AÇIKLAMA | Hesap açıklaması (yoksa —). |
| BAKİYE | Güncel bakiye; negatifse kırmızı, pozitifse yeşil. |
| DURUM | Kartın varsayılan hesabı **Varsayılan** rozetiyle işaretlidir. |

**Hareketler (N)** bölümü: başlığın sağında her hesap için `kavram · bakiye para birimi` rozeti (örn. `cari · 0,00 TRY`, üye cüzdanında `wallet · 150,00 TRY`).
| Sütun | Anlamı |
|---|---|
| Tarih | Hareket tarihi ve saati. |
| Tip | `Manuel düzeltme`, `İade`, `Ters kayıt` ya da diğer hareket tipleri. |
| Açıklama | Hareket açıklaması (yoksa —). |
| Borç | Bakiyeyi azaltan tutar (kırmızı). |
| Alacak | Bakiyeyi artıran tutar (yeşil). |
| Bakiye | Hareket sonrası bakiye. |

Hareket yoksa "Henüz hareket yok." yazar.

## Durumlar ve iş kuralları
- **Aktif / Pasif:** Pasif kart listede kalır, yalnız Durum filtresiyle ayrıştırılır. Silme işlemi yoktur.
- **Tip rozetleri:** `Müşteri`, `Tedarikçi`, `Her İkisi`; tedarikçi kartında Satıcı Tipi = Pazaryeri satıcısı ise ek `Pazaryeri` rozeti.
- **Bakiye yalnız hareketle değişir.** Bu ekranda bakiye elle yazılamaz; Hesaplar tablosundaki bakiye, hareket dökümünün sonucudur. Hareketler silinmez ya da düzeltilmez; yanlış hareket **ters kayıt** (storno) ile dengelenir.
- **Negatif bakiye** varsayılan olarak reddedilir: bakiyeyi aşan bir borç hareketi "Yetersiz bakiye: mevcut 50,00 TRY, istenen borç 80,00." benzeri hatayla döner.
- **Üye cüzdanları:** Her üyenin cüzdanı, Sahip = Üye olan bir cari hesaptır; kodu `M-000001` biçiminde otomatik verilir ve ilk hareketle birlikte kendiliğinden açılır. Üye cüzdanına hareket eklemek için **Üyeler > üye detayı > Cüzdan** kartı kullanılır; burada yalnız döküm izlenir. Siparişten iade "cüzdana" tamamlandığında cüzdan bakiyesi artar ve dökümde `İade` tipiyle görünür.
- **Hesap/para birimi:** Her hesap tek para birimindedir; başka para birimi gerekirse yeni hesap açılır. Aynı kartta aynı para biriminden iki hesap olamaz.
- **Kod** sonradan değiştirilemez.

## Adım adım
**Yeni tedarikçi kartı açma**
1. Cari > Cari Kartlar'da **+ Yeni Cari**'ye tıklayın.
2. **Kod** (örn. `TED-0001`) ve **Ünvan** girin.
3. **Cari Tipi**'ni Tedarikçi seçin; görünen **Satıcı Tipi**'nde normal tedarikçi ya da pazaryeri satıcısı seçin.
4. İsterseniz grup, vergi/iletişim bilgileri, kredi limiti ve para birimini doldurun.
5. **Oluştur**'a tıklayın; detay sayfası açılır ve seçtiğiniz para biriminde ilk hesap hazır gelir.

**Cariye ikinci para birimi hesabı açma**
1. Cari detayında **Hesaplar** bölümünde **+ Hesap Ekle**'ye tıklayın.
2. **Para Birimi** seçin, isteğe bağlı açıklama yazın.
3. **Ekle**'ye tıklayın; yeni hesap tabloda 0,00 bakiyeyle görünür.

**Bir üyenin cüzdan dökümünü bulma**
1. Listede **Sahip** filtresini **Üye (cüzdan)** yapın ya da Ara kutusuna üyenin adını/e-postasını yazın.
2. Satıra tıklayın; **Hareketler** bölümünde `wallet` rozetli bakiyeyi ve dökümü görürsünüz.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Tedarikçi listesine hızlı ulaşmak için Tip filtresini **Tedarikçi** yapın; tedarikçi faturaları ayrı ekranda tutulur ve tedarikçi kartını buradan seçer.

> **Dikkat:** "'X' cari kodu zaten mevcut." hatası alırsanız farklı bir kod verin; kod benzersizdir ve sonradan değiştirilemez.

> **Dikkat:** Cari detayında manuel hareket ekleme butonu yoktur. Üye cüzdanı için Üyeler > üye detayı > Cüzdan kartındaki **Hareket Ekle** formunu kullanın; bu işlem de hareket olarak dökümde görünür.

> **Not:** Sahip = Üye olan kartlar (Üye rozeti) site kayıtlarından otomatik açılır; bunları elle oluşturmaya gerek yoktur.

## İlgili sayfalar
- [Cari Grupları](/rehber/cari/cari-gruplari/)
- [Üyeler](/rehber/musteriler/uyeler/)
- [Üye Grupları](/rehber/musteriler/uyelik-gruplari/)
