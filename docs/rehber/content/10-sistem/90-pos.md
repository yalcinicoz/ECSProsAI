---
title: POS Satışları ve Kasalar
route: /pos/sales
group: Sistem
order: 90
summary: Mağaza kasasından (POS) yapılan satışların listesi ve detayı, satış iadesi; tanımlı kasaların listesi.
---

## Ne işe yarar
Fiziksel mağazada kasa (POS) üzerinden yapılan satışlar bu ekranda izlenir. Mağaza sorumlusu günün fişlerini
kontrol eder, bir fişin kalemlerini ve ödemelerini açar, gerekirse tamamlanmış bir satışı iade eder. İade edilen
satışın ürünleri stoğa geri döner. Tanımlı kasalar (kod, ad, fiş öneki) ayrı bir alt sayfada, **Kasalar**'da
listelenir.

Sol menüde **Sistem > POS** bağlantısı bu sayfayı açar. Ayrı bir izin gerekmez. Satış yapma (POS terminali),
oturum açma/kapatma ve gün sonu raporu panelde yer almaz; bunlar kasa uygulamasından yürür — panel yalnız sonuçları
gösterir ve iade aldırır.

## Ekran yerleşimi
![POS Satışları — durum sekmeleri ve fiş listesi](img/pos-sales.webp)
1. **Başlık satırı** — "POS Satışları" ve toplam kayıt sayısı.
2. **Sekmeler** — `Tümü` / `Tamamlanan` / `İade` hızlı durum filtresi.
3. **Tablo** — fişler; satıra tıklayınca satış detay penceresi açılır.
4. **Sayfalama** — `← Önceki  1 / N  Sonraki →`; sayfa boyutu 20.
5. **Satış detay penceresi** — durum rozeti + tarih, KALEMLER listesi, ÖDEMELER listesi, toplamlar, **İade Et** / **Kapat**.

![Satış detay penceresi — kalemler, ödemeler, toplamlar ve İade Et butonu](img/pos-sales--detay-modal.webp)

![Kasalar listesi — kod, ad, fiş öneki, durum](img/pos-registers.webp)

## Liste ve filtreler
### POS Satışları (`/pos/sales`)
| Sütun | Anlamı |
|---|---|
| FİŞ NO | Satışın fiş numarası (kasanın fiş önekiyle başlar). |
| TUTAR | Fişin genel toplamı (₺, iki ondalık). |
| TARİH | Satışın yapıldığı tarih ve saat. |
| DURUM | `Tamamlandı` (yeşil) / `İade Edildi` (kırmızı). Başka bir durum kodu gelirse gri rozetle kod adı görünür. |
| (son sütun) | "Detay →" ipucu; satır tıklanabilir. |

| Filtre | Ne yapar |
|---|---|
| Tümü | Tüm fişler. |
| Tamamlanan | Yalnız `Tamamlandı` durumundaki satışlar. |
| İade | Yalnız `İade Edildi` durumundaki satışlar. |

- Arama kutusu ya da tarih/kasa filtresi yoktur; sekme değişince liste 1. sayfaya döner.
- Satıra tıklayınca "Satış: {fiş no}" başlıklı detay penceresi açılır.

### Kasalar (`/pos/registers`)
Sol menüde ayrı bağlantısı yoktur; `/admin/pos/registers` adresinden açılır. Aktif ve pasif tüm kasalar, koda göre
sıralı, tek sayfada listelenir. Satır tıklaması ve düzenleme yoktur.

| Sütun | Anlamı |
|---|---|
| KOD | Kasanın kısa kodu. |
| AD | Kasanın adı (ör. mağaza/konum). |
| FİŞ ÖNEKİ | Bu kasadan kesilen fişlerin numara öneki. |
| DURUM | `Aktif` (yeşil) / `Pasif` (gri). Pasif kasadan satış yapılamaz. |

