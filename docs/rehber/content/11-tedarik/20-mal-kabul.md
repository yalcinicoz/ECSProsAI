---
title: Mal Kabul
route: /procurement/receipts
group: Tedarik
order: 20
summary: Gelen kolilerin parti olarak kaydedildiği ekran; kalem bilgisi zorunlu değildir, ayrıştırma hemen başlayabilir; evrak kalemleri ve satın alma bağları yalnız dönemsel mutabakat raporuna girdidir.
---

## Ne işe yarar
Tedarikçiden gelen koliler **Mal Kabul Partisi** olarak kaydedilir. Parti açarken yalnız tedarikçi ve depo
zorunludur — "koli geldi" demektir; kalem bilgisi, irsaliye, fatura sonradan eklenebilir ya da hiç eklenmez.
Asıl süreç (ayrıştırma + etiketleme) partiyi beklemez; buradaki bilgiler dönemsel mutabakat raporuna girdidir.
Satıcı fazla/eksik gönderebilir; kesin eşleşme aranmaz.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | `MK-YYYYAAGG-0001` — otomatik. |
| TEDARİKÇİ / DEPO / TARİH | Parti kimliği. |
| KOLİ / İRSALİYE | Koli sayısı ve irsaliye no (opsiyonel). |
| SA BAĞI / FATURA | Bağlı satın alma sayısı; fatura bağlıysa ✓. |
| DURUM | `Teslim Alındı` → `Ayrıştırılıyor` → `Tamamlandı` (geri açılabilir). |

Satıra tıklayınca detay açılır. Arama kod ve irsaliye numarasında çalışır.

## Detay sayfası
- **Başlık**: koli, irsaliye no, not — tamamlanmamış partide düzenlenir; durum butonları:
  **Ayrıştırmaya Başla** / **Tamamla** / **Geri Aç**.
- **Bağlı Satın Almalar**: aynı tedarikçinin SA'ları bağlanır/çözülür (çoktan-çoğa — birkaç SA tek partide
  gelebilir). Bağ **bilgi amaçlıdır**; kalem eşleşmesi zorlanmaz. `Sipariş Verildi` durumundaki SA bağlanınca
  kendiliğinden `Teslim Alınıyor` olur. Başka tedarikçinin SA'sı bağlanamaz.
- **Tedarikçi Faturası**: opsiyonel bağ; mutabakat raporunda "fatura tutarı" sütununu besler.
- **Evrak Kalemleri (kaba)**: teslim evrakında yazan haliyle ("t-shirt, 1000 adet, 15 TL"); açıklama zorunlu,
  adet/fiyat opsiyonel. Ayrıştırmayı hiçbir şekilde kısıtlamaz.

## Durumlar ve iş kuralları
- `Tamamlandı` = "bu partide ayrıştırılacak bir şey kalmadı" beyanı; elle verilir, **Geri Aç** ile açılır.
- Tamamlanmış partide başlık/kalem düzenlenemez.
- Hiçbir alan stok üretmez — stok girişi ayrıştırma + yerleştirme adımındadır (sonraki sürüm).

## İlgili sayfalar
- [Satın Almalar](/rehber/tedarik/satin-almalar/)
