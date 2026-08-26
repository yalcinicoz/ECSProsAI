---
title: Tedarik Raporu
route: /procurement/report
group: Tedarik
order: 50
summary: Dönemsel mutabakat (satın alınan ↔ sayılan ↔ fatura — kesin değildir) ve hız göstergeleri; yerleştirme bekleyenler, satışa girmeyenler ve açık kart-eksik bildirimleri.
---

## Ne işe yarar
Tedarik sürecinin dönemlik fotoğrafı. **Mutabakat kesin değildir** — teslim evrakları kabadır, satıcılar fazla
gönderir; sayılar dönem bütününde deneyimli gözle yorumlanır. Asıl izlenen: teslim alınan ürünün **ne kadar
sürede satışa girdiği** ve **satışa girmeyenlerin nerede takıldığı**.

## Filtreler
Başlangıç / Bitiş (varsayılan son 30 gün; bitiş günü dahil) + Tedarikçi.

## KPI kartları
| Kart | Anlamı |
|---|---|
| Teslim → Sayım | Partinin teslim alınmasından sayım okutmasına ortalama süre (partili sayımlar). |
| Sayım → Satışa Giriş | Sayımdan ürünün sitede yayına girmesine ortalama süre. Damga 6 saatte bir işlenir (gün hassasiyeti). |
| Yerleştirme Bekleyen | Sayıldı ama rafa konmadı: kayıt/adet + yaş kovaları (0-2 / 3-7 / 7+ gün — 7+ varsa kart turuncu). |
| Satışa Girmeyen | Yerleşti (stok girdi) ama sitede yayında değil. |
| Açık Kart-Eksik | Katalog sorumlusunu bekleyen bildirimler + en eskisinin yaşı. |

## Dönemsel Mutabakat tablosu
Tedarikçi başına: SA sayısı/adedi/tutarı (dönemdeki satın almalar, iptaller hariç) ↔ **sayılan adet** ve sayım
maliyeti (maliyet girilen kayıtlardan) ↔ partilere bağlı **fatura tutarı**. **FARK = sayım − SA**: pozitif
"fazla gönderim" (satıcıya fiyat revizyonu konuşulur), negatif eksik; yüzde SA adedine göredir. Partisiz
sayımlar "— Partisiz —" satırında toplanır.

## Satışa Girmeyenler
Yerleşmiş ama yayına girmemiş ürünler (en eski 100): adet, yerleşme tarihi, bekleme süresi (7+ gün turuncu),
**Ürüne git** bağlantısı. Sebep (görsel yok, fiyat 0, kanal kararı, stok…) ürün detayında ve
[Kanal Ürünleri](/rehber/vitrin/kanal-urunleri/) ekranındaki sebep rozetlerinde görünür.

## Kurallar
- "Satışa girdi" = stok girmiş **ve** herhangi bir site kanalında yayında; damga ilk giriş anıdır, geri alınmaz.
- Yetki: **Tedarik Yönetimi** (`procurement.manage`).

## İlgili sayfalar
- [Satın Almalar](/rehber/tedarik/satin-almalar/) · [Mal Kabul](/rehber/tedarik/mal-kabul/) · [Sayım / Teslim](/rehber/tedarik/sayim-teslim/)
