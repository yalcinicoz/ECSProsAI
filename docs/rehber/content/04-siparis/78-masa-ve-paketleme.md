---
title: Masa ve Paketleme
route: /fulfillment/desk/:deskId
group: Sipariş Yönetimi
order: 78
summary: Zimmetli kolinin ürünlerini masa slotlarına ayırma, sipariş tamamlanınca son kontrol okutmasıyla paket + fatura + etiket oluşturma, koli kapatma ve tüm açık masaları izleme.
---

## Ne işe yarar
Masa ekranı, ara ayrıştırmadan gelen **bir kolinin** siparişlerini paketlemeye hazırlar. İki modu vardır:
**Ayrıştırma modu** — kolideki ürün okutulur, sistem ürünün gideceği **slot** (masadaki göz) numarasını dev gösterir ve
sesli söyler; sipariş tamamlanınca "PAKETLE" uyarısı verir. **Son kontrol modu** — slottaki ürünler tek tek okutulur;
hepsi doğruysa paket oluşur, fatura otomatik kesilir, fatura ve paket etiketi masadaki yazıcıya gönderilir ve slot boşalır.
Koli bitince **Koliyi Kapat** ile koli ve masa kapanır. **Masa İzleme** sayfası ise tüm açık masaların canlı
durumunu gösterir. Masa, [Koli Duvarı](/rehber/siparis/ara-ayristirma/)'ndaki **Masa Aç** butonuyla açılır.

## Ekran yerleşimi — Masa ekranı (`/fulfillment/desk/:deskId`)
![Masa ekranı — üst şerit, okutma alanı, slot paneli](img/fulfillment-desk-detay.webp)
1. **Üst şerit** — mod etiketi ("Masa Ekranı — Ayrıştırma" veya "SON KONTROL — Slot N / sipariş no"), **MASA N** + **Koli N**, **paketlenen / sipariş** sayacı, varsa **OBM** sayacı, yazdırma kuyruğu 🖨, **Koliyi Kapat** butonu.
2. **Sol: okutma alanı** — büyük okutma kutusu ve moda göre sonuç kartları (SLOT numarası, PAKETLE uyarısı, KALAN ÜRÜN, PAKET TAMAM, hata kartları).
3. **Sağ: Slotlar paneli** — dolu slotların listesi (slot no, sipariş no, ayrılan/okutulan), "Son işlem" saati, "Dolu slot" ve "Paketlenen / sipariş" özetleri. 10 saniyede bir yenilenir.

## Ekran yerleşimi — Masa İzleme (`/fulfillment/desks`)
![Masa İzleme — açık masa kartları](img/fulfillment-desks.webp)
1. **Üst şerit** — "Açık Paketleme Masaları", **açık masa** ve toplam **paketlenen / sipariş** sayaçları.
2. **Masa kartları** — masa numarasına göre sıralı; karta tıklayınca o masanın ekranı açılır. 10 saniyede bir yenilenir.

## Alanlar ve rozetler — Masa ekranı
| Öğe | Anlamı |
|---|---|
| MASA N / Koli N | Açık masa numarası ve masaya bağlı koli. |
| paketlenen / sipariş | Bu kolide paketlenen sipariş / kolideki toplam sipariş. |
| OBM | Kolide OBM'ye (Ortak Birleştirme Masası) aktarılmış sipariş sayısı; 0 ise gizli. |
| 🖨 N yazdırılıyor | Yazıcıya gönderilmeyi bekleyen belge sayısı. |
| SLOT N (yeşil dev kart) | Okutulan ürünün gideceği slot; altında sipariş no ve "Siparişte kalan: N". |
| PAKETLE N (turuncu dev kart) | Sipariş tamamlandı; "SLOT N (sipariş no)" ve **Son Kontrole Başla** butonu. |
| ASKIYA AYIRIN (kırmızı) | Ayrıştırma modunda: "Bu kolide bu ürüne ihtiyaç yok — ara ayrıştırma hatası, ürünü askıya ayırın." |
| BU SİPARİŞE AİT DEĞİL / ASKIYA AYIR (kırmızı) | Son kontrolde: okutulan ürün bu siparişe ait değil — "son ayrıştırma hatası, ürünü askıya ayırın." |
| KALAN ÜRÜN N | Son kontrolde okutulması kalan ürün sayısı. |
| PAKET TAMAM (yeşil) | Sipariş No, Paket No, Fatura No, "🖨 N belge yazıcıya gönderildi." |
| ⚠ Fatura kesilemedi: … | Paket oluştu, fatura kesilemedi; yalnız etiket basılır. |

