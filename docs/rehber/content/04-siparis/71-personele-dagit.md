---
title: Personele Dağıt
route: /fulfillment/assignments
group: Sipariş Yönetimi
order: 71
summary: Aktif toplama görevlerinde personel başına atanan/toplanan/kalan satır sayıları; atanmamış havuzdan sayıyla dağıtım ve personeller arası aktarım.
---

## Ne işe yarar
Eski paneldeki "Kullanıcı Toplama Yönetimi"nin karşılığı. Bekleyen ve toplanan görevlerde her personelin yükünü gösterir;
"şu personele N satır ver" ve "A'dan B'ye N satır aktar" işlemleri buradan yapılır. Satır seçerek dağıtım (belirli ürünler)
**Görev Detayı**'ndadır; bu sayfa sayıya göre çalışır.

## Ekran yerleşimi
1. **Aktif görevler** (sol) — görev no, durum, satır/atanmamış/toplanan sayıları; tıklayınca seçilir.
2. **Personel tablosu** — seçili görevde personel · atanan · toplanan · kalan; satırda **bundan aktar**.
3. **Havuzdan dağıt / aktar** — hedef personel + satır sayısı → **Dağıt** (havuzdan) ya da **Aktar** (kaynak personelden).

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Dağıt | Alt kart | Görevin atanmamış satırlarından rota sırasına göre N tanesi hedef personele atanır (Görev Detayı'ndaki atamayla aynı kayıt/olay). | Görev bekleyen/toplanıyor; `fulfillment.manage` |
| bundan aktar → Aktar | Personel satırı → alt kart | Kaynak personelin henüz toplamadığı satırlarından N tanesi hedefe geçer; toplanmış satırlar aktarılmaz. | Kaynak ≠ hedef |
| Yenile | Başlık | Özeti yeniler (30 sn'de bir kendiliğinden de yenilenir). | — |
| Görev detayı → | Görev kartı | Satır bazlı dağıtım ekranı. | — |

## Durumlar ve iş kuralları
- Yalnız `pending` (bekliyor) ve `picking` (toplanıyor) görevler listelenir.
- "Görevde atanmamış satır kalmadı" / "Kaynak personelin bekleyen satırı yok" hataları istenen sayı kadar satır bulunamadığında değil, hiç satır yokken gelir; bulunan kadarı dağıtılır ve sonuç mesajında sayı görünür.
- Dağıtım personelin **Ürün Toplama** listesine anında düşer.

## İlgili sayfalar
- [Toplama Planlama](/rehber/siparis/toplama-planlama/)
- [Ürün Toplama](/rehber/siparis/urun-toplama/)
