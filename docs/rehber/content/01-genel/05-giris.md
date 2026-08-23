---
title: Giriş
route: /login
group: Genel
order: 5
summary: Panele giriş ekranı, oturumun nasıl açıldığı/yenilendiği/kapatıldığı ve giriş sonrası karşınıza çıkan üst çubuk, sol menü ve kullanıcı alanı.
---

## Ne işe yarar
Giriş sayfası, yönetim paneline erişimin tek kapısıdır. Size tanımlanan **kullanıcı adı** ve **şifre** ile oturum açarsınız;
oturum açmadan panelin hiçbir sayfası görüntülenemez — adres çubuğuna doğrudan bir panel adresi yazsanız bile bu
sayfaya yönlendirilirsiniz. Başarılı girişten sonra Dashboard açılır ve yetkinize göre sol menüde göreceğiniz sayfalar
belirlenir. Bu sayfa ayrıca giriş sonrasında her ekranda ortak olan üst çubuğu, sol menüyü ve oturum davranışlarını
(otomatik yenileme, çıkış) anlatır.

## Ekran yerleşimi
![Giriş ekranı — koyu arka plan üzerinde logo ve giriş kartı](img/login.webp)
*(1) Logo ve "Yönetim Paneli" ibaresi · (2) Giriş kartı: "Giriş Yap" başlığı, hata kutusu, Kullanıcı Adı, Şifre, Giriş Yap butonu*

1. **Logo alanı** — ekranın üst ortasında panel adı ve altında "Yönetim Paneli" yazısı. Tıklanabilir değildir.
2. **Giriş kartı** — ortalanmış, dar bir kart. Üstte "Giriş Yap" başlığı; giriş başarısız olursa başlığın hemen altında
   kırmızı zeminli hata kutusu belirir. Altında iki alan (Kullanıcı Adı, Şifre) ve tam genişlikte **Giriş Yap** butonu.

Giriş yaptıktan sonra panelin ortak çerçevesi açılır:

![Giriş sonrası panel çerçevesi — sol menü, üst çubuk, içerik alanı](img/dashboard.webp)
*(1) Sol menü · (2) Üst çubuk · (3) İçerik alanı · (4) Sağ kenardaki Sık Kullanılanlar sekmesi*