## Satış detay penceresi
| Bölüm | İçerik |
|---|---|
| Üst satır | Durum rozeti ve satış tarihi-saati. |
| KALEMLER | Her satırda ürün adı, `adet × birim fiyat` ve satır toplamı. |
| ÖDEMELER | Her satırda ödeme yöntemi ve tutar; nakitte varsa "para üstü …" notu. Yöntemler: `Nakit`, `Kredi Kartı`, `Havale`, `Online`, `POS`. |
| Toplamlar | Ara toplam; indirim varsa "İndirim: -…"; kalın **Genel toplam**. |
| Alt şerit | Solda **İade Et** (yalnız `Tamamlandı` satışta), sağda **Kapat**. Hata mesajları toplamların altında kırmızı görünür. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Sekme (Tümü / Tamamlanan / İade) | Liste üstü | Listeyi duruma göre süzer. | — |
| Satır tıklama | Tablo | Satış detay penceresini açar. | — |
| İade Et ⚠️ | Detay penceresi alt sol | "İade nedeni:" diyalogu açılır; neden yazılıp onaylanınca satış `İade Edildi` olur, fişteki tüm ürünler ilgili depoya stok olarak geri eklenir, neden satış notuna "İade: …" olarak eklenir. Liste ve detay yenilenir. **Geri alınamaz** ve kısmi iade yoktur — fişin tamamı iade edilir. | Satış `Tamamlandı` durumunda olmalı; neden boş bırakılırsa işlem yapılmaz. |
| Kapat | Detay penceresi alt sağ | Pencereyi kapatır. | — |

## Durumlar ve iş kuralları
| Rozet | Anlamı |
|---|---|
| `Tamamlandı` (`completed`) | Satış kapanmış, ödeme alınmış, ürünler stoktan düşmüştür. İade edilebilir. |
| `İade Edildi` (`refunded`) | Satış iade edilmiş, ürünler stoğa geri dönmüştür. Tekrar iade edilemez. |

- Geçiş tek yönlüdür: `completed` → `refunded`. İade dışında durum değişikliği yapılamaz.
- Satış tamamlandığında stok otomatik düşer; iadede aynı miktarlar otomatik geri gelir. Stok hareketlerini
  **Stok > Stok Hareketleri** ekranından izleyebilirsiniz.
- İade edilmiş bir satış yeniden iade edilmeye çalışılırsa "Yalnızca tamamlanmış satışlar iade edilebilir." hatası
  görünür (zaten buton da gizlenir).
- POS satışları web siparişlerinden ayrıdır; **Sipariş Yönetimi** listelerinde görünmez.

## Adım adım
**Bir fişin içeriğini kontrol etme**
1. **Sistem > POS** sayfasını açın; gerekirse `Tamamlanan` sekmesine geçin.
2. Fiş numarasına göre satırı bulun ve tıklayın.
3. KALEMLER ve ÖDEMELER bölümlerini, altındaki toplamları inceleyin; **Kapat** ile çıkın.

**Satış iadesi alma**
1. İlgili fişin satırına tıklayın; durumun `Tamamlandı` olduğundan emin olun.
2. **İade Et** butonuna basın, açılan diyaloga iade nedenini yazın (ör. `Müşteri vazgeçti`) ve onaylayın.
3. Rozet `İade Edildi` olur; fiş artık `İade` sekmesinde listelenir ve ürünler stoğa döner.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** İade geri alınamaz ve fişin tamamını kapsar. Tek kalem iadesi gerekiyorsa kasa uygulamasında yeni
> işlem yapılmalıdır; panelden kısmi iade yapılamaz.

> **İpucu:** Nakit ödemede "para üstü" notu, müşteriye verilen üstü gösterir; ÖDEMELER'deki tutar fişe sayılan
> kısımdır.

> **Not:** Listede belirli bir tarih ya da kasaya göre süzme yoktur; fişler en yeniden eskiye sıralıdır, sayfalama ile
> geriye gidin.

Bilinen sınırlar: panelde kasa oluşturma/düzenleme, oturum açma-kapatma ve gün sonu kasa raporu ekranı yoktur;
satış detayında kasa/oturum bilgisi gösterilmez.

## İlgili sayfalar
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
- [Denetim Logları](/rehber/sistem/denetim-loglari/)
