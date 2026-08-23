---
title: Hediye Kartı
route: /orders/gift-cards
group: Pazarlama
order: 30
summary: Hediye kartlarının (bakiye kartı) listelendiği, arandığı ve yeni kart oluşturulduğu ekran; kartın tutarı, kalan bakiyesi, geçerlilik süresi ve durumu izlenir.
---

## Ne işe yarar
Hediye kartı, belirli bir tutarla oluşturulan ve müşterinin alışverişte bakiyesini harcayabildiği bir koddur
(`GC-XXXX-XXXX-XXXX`). Pazarlama/müşteri hizmetleri ekibi burada kart oluşturur, kart kodunu müşteriye iletir ve
kartın kalan bakiyesini/durumunu izler. Kart kullanımı (bakiye düşme) sipariş tarafında kod girilerek yapılır; bu
ekran kullanım yapmaz, yalnız oluşturur ve izler.

## Ekran yerleşimi
![Hediye Kartları listesi](img/orders-gift-cards.webp)
1. **Başlık ve sayaç** — "Hediye Kartları" ve toplam kayıt; sağda **+ Yeni Hediye Kartı**.
2. **Sekmeler** — `Aktif` / `Tümü`.
3. **Arama şeridi** — "Kart kodu ara…" + **Ara**.
4. **Tablo** — kart satırları (satır tıklanmaz; detay sayfası yoktur).
5. **Sayfalama** — 20 kayıt/sayfa.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Kart kodu (`GC-…`), sistem üretir. |
| TUTAR | Kartın ilk yüklenen tutarı (₺). |
| KALAN | Harcanabilir kalan bakiye (₺). |
| GEÇERLİLİK | Başlangıç → bitiş; bitiş boşsa `süresiz`. |
| TEK KULLANIM | `Evet` ise ilk kullanımda kalan bakiye ne olursa olsun kart kapanır; `Hayır` ise bakiye bitene kadar tekrar kullanılır. |
| DURUM | `Aktif` (yeşil) · `Kullanıldı` (gri) · `Bakiye Bitti` (gri) · `Süresi Doldu` (sarı) · `İptal` (kırmızı). |

| Sekme / Filtre | Ne yapar |
|---|---|
| `Aktif` | Yalnız durumu Aktif olan kartlar (varsayılan). |
| `Tümü` | Tüm durumlar. |
| Kart kodu ara… + Ara | Kodun bir bölümüyle arar (büyük/küçük harf önemsiz). |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Hediye Kartı | Sağ üst | "Yeni Hediye Kartı" penceresi açılır. | Panele giriş yeterli. |
| Oluştur | Pencere altı | Kart oluşturulur, kod sistemce üretilir, listeye düşer. | Firma seçili, Tutar > 0, Geçerlilik Başlangıcı dolu. |
| Vazgeç | Pencere altı | Pencere kapanır, kart oluşmaz. | — |

> **Dikkat:** Bu ekranda kart düzenleme, iptal etme ya da bakiye değiştirme butonu yoktur. Kart oluşturulduktan sonra
> tutar/tarih değiştirilemez; yanlış kart için yeni kart oluşturun ve eski kodu müşteriye vermeyin.

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Firma | Evet | Kartın ait olduğu firma. |
| Tutar (₺) | Evet | Karta yüklenen tutar; sıfırdan büyük olmalı. Para birimi TL'dir. |
| Tek kullanımlık | — | İşaretliyse ilk kullanımda kart kapanır (artan bakiye yanar). |
| Geçerlilik Başlangıcı | Evet | Varsayılan bugün. Bu tarihten önce kart kullanılamaz. |
| Bitiş (boş = süresiz) | Hayır | Son geçerli tarih; boşsa süresiz. |

## Durumlar ve iş kuralları
- Yeni kart `Aktif` ve KALAN = TUTAR ile başlar; kod `GC-` önekiyle rastgele üretilir, elle kod verilemez.
- Kullanımda kontroller ve müşteriye dönen mesajlar: kod yok → "Geçersiz hediye kartı kodu."; durum Aktif değil →
  "Hediye kartı aktif değil."; başlangıç gelmemiş → "Hediye kartı henüz geçerli değil."; bitiş geçmiş → "Hediye kartının
  geçerlilik süresi dolmuş."; bakiye 0 → "Hediye kartı bakiyesi tükenmiş."
- Kullanılan tutar kalan bakiyeyi aşamaz; fazlası kullanılmaz (bakiye kadar düşer).
- Bakiye sıfırlanınca **ya da** kart tek kullanımlıksa ilk kullanımdan sonra durum `Kullanıldı` olur.
- Her kullanım için işlem kaydı (kullanılan tutar, kalan bakiye) tutulur.

## Adım adım
**Müşteriye 500 ₺ hediye kartı verme**
1. **+ Yeni Hediye Kartı** → Firma seçin, Tutar `500`, Bitiş'e bir yıl sonrası, "Tek kullanımlık" işaretsiz → **Oluştur**.
2. Listede oluşan `GC-…` kodunu kopyalayıp müşteriye iletin.
3. Müşteri kullandıkça KALAN sütunu azalır; 0 olunca DURUM `Kullanıldı` olur.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Müşteri "kart geçmiyor" dediğinde `Tümü` sekmesinde kodu aratın; DURUM ve GEÇERLİLİK sütunları nedeni
> gösterir.

> **Dikkat:** "Tek kullanımlık" kartta müşteri bakiyenin tamamını tek seferde kullanmazsa kalan yanar; promosyon
> kartlarında bilinçli seçin.

## İlgili sayfalar
- [Kuponlar](/rehber/pazarlama/kuponlar/)
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