**Slot paneli satırı:** slot numarası kutusu (gri: doluyor, turuncu: `PAKETLE` — tüm ürünleri ayrıldı, yeşil: `PAKETLENDİ`),
sipariş no, `Ayrılan x/y · Okutulan x/y`.

## Alanlar — Masa İzleme kartı
| Öğe | Anlamı |
|---|---|
| MASA N / Koli N | Masa ve bağlı koli; altında masayı açan personel. |
| Paketlenen | `x / y sipariş` ve yeşil yüzde çubuğu. |
| Dolu slot | Masada şu an dolu slot sayısı. |
| OBM | OBM'ye aktarılan sipariş sayısı (varsa). |
| Açılış / Son işlem | Masanın açılış saati ve son okutma saati. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Barkod okutma (ayrıştırma modu) | Okutma kutusu | Kolideki siparişlerden seçim yapılır (önce en az 1 ürünü slotta olan, eşitlikte en eski); siparişin slotu yoksa en küçük boş slot verilir; slot numarası gösterilir ve seslendirilir. Sipariş tamamlanırsa PAKETLE kartı + "Paketle N" sesi. | Masa açık olmalı. "Masada boş slot kalmadı (N) — önce bir sipariş paketleyin." hatası slot sayısı dolunca gelir. |
| Son Kontrole Başla | PAKETLE kartı | Son kontrol moduna geçilir; üst şerit "SON KONTROL — Slot N / sipariş" olur. | — |
| Slot satırına dokunma | Sağ slot paneli | `PAKETLE` durumundaki slot için son kontrol başlar. | Slot paketlenebilir (tüm ürünleri ayrılmış) olmalı; paketlenmiş/dolan slotlar tıklanamaz. |
| Barkod okutma (son kontrol modu) | Okutma kutusu | Siparişin ürünleri tek tek okutulur, KALAN ÜRÜN azalır; son üründe **paket** oluşur (`packed`), kargo kaydı açılır, **fatura** otomatik kesilir, fatura + etiket yazdırma kuyruğuna girer, slot boşalır. | Okutulan ürün siparişe ait olmalı; değilse hata sesi + ASKIYA AYIR. Paket kapandıktan sonra yeni okutma için önce **Ayrıştırmaya Dön**. |
| Ayrıştırmaya Dön | Son kontrol modu (alt buton / PAKET TAMAM kartı) | Ayrıştırma moduna döner. | — |
| Koliyi Kapat ⚠️ | Üst şerit (kırmızı) | Onay penceresi: "Koli N ve Masa N kapatılacak. Paketlenmemiş siparişler OBM'ye aktarılır." → **Koliyi Kapat**. Koli `closed`, masa kapanır; numaralar yeniden kullanılabilir; sonuç kartında "Paketlenen sipariş" ve "OBM'ye aktarılan" görünür. | Koli kapalı değilse. Sistem kontrolü: kolideki siparişlerin toplanmış ama henüz ayrıştırılmamış ürünü varsa "… koliye ürün gelebilir. Yine de kapatmak için onaylayın." uyarısı gelir. |
| Yine de kapat (OBM'ye aktar) ⚠️ | Kapatma penceresi, uyarı sonrası | Zorla kapatır; paketlenmemiş siparişler OBM'ye aktarılır. Geri alınamaz. | — |
| Koli Duvarına Dön | Kapatma sonucu | Görevin koli duvarına döner. | — |
| Vazgeç | Kapatma penceresi | Pencere kapanır. | — |
| Masa kartına tıklama | Masa İzleme | O masanın ekranı açılır. | — |

## Sesler
| Ses | Ne zaman |
|---|---|
| Kısa tiz bip + sesli slot numarası ("4") | Ayrıştırma modunda başarılı okutma. |
| Kısa tiz bip + sesli "Paketle 4" | Okutmayla sipariş tamamlandığında. |
| Kısa tiz bip | Son kontrolde her doğru okutma. |
| Çift pes bip | Yabancı ürün (askıya ayırın), dolu masa, kapalı masa gibi hatalar. |

## Durumlar ve iş kuralları
- **Slot ataması:** Sipariş ilk ürünüyle masaya girerken **en küçük boş slot** numarasını alır; masa slot sayısı operasyon
  profilinden gelir (varsayılan 26). Siparişin tüm ürünleri aynı slota gider.
- **Sipariş seçimi:** Okutulan ürüne ihtiyacı olan kolideki siparişlerden önce zaten slotu olan, eşitlikte en eski sipariş.
- **Son kontrol** tek tek okutmadır; sayılar tutmadan paket oluşmaz. Yabancı ürün okutulursa "askıya" ayrılır; koli
  kapanınca aidiyeti kesinleşir (OBM).
- **Paket kapanışında (tek işlemde):** paket `packed`; kanal firmasının fatura serisinden fatura kesilir (seri yoksa
  "Firma için aktif fatura serisi tanımlı değil — Ayarlar'dan seri girin." uyarısı, paket yine oluşur); kargo gönderi
  kaydı açılır ve kargo bildirimi kuyruğa düşer; siparişin tüm kalemleri paketlendiyse sipariş otomatik **Kargoya verildi**
  olur. Fatura ve etiket masadaki yazıcıya otomatik gönderilir.
- **Koli kapatma:** Tüm siparişler paketlendiyse doğrudan kapanır. Paketlenmemiş sipariş kaldıysa bunlar **OBM'ye**
  aktarılır (slotları boşalır) — OBM'de çözüm personel inisiyatifiyle bulunur. Koli ve masa numaraları boşa çıkar.
- Masa numarası sanaldır: her Masa Aç işleminde o an açık olmayan en küçük numara verilir.
- Tüm işlemler (slot atama, son kontrol, paketleme, OBM aktarımı) sipariş operasyon geçmişine yazılır.

## Adım adım
**Bir koliyi paketleme**
1. Koli Duvarı'nda koliyi **Zimmete Al**, ardından **Masa Aç**; Masa ekranı açılır.
2. Koliden ürün alın ve okutun; ekrandaki/sesteki **slot** numarasına koyun. Tekrarlayın.
3. "PAKETLE N" gelince o slottaki ürünleri toplayın ve **Son Kontrole Başla**'ya dokunun (ya da sağ panelde turuncu slota dokunun).
4. Slottaki ürünleri tek tek okutun; KALAN ÜRÜN 0 olunca "PAKET TAMAM" gelir, fatura ve etiket yazıcıdan çıkar; belgeleri pakete ekleyin.
5. **Ayrıştırmaya Dön** ile kolinin kalan ürünlerine devam edin.
6. Koli boşalınca **Koliyi Kapat → Koliyi Kapat**; sonuç kartında sayıları görüp **Koli Duvarına Dön**.

**Açık masaları izleme**
1. Sol menüden **Sipariş Yönetimi → Masa İzleme**'yi açın; masa kartlarında personel, ilerleme, dolu slot ve son işlem saatini görün.
2. Bir masaya müdahale gerekiyorsa karta tıklayarak masa ekranına geçin.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Paket kapandıktan sonra okutma kabul edilmez; sonraki ürün için önce **Ayrıştırmaya Dön**.

> **Dikkat:** "Masada boş slot kalmadı" uyarısında turuncu (PAKETLE) slotlardan birini paketleyip slot boşaltın; kolideki diğer ürünler beklesin.

> **Dikkat:** Koli kapatmadaki "koliye ürün gelebilir" uyarısı, toplanmış ama henüz ayrıştırılmamış ürünü olan sipariş bulunduğunu söyler. Ara ayrıştırma arabası bitmeden zorla kapatırsanız bu siparişler OBM'ye düşer.

> **Not:** Ekranlar 10 saniyede bir yenilenir; sayaçlar birkaç saniye geç görünebilir. Masa İzleme'de "Şu anda açık masa yok" ise Koli Duvarı'ndan **Masa Aç** yapılmamıştır.

## İlgili sayfalar
- [Ara Ayrıştırma ve Koli Duvarı](/rehber/siparis/ara-ayristirma/)
- [Toplama Planlama](/rehber/siparis/toplama-planlama/)
- [Paketleme İstasyonları](/rehber/siparis/paketleme-istasyonlari/)
- [Kargo Yönlendirme](/rehber/siparis/kargo-yonlendirme/)
