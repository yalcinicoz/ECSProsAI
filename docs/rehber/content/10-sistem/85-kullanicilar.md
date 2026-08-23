---
title: Kullanıcılar
route: /settings/users
group: Sistem
order: 85
summary: Panele giriş yapan personel hesaplarının listelendiği, oluşturulduğu, güncellendiği; şifre sıfırlama ve rol atamanın yapıldığı ekran.
---

## Ne işe yarar
Panele giriş yapacak personel hesapları (yönetici, depo, muhasebe vb.) bu ekrandan yönetilir. Yeni bir çalışan işe
başladığında hesabı burada açılır, rolü atanır; ayrıldığında hesabı pasife alınır. Şifresini unutan kullanıcı için
geçici şifre de buradan verilir. Müşteri (üye) hesapları bu ekranda yer almaz; onlar **Müşteriler > Üyeler**
altındadır.

Sol menüde **Sistem > Ayarlar** bağlantısı bu sayfayı açar. Ayrı bir izin gerekmez; panele giriş yapabilen her
kullanıcı görür.

## Ekran yerleşimi
![Kullanıcılar listesi — arama kutusu, tablo ve Yeni Kullanıcı butonu](img/settings-users.webp)
1. **Başlık satırı** — "Kullanıcılar" başlığı, altında toplam kayıt sayısı; sağda **+ Yeni Kullanıcı** butonu.
2. **Arama şeridi** — "Ad, e-posta, kullanıcı adı ara…" kutusu ve **Ara** butonu.
3. **Tablo** — kullanıcı satırları; satıra tıklayınca düzenleme penceresi açılır.
4. **Sayfalama** — tablonun altında `← Önceki  1 / N  Sonraki →` (tek sayfaysa görünmez).
5. **Kullanıcı penceresi (modal)** — yeni kayıt ya da düzenleme formu; düzenlemede ek olarak Rol Ata ve Şifre Sıfırla bölümü.

![Kullanıcı düzenleme penceresi — form, Aktif kutusu, Rol Ata ve Şifre Sıfırla](img/settings-users--duzenle-modal.webp)

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KULLANICI ADI | Giriş için kullanılan benzersiz ad (değiştirilemez). |
| AD SOYAD | Kullanıcının adı ve soyadı. |
| E-POSTA | Kayıtlı e-posta adresi (değiştirilemez). |
| ROLLER | Atanmış rollerin kodları, virgülle ayrılmış; rol yoksa `—`. |
| SON GİRİŞ | Son başarılı giriş tarihi ve saati; hiç girmediyse `—`. |
| DURUM | `Aktif` (yeşil) / `Pasif` (gri) rozeti. Pasif kullanıcı panele giremez. |
| (son sütun) | "Düzenle →" ipucu; satırın tıklanabilir olduğunu gösterir. |

| Filtre | Ne yapar |
|---|---|
| Arama kutusu | Kullanıcı adı, e-posta, ad ve soyad içinde geçen metni arar. Yazdıktan sonra **Enter** ya da **Ara** ile uygulanır; liste 1. sayfaya döner. |

- Sıralama: kullanıcı adına göre alfabetik, sabit.
- Sayfa boyutu 20 kayıttır.
- Pasif kullanıcılar da listelenir (gizlenmez).
- Satıra tıklayınca o kullanıcının düzenleme penceresi açılır.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Kullanıcı | Başlık satırı sağ | Boş "Yeni Kullanıcı" penceresi açılır. | Panel girişi |
| Ara | Arama şeridi | Yazılan metinle listeyi süzer. | — |
| Satır tıklama | Tablo | "Kullanıcı: {kullanıcı adı}" düzenleme penceresi açılır. | — |
| Kaydet | Pencere alt sağ | Yeni kayıtta hesabı oluşturur; düzenlemede ad/soyad/departman/ünvan/aktiflik bilgisini günceller. Başarılıysa pencere kapanır, liste yenilenir. | Zorunlu alanlar dolu olmalı; aksi hâlde buton pasiftir. |
| Vazgeç | Pencere alt sol | Kaydetmeden kapatır. | — |
| Aktif (onay kutusu) | Düzenleme penceresi | İşareti kaldırıp **Kaydet** deyince hesap pasife alınır; kullanıcı panele giremez. Tekrar işaretleyip kaydedince yeniden aktif olur. | Yalnız düzenlemede |
| Ata | Düzenleme penceresi, Rol Ata satırı | Açılır listeden seçilen rolü kullanıcıya **ekler** (mevcut roller kaldırılmaz). "Rol atandı." mesajı çıkar. | Listeden bir rol seçilmiş olmalı (seçilmeden buton pasif). Aynı rol ikinci kez atanamaz: "Bu rol zaten atanmış." |
| Şifre Sıfırla | Düzenleme penceresi | "Yeni şifre (kullanıcı ilk girişte değiştirecek):" diyalogu açılır; girilen şifre anında geçerli olur, "Şifre sıfırlandı." mesajı çıkar. Diyalog boş/iptal edilirse hiçbir şey yapılmaz. | Yalnız düzenlemede |

> **Dikkat:** Kullanıcı silme işlemi bu ekranda yoktur. Ayrılan personelin hesabını **Aktif** işaretini kaldırarak
> pasife alın; geçmiş işlem izleri ve kayıt sahipliği korunur.

