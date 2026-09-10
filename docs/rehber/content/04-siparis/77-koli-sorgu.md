---
title: Koli Sorgu
route: /fulfillment/box-lookup
group: Sipariş Yönetimi
order: 77
summary: Set (toplama görevi), koli, sipariş no, tarih ya da personel ile kolideki siparişleri masa/yuva bilgisi, sipariş durumu ve fatura tarihiyle listeler.
---

## Ne işe yarar
Eski paneldeki "Set Koli Sipariş Sorgula"nın karşılığı. "Bu sipariş hangi kolide, o kolide başka hangi siparişler var,
hangi masada/yuvada?" sorusuna yanıt verir. **Sipariş no** girilirse o siparişin kolisindeki TÜM siparişler döner.

## Ekran yerleşimi
1. **Filtre formu** — Set / görev no (tam ya da sonu), Koli no, Sipariş no, Başlangıç–Bitiş (plan tarihi), Personel (koli zimmeti) → **Sorgula**.
2. **Koli kartları** — her koli için başlık (set, koli no/durumu, masa, istasyon, zimmet, plan tarihi, "Koli duvarı →") ve sipariş tablosu.

| Sütun | Anlamı |
|---|---|
| YUVA | Siparişin paketleme masasındaki yuva numarası. |
| GÖZ | Ayrıştırma gözü numarası ve durumu (boş / doluyor / hazır). |
| SİPARİŞ | Sipariş no (detaya bağlantı). |
| MÜŞTERİ / SİPARİŞ TARİHİ / DURUM | Sipariş modülünden. |
| TOPLAMA | Toplanan / toplam satır. |
| FATURA TARİHİ | İptal edilmemiş son faturanın tarihi; yoksa "—". |

## Durumlar ve iş kuralları
- En az bir ölçüt zorunludur ("En az bir ölçüt girin…"). Sonuç 500 satırla sınırlıdır; daraltın.
- Sipariş no verildiğinde diğer ölçütler yok sayılır (koli bütünü listelenir). Sipariş henüz göze girmemişse boş döner.
- Koli yoksa (tek ürünlü akış) sipariş yalnız kendi gözüyle görünür; koli no "—".

## İlgili sayfalar
- [Ara Ayrıştırma](/rehber/siparis/ara-ayristirma/)
- [Masa ve Paketleme](/rehber/siparis/masa-ve-paketleme/)
