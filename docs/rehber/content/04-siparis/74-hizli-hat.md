---
title: Hızlı Hat (Tek Ürünlü)
route: /fulfillment/fast-lane/:planId
group: Sipariş Yönetimi
order: 74
summary: Tek ürünlü görevlerde toplanan yığından ürün okutarak siparişi eşleştiren, paketi ve faturayı otomatik oluşturup etiketleri yazdıran masa ekranı.
---

## Ne işe yarar
Hızlı Hat, **tek ürünlü** siparişlerin paketleme ekranıdır. Toplanan ürünler ara ayrıştırma olmadan doğrudan
masaya gelir; ürün okutulur, sistem görevdeki **en eski onaylı** eşleşen siparişe verir, paketi oluşturur, faturayı
keser ve paket etiketi ile faturayı masadaki yazıcıya otomatik gönderir. Tablet ya da masa bilgisayarında,
barkod okuyucuyla kullanılır. Ekrana [Toplama Planlama](/rehber/siparis/toplama-planlama/) → görev detayı →
**Hızlı Hat Ekranı** butonuyla girilir.

## Ekran yerleşimi
![Hızlı Hat — üst şerit sayaçları, büyük okutma kutusu ve sonuç kartı](img/fulfillment-fast-lane-detay.webp)
1. **Üst şerit** — "Tek Ürünlü Hat" etiketi ve plan numarası; **paketlenen** (yeşil) ve **hata** (kırmızı) oturum sayaçları; yazdırma kuyruğu varsa 🖨 sayacı ("yazdırılıyor").
2. **Büyük okutma kutusu** — "Ürün barkodunu okut"; odak sürekli burada kalır, "Barkod bekleniyor ●" / "İşleniyor..." yazar.
3. **Sonuç alanı** — başarıda yeşil "Paketlendi" kartı, hatada kırmızı "Bu ürüne ihtiyaç yok / DEPO İADESİNE AYIR" kartı; hiç okutma yoksa açıklama kartı.

## Alanlar ve rozetler
| Öğe | Anlamı |
|---|---|
| paketlenen | Bu ekran oturumunda başarıyla paketlenen sipariş sayısı (sayfa yenilenince sıfırlanır). |
| hata | Oturumdaki hatalı okutma sayısı. |
| 🖨 N yazdırılıyor | Yazıcıya gönderilmeyi bekleyen belge sayısı; belgeler sırayla basılır. |
| Paketlendi kartı | Sipariş No, Paket No, Fatura No (kesildiyse) ve "🖨 N belge yazıcıya gönderildi." |
| ⚠ Fatura kesilemedi: … | Paket oluştu ama fatura kesilemedi; akış devam eder, yalnız paket etiketi basılır. |
| Kırmızı kart | "Bu ürüne ihtiyaç yok — DEPO İADESİNE AYIR" + hata mesajı. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Barkod okutma | Okutma kutusu (Enter ile gönderilir) | Görevde bu barkoda ihtiyacı olan **en eski onaylı** sipariş seçilir; satır toplanmış + son kontrolü yapılmış sayılır; **paket** oluşur (`packed`), kargo kaydı açılır, **fatura** otomatik kesilir; fatura ve paket etiketi yazdırma kuyruğuna girer. | Görev tek ürünlü olmalı. Eşleşme yoksa "Bu ürüne bu görevdeki hiçbir siparişin ihtiyacı yok — depo iadesine ayırın." + hata sesi. |
| (otomatik) yazdırma | Arka planda | Fatura ve paket etiketi sayfaları sırayla gizli olarak açılır ve yazdırılır; kullanıcı tetiklemez. | Tarayıcı yazdırma diyaloğu sormayacak şekilde (kiosk yazdırma) ayarlanmış olmalı. |

Bu ekranda başka buton yoktur; işlem sırasında (İşleniyor...) yeni okutma kabul edilmez.

## Sesler
| Ses | Ne zaman |
|---|---|
| Kısa tiz bip | Başarılı eşleşme ve paketleme. |
| Çift pes bip | Eşleşmeyen ürün (ihtiyaç yok) — ürünü depo iadesi yığınına ayırın. |

## Durumlar ve iş kuralları
- **En eski onaylı sipariş kuralı:** Aynı ürüne ihtiyacı olan birden çok sipariş varsa, sipariş tarihi en eski olan kazanır.
- Tek ürünlü hatta ayrıştırma ve ayrı son kontrol yoktur; okutma = toplama + son kontrol + paketleme.
- **Paket başına fatura:** Fatura, paket kapanırken kanalın firmasına tanımlı fatura serisinden kesilir. Seri yoksa
  "Firma için aktif fatura serisi tanımlı değil — Ayarlar'dan seri girin." uyarısı görünür; paket yine oluşur, sadece etiket basılır, fatura sonra sipariş detayından kesilebilir.
- **Kargo zinciri:** Paket oluşunca kargo gönderi kaydı açılır ve kargo bildirimi kuyruğa düşer; siparişin tüm kalemleri
  paketlenince sipariş otomatik **Kargoya verildi** (`shipped`) olur ve stok fiili raftan düşer. Kuyruk
  [Kargo Yönlendirme](/rehber/siparis/kargo-yonlendirme/) ekranından izlenir.
- Görevin bitmesi beklenmez; siparişler tek tek akar. Görev kaydı Toplama Planlama'dan **Tamamla** ile kapatılır.
- Her okutma sipariş operasyon geçmişine yazılır (paketleme, fatura, etiket).

## Adım adım
**Tek ürünlü yığını paketleme**
1. Toplama Planlama → tek ürünlü görev → **Hızlı Hat Ekranı**.
2. Masadaki yazıcının açık olduğundan emin olun; ekrana bir kez dokunun (ses ve odak için).
3. Yığından ürünü alın, barkodunu okutun. Yeşil "Paketlendi" kartında sipariş/paket/fatura numaralarını görün; etiket ve fatura yazıcıdan çıkar.
4. Çıkan belgeleri pakete yapıştırın, sonraki ürüne geçin.
5. Kırmızı kart gelirse ürünü **depo iadesi** yığınına ayırın ve devam edin.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** 🖨 sayacı birikiyorsa yazıcı kuyruğunu kontrol edin; belgeler sırayla basılır, ekran çalışmaya devam eder.

> **Dikkat:** "Fatura kesilemedi" uyarısında paket ve kargo kaydı oluşmuştur; fatura serisini Ayarlar'dan tanımlayıp faturayı sipariş detayından kesin. Sorunu görmezden gelirseniz paket faturasız çıkar.

> **Not:** Aynı üründen yığında fazla varsa (eşleşecek sipariş kalmadıysa) ekran "ihtiyaç yok" der — bu bir yanlış toplama göstergesidir; ürün depo iadesine ayrılır.

## İlgili sayfalar
- [Toplama Planlama](/rehber/siparis/toplama-planlama/)
- [Ürün Toplama](/rehber/siparis/urun-toplama/)
- [Kargo Yönlendirme](/rehber/siparis/kargo-yonlendirme/)
- [Siparişler](/rehber/siparis/siparisler/)
- [Sipariş Detayı — Operasyon Geçmişi](/rehber/siparis/siparis-detay/)