## Form alanları
Yeni kayıt ve düzenleme aynı pencereyi kullanır; bazı alanlar yalnız yeni kayıtta düzenlenebilir.

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kullanıcı Adı | Evet | En az 3 karakter; benzersiz olmalı. Giriş ekranında kullanılır. Yalnız yeni kayıtta yazılır, sonradan değiştirilemez (düzenlemede gri). Örn. `ayse.yilmaz` |
| E-posta | Evet | `@` içeren geçerli bir adres; benzersiz olmalı. Sonradan değiştirilemez. |
| Geçici Şifre | Evet (yalnız yeni kayıt) | En az 8 karakter. Kullanıcı bu şifreyle ilk kez girer; ilk girişte şifreyi değiştirmesi istenir ("şifre değişmeli" işareti yeni kullanıcıda otomatik açıktır). |
| Ad | Evet | Kullanıcının adı. |
| Soyad | Evet | Kullanıcının soyadı. |
| Departman | Hayır | Serbest metin, örn. `Depo`, `Muhasebe`. |
| Ünvan | Hayır | Serbest metin, örn. `Operasyon Sorumlusu`. |
| Aktif | — (yalnız düzenleme) | Onay kutusu; işaretli değilse kullanıcı panele giremez. Yeni kullanıcı her zaman aktif açılır. |
| Rol Ata | — (yalnız düzenleme) | Açılır liste; etiketin yanında "(mevcut: …)" ile kullanıcının hâlihazırdaki rolleri görünür. **Ata** ile uygulanır, Kaydet gerekmez. |

Doğrulama mesajları pencerenin altında kırmızı metinle gösterilir:
- "Bu kullanıcı adı veya e-posta zaten kullanımda." — aynı kullanıcı adı/e-posta ile başka hesap var.
- "Bu rol zaten atanmış." — seçilen rol kullanıcıda zaten tanımlı.
- "Kullanıcı bulunamadı." / "Rol bulunamadı." — kayıt bu arada silinmiş olabilir; listeyi yenileyin.

## Durumlar ve iş kuralları
| Rozet | Anlamı |
|---|---|
| `Aktif` (yeşil) | Hesap açık; kullanıcı giriş yapabilir. |
| `Pasif` (gri) | Hesap kapalı; giriş reddedilir. Kayıt ve geçmiş izler durur. |

- Yeni açılan her hesap **şifre değişmeli** işaretiyle oluşturulur: kullanıcı ilk girişte kendi şifresini belirlemeden
  devam edemez.
- Kullanıcı kendi şifresini panel üst çubuğundaki kullanıcı menüsünden (mevcut şifresini girerek) değiştirebilir;
  yöneticinin yaptığı **Şifre Sıfırla** işleminde mevcut şifre sorulmaz.
- Roller toplamalıdır: birden çok rol atanabilir; kullanıcının yetkileri atanmış rollerin izinlerinin birleşimidir.
  Rol kaldırma bu ekranda yoktur (bkz. bilinen sınırlar).
- Sol menüde yalnız yetkinizin olduğu sayfalar görünür; yeni rol atadığınız kullanıcı değişikliği bir sonraki
  girişinde (oturumu yenilendiğinde) görür.

## Adım adım
**Yeni kullanıcı açma**
1. **+ Yeni Kullanıcı** butonuna tıklayın.
2. Kullanıcı Adı, E-posta, Geçici Şifre (en az 8 karakter), Ad ve Soyad alanlarını doldurun; isterseniz Departman ve Ünvan girin.
3. **Kaydet**'e tıklayın — pencere kapanır, kullanıcı listede görünür.
4. Listede yeni kullanıcıya tıklayın, **Rol Ata** listesinden rolü seçip **Ata**'ya basın.
5. Kullanıcı adı ve geçici şifreyi çalışana iletin; ilk girişte yeni şifre belirlemesi istenecektir.

**Şifresini unutan kullanıcıya geçici şifre verme**
1. Listede kullanıcıyı bulup satırına tıklayın.
2. **Şifre Sıfırla** butonuna basın, açılan diyaloga yeni şifreyi yazıp Tamam deyin.
3. "Şifre sıfırlandı." mesajını görünce şifreyi kullanıcıya güvenli bir yolla iletin.

**Ayrılan personeli kapatma**
1. Kullanıcının satırına tıklayın.
2. **Aktif** kutusunun işaretini kaldırın ve **Kaydet**'e basın. Durum rozeti `Pasif` olur.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Arama kutusu ad, soyad, e-posta ve kullanıcı adında aynı anda arar; "yilmaz" yazmak soyadı Yılmaz olan
> herkesi getirir (büyük/küçük harf fark etmez).

> **Dikkat:** Kullanıcı adı ve e-posta sonradan değiştirilemez. Yanlış açıldıysa hesabı pasife alıp doğru bilgilerle
> yeni hesap açın.

> **Dikkat:** **Şifre Sıfırla** ile verdiğiniz şifre anında geçerlidir; diyalog metni "ilk girişte değiştirecek" dese de
> bu işlemde kullanıcıdan zorunlu şifre değişimi istenmez. Kullanıcıya kendi şifresini üst çubuktaki menüden
> değiştirmesini hatırlatın.

> **Not:** Kaydet butonu pasifse zorunlu alanlardan biri eksik ya da kurala uymuyordur (kullanıcı adı 3 karakterden
> kısa, e-postada `@` yok, şifre 8 karakterden kısa).

Bilinen sınırlar: rol kaldırma, telefon alanı ve kullanıcı silme bu ekranda yoktur; roller yalnız eklenir.

## İlgili sayfalar
- [Roller ve Yetkiler](/rehber/sistem/roller-ve-yetkiler/)
- [Denetim Logları](/rehber/sistem/denetim-loglari/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