1. **Sol menü** — en üstte logo (tıklayınca Dashboard'a döner) ve "Daralt" düğmesi; altında **Menüde ara…** kutusu;
   ardından bölümler (Genel, Katalog, Sipariş Yönetimi, Cari, Müşteriler, Stok, Pazarlama, İçerik, Sistem) ve sayfa
   bağlantıları; en altta **kullanıcı alanı** (baş harflerinizden oluşan yuvarlak rozet, ad-soyad, e-posta ve **Çıkış**
   simgesi). Menü daraltıldığında yalnız simgeler görünür; simgenin üzerine gelince sayfa adı ipucu olarak çıkar.
   Telefon/tablette sol menü gizlidir; üst çubuktaki ☰ düğmesiyle soldan kayarak açılır.
2. **Üst çubuk** — soldan sağa: ☰ menü düğmesi (masaüstünde menüyü daraltır/genişletir), bulunduğunuz sayfanın adı,
   ortada **Ara…** kutusu (`Ctrl`+`K`), sağda **Favorilere Ekle**, **Bildirimler** (çan), **tema** düğmesi (ay/güneş).
3. **İçerik alanı** — seçilen sayfanın listesi, formu veya detayı.
4. **Sık Kullanılanlar sekmesi** — ekranın sağ kenarındaki küçük yıldız; tıklanınca sağdan hızlı erişim paneli kayar.

## Liste ve filtreler
Bu sayfada liste yoktur. Sol menüdeki **Menüde ara…** kutusu yazdıkça menü başlıklarını süzer (ör. "stok" yazınca yalnız
Stok bölümü ve adında "stok" geçen sayfalar kalır). Kutuyu boşaltınca tam menü geri gelir.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| **Giriş Yap** | Giriş kartı | Kullanıcı adı ve şifre doğrulanır; başarılıysa Dashboard açılır. Doğrulama sürerken buton dönen simgeyle kilitlenir. | İki alan da dolu olmalı |
| **Çıkış** (kapı simgesi) | Sol menü, kullanıcı alanı (menü genişken görünür) | Oturum kapanır, tarayıcıdaki oturum bilgileri silinir; giriş sayfasına dönersiniz. Onay sorulmaz. | — |
| **Logo** | Sol menü üstü | Dashboard'a gider. | — |
| **Daralt** (üç çizgi) | Sol menü üstü (menü genişken) | Menüyü yalnız simgeler görünecek şekilde daraltır. Tercih tarayıcıda hatırlanır; bir sonraki girişte aynı durumda açılır. | — |
| **☰** | Üst çubuk, en sol | Masaüstünde menüyü daraltır/genişletir; telefonda menüyü soldan açar. | — |
| **Ara…** (`Ctrl`+`K`) | Üst çubuk ortası | Hızlı arama penceresini açar: "Sipariş, ürün, müşteri ara…" kutusuna yazdıkça **Tümü / Ürünler / Siparişler / Müşteriler** kapsamında ilk 5'er sonuç listelenir. `↑` `↓` ile gezilir, `↵` ile seçilen kayda gidilir, `Esc` kapatır. Ürün sonucu ürün kartına, sipariş sonucu sipariş detayına, müşteri sonucu üye detayına götürür. | — |
| **Favorilere Ekle** | Üst çubuk sağ | Sağdan **Sık Kullanılanlar** panelini açar (aşağıya bakın). | — |
| **Bildirimler** (çan) | Üst çubuk sağ | Üzerinde kırmızı nokta bulunur; bu sürümde tıklanınca bir liste açılmaz. | — |
| **Koyu tema / Açık tema** (ay/güneş) | Üst çubuk sağ | Panelin renk temasını değiştirir; tercih tarayıcıda hatırlanır. | — |
| **Sık Kullanılanlar** (yıldız sekmesi) | Ekranın sağ kenarı | Paneli açar/kapatır. Panelde **Sayfalar** başlığı altında *Bekleyen Siparişler* (yanında bekleyen sipariş sayısı rozeti), *Stok Uyarıları* (aktif stok uyarısı sayısı rozeti) ve *Yeni Sipariş* kısayolları; **Son Ziyaret** başlığı altında *Özellik Tipleri* ve *Ürün Grupları*. Kısayola tıklayınca ilgili sayfa açılır, panel kapanır. Alt kısımdaki **Kısayol Ekle** düğmesi bu sürümde işlevsizdir. Rozet sayıları dakikada bir tazelenir. `Esc` veya panel dışına tıklama kapatır. | — |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kullanıcı Adı | Evet | Yöneticinizin size tanımladığı kullanıcı adı. Tarayıcı otomatik tamamlamayı destekler. Örnek: `admin` |
| Şifre | Evet | Şifreniz gizli karakterlerle gösterilir; göster/gizle düğmesi yoktur. Boş bırakılırsa tarayıcı uyarır. |

Doğrulama mesajı tektir: **"Kullanıcı adı veya şifre hatalı."** — kullanıcı adı bilinmiyor, şifre yanlış ya da hesap pasif
olsa da aynı mesaj görünür; hangi alanın hatalı olduğu söylenmez.

## Sekmeler
Bu sayfada sekme yoktur.

## Durumlar ve iş kuralları
- **Oturum açma:** başarılı girişte panel sizi doğrudan Dashboard'a (`/`) götürür; giriş sayfasına "geri" tuşuyla dönülmez.
- **Oturum süresi ve otomatik yenileme:** erişim oturumu yaklaşık 1 saat geçerlidir, arka planda 30 güne kadar otomatik
  yenilenir. Yani paneli açık bıraktığınızda ya da ertesi gün geri döndüğünüzde genelde yeniden giriş gerekmez.
  Yenileme başarısız olursa (oturum yönetici tarafından kapatılmış, 30 gün geçmiş vb.) tüm oturum bilgileri silinir ve
  giriş sayfasına yönlendirilirsiniz; o anda kaydetmediğiniz form verileri kaybolur.
- **Sayfa koruması:** oturum yokken herhangi bir panel adresi açılırsa otomatik olarak `/login` sayfasına düşersiniz.
- **Yetki ve menü:** sol menüde yalnız yetkinizin olduğu sayfalar listelenir (ör. *Servis Kataloğu* yalnız tanım
  yönetimi yetkisi olanlara, *Tedarikçi Gönderimleri* ürün yönetimi yetkisi olanlara görünür). Menüde olmayan bir
  sayfaya adresle gitmeye çalışırsanız içerik yüklenmez ya da yetki hatası alırsınız.
- **Şifre değiştirme:** panelde kullanıcının kendi şifresini değiştirdiği bir ekran **bu sürümde yoktur**. Şifreniz
  yalnız yönetici tarafından **Ayarlar → Kullanıcılar** sayfasındaki **Şifre Sıfırla** işlemiyle yenilenir; yeni
  kullanıcı açılırken verilen geçici şifre en az 8 karakterdir ve kayıt "ilk girişte değiştirecek" olarak işaretlenir.
  Şifrenizi unuttuysanız yöneticinize başvurun.
- **Tercihlerin hatırlanması:** menü daraltma durumu ve koyu/açık tema tercihi kullandığınız tarayıcıda saklanır;
  çıkış yapsanız bile korunur, başka bir tarayıcıya taşınmaz.

## Adım adım
**Panele giriş**
1. Tarayıcıda panel adresini açın (`/admin`); giriş sayfası gelir.
2. **Kullanıcı Adı** ve **Şifre** alanlarını doldurun.
3. **Giriş Yap**'a tıklayın (ya da `Enter`). Dashboard açılır.

**Oturumu kapatma**
1. Sol menü daraltılmışsa üst çubuktaki ☰ ile genişletin.
2. Menünün en altındaki kullanıcı alanında, e-postanızın sağındaki **Çıkış** simgesine tıklayın.
3. Giriş sayfasına dönersiniz; ortak bilgisayarlarda bu adımı atlamayın.

**Hızlı arama ile kayda gitme**
1. Herhangi bir sayfada `Ctrl`+`K` basın (veya üst çubuktaki **Ara…** kutusuna tıklayın).
2. Sipariş numarası, ürün kodu/adı ya da müşteri adı yazın; 300 ms sonra sonuçlar listelenir.
3. Gerekirse üstteki **Ürünler / Siparişler / Müşteriler** sekmesiyle kapsamı daraltın; `↑` `↓` ve `↵` ile açın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Menüde sayfa ararken sol menüdeki **Menüde ara…** kutusunu kullanın; kayıt (sipariş/ürün/müşteri) ararken
> `Ctrl`+`K`. İkisi farklı şeyleri arar.

> **Dikkat:** "Kullanıcı adı veya şifre hatalı." mesajı tek tip olduğundan hesabınızın pasif olup olmadığını
> ekrandan anlayamazsınız. Şifrenizden eminseniz yöneticinizden hesabınızın aktif olduğunu ve şifrenin sıfırlanmasını
> isteyin.

> **Dikkat:** Uzun süre açık kalan sekmede bir işlem yaparken aniden giriş sayfasına düşerseniz oturum yenilenememiştir;
> o işlem kaydedilmemiştir. Yeniden giriş yapıp tekrarlayın.

> **Not:** Üst çubuktaki çan simgesi ve Sık Kullanılanlar panelindeki **Kısayol Ekle** düğmesi bu sürümde yalnız
> görseldir; kişisel kısayol tanımlanamaz.

## İlgili sayfalar
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
- [Dashboard](/rehber/genel/dashboard/)
- [Kullanıcılar ve Roller](/rehber/sistem/kullanicilar/)
