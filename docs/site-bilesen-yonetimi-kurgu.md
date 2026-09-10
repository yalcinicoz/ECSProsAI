# Site Bileşen Yönetimi — Kurgu v1 (2026-09-10, FAZ 15.4c) — ONAY BEKLİYOR

Eski panel "Site Bileşen Yönetimi" (`dizayn-site-bilesen-yonetimi`, `ekare-bilesen.js`): sayfalara **hedefli, tarihli, kurallı**
bileşenler basar. Yeni tarafta karşılığı yok; Vitrin Yönetimi blokları sayfa içeriğidir (hedefleme/kural/popup yok), Kart
Mesajları ürün kartına özeldir.

## Eski model (kod okuması)
- Bileşen: tip, hedef sayfa tipi (ana sayfa / kategori / ürün / sepet / tümü), hedef event (ad + değer: kategori id, ürün kodu,
  path), öncelik, başlangıç/bitiş, platform, **tek sefer göster** (tarayıcı) / **müşteri bazlı tek sefer**, içerik (başlık, mesaj,
  alt metin, buton metni + linki), stil (arka plan tipi düz/gradyan yön, renkler, yazı rengi, gölge, köşe, font/iç boşluk),
  görseller (masaüstü/mobil + link + alt), **kurallar** (tip · operatör · değer: üye tipi, sepet tutarı, ödeme tipi…).
- Site tarafı: layout'ta component; JS istemci sayfa/sepet/üye bağlamını gönderir, sunucu uygun bileşenleri döner, JS basar.

## Yeni taraf önerisi
1. **Entity** `storefront.site_components`: FirmPlatformId, Type (`popup` | `top_bar` | `inline_banner`), Name, TargetPageType
   (`all|home|category|product|cart|checkout`), TargetKey (kategori slug / ürün kodu / path), Priority, StartAt/EndAt, ShowOnce
   (`none|browser|member`), ContentI18n (başlık/mesaj/buton/link), Style jsonb, Images (desktop/mobile), Rules jsonb
   (`member_type`, `cart_total_gte`, `payment_method`, `device`), IsActive. Yayın snapshot'ı Vitrin Yönetimi kalıbıyla (PublishLog).
2. **Panel** Vitrin & İçerik › **Site Bileşenleri**: DataGrid liste + detay sayfası (Genel / İçerik / Stil / Görseller / Kurallar
   sekmeleri), önizleme.
3. **Site**: `GET /api/store/components?page=&key=` (bağlam: üye tipi, sepet tutarı, cihaz) → uygun bileşenler; Razor'da top bar ve
   inline banner sunucu tarafı, popup JS ile (tek sefer: localStorage / üye bayrağı). Mobil aynı uçtan okur (referans §).
4. **Tasarım**: misharix kaynağında popup/duyuru bileşeni YOK → **tasarım gerekir** (K: tasarım ekibinden 3 bileşenin HTML/CSS'i
   alınmadan site tarafı yazılmaz; K16 ve "tasarım kaynağını birebir kullan" kuralı).

## Kararlar
| # | Soru | Öneri |
|---|------|-------|
| K1 | Bileşen tipleri v1: popup + üst bar + inline banner yeterli mi? (eski: ayrıca hikaye/slider?) | Üçü. |
| K2 | Kurallar v1: üye tipi, sepet tutarı, ödeme tipi, cihaz — başka? | Bu dört. |
| K3 | Tasarım: misharix'e üç bileşen eklenecek mi, yoksa Vitrin Yönetimi'ndeki mevcut banner bloğu görselleriyle mi? | Tasarım ekibine 3 bileşen. |
| K4 | Önce panel+API (site kapalı) mi, tasarım gelince hepsi mi? | Tasarım gelince hepsi (yarım ekran açmayalım). |

Kapsam tahmini: backend 1 gün, panel 1 gün, site+mobil 1 gün (tasarım sonrası).
