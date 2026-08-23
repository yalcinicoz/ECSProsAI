---
title: Dashboard
route: /
group: Genel
order: 10
summary: Giriş sonrası açılan özet ekranı — sipariş, ürün, üye ve POS sayaçlarını tek bakışta gösteren beş kart.
---

## Ne işe yarar
Dashboard, panele girdiğinizde ilk açılan sayfadır ve mağazanızın genel büyüklüğünü tek bakışta özetler: toplam ve
bekleyen sipariş sayısı, ürün kartı sayısı, üye sayısı ve POS satış sayısı. Günün işine başlamadan önce "kaç sipariş
bekliyor?" sorusuna hızlı yanıt almak için kullanılır. Ayrıntılı çalışma için ilgili liste sayfalarına (Siparişler,
Ürün Kartları, Üyeler, POS) sol menüden geçilir; kartların kendisi tıklanabilir değildir.

## Ekran yerleşimi
![Dashboard — beş özet kart](img/dashboard.webp)
*(1) Sol menü · (2) Üst çubuk · (3) "Dashboard" başlığı · (4) Özet kart şeridi*

1. **Sol menü** ve **üst çubuk** — tüm sayfalarda ortaktır (bkz. [Giriş](/rehber/genel/giris/)). Üst çubukta sayfa adı
   olarak "Dashboard" yazar; sol menüde **Genel → Dashboard** seçili görünür.
2. **Sayfa başlığı** — "Dashboard".
3. **Özet kart şeridi** — yan yana beş kart. Geniş ekranda tek satırda beşi birden, orta ekranda üçerli, telefonda ikişerli
   dizilir. Her kartta renkli bir simge, büyük puntoyla sayı ve altında kartın adı bulunur. Sayılar binlik ayraçla
   gösterilir (ör. `28.549`).

Sayfa açılırken sayılar hazırlanana kadar ortada dönen yükleme simgesi görünür; ardından kartlar gelir.

## Liste ve filtreler
Bu sayfada liste, filtre, arama kutusu veya dönem seçici yoktur. Kartlar sabit kapsamdadır (aşağıdaki tabloya bakın).

## Kartlar
| Kart | Simge / renk | Ne gösterir | Dönem | Tıklanınca |
|---|---|---|---|---|
| **Toplam Sipariş** | Sepet · mavi | Sistemdeki tüm siparişlerin sayısı (her durum dahil: bekleyen, onaylı, kargoda, teslim, iptal). | Tüm zamanlar | Bir yere gitmez |
| **Bekleyen Sipariş** | Sepet · turuncu | Durumu `pending` (Bekliyor) olan, henüz onaylanmamış sipariş sayısı. | Tüm zamanlar | Bir yere gitmez — bekleyenleri görmek için **Sipariş Yönetimi → Siparişler** sayfasında Bekleyen sekmesini açın |
| **Toplam Ürün** | Kutu · marka rengi | Katalogdaki ürün kartı sayısı (varyantlar ayrı sayılmaz; bir ürün kaç bedende/renkte olursa olsun 1 sayılır). | Tüm zamanlar | Bir yere gitmez |
| **Toplam Üye** | Kişiler · mor | Mağazanıza kayıtlı üye (müşteri) sayısı. | Tüm zamanlar | Bir yere gitmez |
| **Bugün POS Satış** | Kart · pembe | Kasa (POS) satış kayıtlarının sayısı. | Kart adı "bugün" dese de sayaç tarih süzgeci uygulamadan POS satış listesinin toplam kayıt sayısını gösterir. | Bir yere gitmez |

Kartlardan herhangi birinin verisi alınamazsa (ör. o modüle yetkiniz yoksa) kart **0** gösterir; hata mesajı çıkmaz.

## Butonlar ve aksiyonlar
Bu sayfada sayfaya özel buton yoktur. Üst çubuktaki ortak düğmeler (menü, `Ctrl`+`K` hızlı arama, Favorilere Ekle,
tema) her sayfada olduğu gibi burada da çalışır; bkz. [Giriş](/rehber/genel/giris/).

## Form alanları
Bu sayfada form yoktur.

## Sekmeler
Bu sayfada sekme yoktur.

## Durumlar ve iş kuralları
- Sayılar sayfa açıldığında bir kez alınır ve 30 saniye "taze" sayılır; bu süre içinde Dashboard'a tekrar gelirseniz
  aynı sayılar gösterilir, süre dolduktan sonra sayfaya her dönüşte yeniden hesaplanır. Sayfayı yenilemek (`F5`) de
  sayıları tazeler. Kartlar kendi kendine anlık güncellenmez.
- **Bekleyen Sipariş** sayacı, Sık Kullanılanlar panelindeki *Bekleyen Siparişler* kısayolunun rozetiyle aynı
  kaynaktan gelir; rozet dakikada bir tazelendiğinden ikisi kısa süre farklı görünebilir.
- Kartlar yetkiye göre gizlenmez; yetkiniz olmayan modülün kartı **0** olarak kalır.

## Adım adım
**Güne başlarken bekleyen siparişleri kontrol etme**
1. Panele giriş yapın; Dashboard açılır.
2. **Bekleyen Sipariş** kartındaki sayıya bakın.
3. Sayı sıfırdan büyükse sol menüden **Sipariş Yönetimi → Siparişler**'e gidin ve Bekleyen sekmesinden siparişleri
   işleme alın. (Alternatif: sağ kenardaki yıldız sekmesinden **Bekleyen Siparişler** kısayolu.)

**Sayıları tazeleme**
1. Sol menüden başka bir sayfaya geçip **Genel → Dashboard**'a geri dönün ya da sayfayı yenileyin (`F5`).
2. Kartlar yeniden hesaplanır.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Dashboard'daki sayılar "kaç adet" bilgisidir; tutar (ciro), grafik veya dönem karşılaştırması bu sürümde
> yoktur. Tutar ve kalem bazlı inceleme için ilgili liste sayfalarını kullanın.

> **Dikkat:** **Bugün POS Satış** kartı adının aksine yalnız bugünün değil, tüm POS satış kayıtlarının sayısını gösterir.
> Günlük kasa raporu için **Sistem → POS** sayfasındaki oturum özetini kullanın.

> **Not:** Bir kart beklenmedik biçimde **0** gösteriyorsa önce o modüle erişim yetkinizi kontrol edin; yetkisiz
> modülün sayacı sessizce sıfır kalır.

## İlgili sayfalar
- [Giriş](/rehber/genel/giris/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
- [Proje Talepleri](/rehber/genel/proje-talepleri/)
