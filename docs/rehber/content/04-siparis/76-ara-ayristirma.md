---
title: Ara Ayrıştırma ve Koli Duvarı
route: /fulfillment/sorting/:planId
group: Sipariş Yönetimi
order: 76
summary: Çok ürünlü görevlerde toplanan ürünleri okutarak koli numarası alma (okutma ekranı) ve kolilerin canlı doluluk/renk durumunu izleyip zimmete alma (koli duvarı).
---

## Ne işe yarar
Çok ürünlü görevlerde toplama arabasından gelen karışık ürünler, önce **kolilere** ayrılır. Ara Ayrıştırma okutma
ekranında ürün barkodu okutulur; sistem ürünün hangi siparişe ait olduğuna karar verir, o siparişin kolisinin numarasını
ekranda **dev** gösterir ve sesli söyler; personel ürünü o koliye atar. **Koli Duvarı** ise aynı görevin açık
kolilerini kart kart gösterir: doluluk, tamamlanma yüzdesi, renk, zimmet (hangi personel/masa). Koliler dolunca
**Zimmete Al** ile bir personel üstlenir ve **Masa Aç** ile [Masa ekranında](/rehber/siparis/masa-ve-paketleme/)
paketlemeye geçer. İki ekran da tablet için tasarlanmıştır; Toplama Planlama → çok ürünlü görev detayındaki
**Ara Ayrıştırma** / **Koli Duvarı** butonlarıyla açılır.

## Ekran yerleşimi — Okutma ekranı (`/fulfillment/sorting/:planId`)
![Ara Ayrıştırma okutma ekranı — sayaçlar, okutma kutusu ve dev koli numarası](img/fulfillment-sorting-detay.webp)
1. **Üst şerit** — "Ara Ayrıştırma" etiketi + plan no; **okutulan** (yeşil) ve **iade** (kırmızı) oturum sayaçları; **Koli Duvarı** butonu.
2. **Büyük okutma kutusu** — "Ürün barkodunu okut"; odak sürekli burada.
3. **Sonuç alanı** — başarıda dev koli numarası kartı (gerekirse "★ YENİ KOLİ ★" şeridi), hatada kırmızı "İHTİYAÇ YOK / DEPO İADESİNE AYIR" kartı.

## Ekran yerleşimi — Koli Duvarı (`/fulfillment/sorting-wall/:planId`)
![Koli Duvarı — renkli koli kartları ve özet sayaçlar](img/fulfillment-sorting-wall-detay.webp)
1. **Üst şerit** — "Koli Duvarı" + plan no; **aktif koli**, **kolisiz sipariş** (sarı), **kapalı koli** (yeşil) sayaçları; **Okutma Ekranı** butonu.
2. **Koli kartları** — koli numarasına göre sıralı; sol kenar ve numara kutusu koli rengiyle (yeşil/sarı/kırmızı).
3. 10 saniyede bir kendiliğinden yenilenir.

## Okutma ekranı alanları
| Öğe | Anlamı |
|---|---|
| okutulan | Bu oturumda başarıyla koliye yönlendirilen ürün sayısı. |
| iade | Bu oturumda "ihtiyaç yok" alan (depo iadesine ayrılan) ürün sayısı. |
| Koli N (dev numara) | Ürünün gideceği koli numarası; aynı anda sesli söylenir. |
| ★ YENİ KOLİ ★ | Bu okutmayla yeni bir koli açıldı — boş bir koliyi bu numarayla etiketleyin. |
| Sipariş No | Ürünün verildiği sipariş. |
| Siparişin kalan ürünü | O siparişin koliye girmesi gereken kalan ürün sayısı. |
| Kolide sipariş | Bu kolideki sipariş sayısı. |

## Koli kartı alanları (Koli Duvarı)
| Öğe | Anlamı |
|---|---|
| Koli numarası (renkli kutu) | Koli no; `(gen.N)` aynı numaranın kaçıncı kez kullanıldığı. |
| `MASADA — Masa N` (mor rozet) | Koli zimmete alınmış; masa açıldıysa masa numarası. |
| Sipariş | `X sipariş (Y tamam)` — kolideki sipariş sayısı ve tüm ürünleri kolide olan sipariş sayısı. |
| Ürün | `giren / gereken` — koliye giren ürün / kolideki siparişlerin toplam ihtiyacı. |
| Yüzde çubuğu | Koli içi tamamlanma yüzdesi (renk koliyle aynı). |
| Son okutma | Bu koliye son ürün okutulma saati. |
| Zimmet | Koliyi alan personel ve saat (zimmetli kolilerde). |
| Renk | Yeşil: tüm ürünleri kolide olan sipariş oranı ≥ yeşil eşik (varsayılan %100); Sarı: ≥ sarı eşik (varsayılan %70); Kırmızı: altı. Eşikler operasyon profilinden gelir. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Barkod okutma | Okutma ekranı | Ürün, kurallara göre bir siparişe ve koliye verilir; koli numarası gösterilir ve seslendirilir; "okutulan" artar. | Görevde bu ürüne ihtiyacı olan sipariş olmalı; yoksa hata sesi + "Bu ürüne bu görevdeki hiçbir siparişin ihtiyacı yok — depo iadesine ayırın." ve "iade" artar. |
| Koli Duvarı | Okutma ekranı üst şerit | Koli Duvarı'na geçer. | — |
| Okutma Ekranı | Koli Duvarı üst şerit | Okutma ekranına döner. | — |
| Zimmete Al | Açık (`open`) koli kartı | Onay penceresi: "Koli N (gen.X) zimmetinize alınacak … Paketleme bitene kadar koli sizde kalır." → **Zimmete Al** ile koli `taken` olur, kartta MASADA rozeti ve zimmet bilgisi görünür. | Koli açık olmalı; "Koli zaten başka bir personelin zimmetinde." / "Kapalı koli alınamaz." hataları. |
| Vazgeç | Zimmet penceresi | Pencere kapanır. | — |
| Masa Aç | Kendi zimmetinizdeki koli kartı | En küçük boş masa numarasıyla sanal masa açılır (zaten açıksa mevcut masaya gidilir) ve Masa ekranı açılır. | Koli sizin zimmetinizde olmalı; "Koli başka personelin zimmetinde." / "Kapalı koli için masa açılamaz." |

## Sesler
| Ses | Ne zaman |
|---|---|
| Kısa tiz bip + sesli koli numarası ("7") | Başarılı okutma — yalnız numara söylenir. |
| Çift pes bip | İhtiyaç yok — ürünü depo iadesine ayırın. |

## Durumlar ve iş kuralları
| Koli durumu | Anlamı |
|---|---|
| `open` | Açık, doluyor; Zimmete Al görünür. |
| `taken` | Bir personelin zimmetinde (MASADA rozeti); dolmaya devam edebilir. |
| `closed` | Masada kapatıldı; numarası yeniden kullanılabilir (yeni kullanımda gen +1). |

- **Sipariş seçimi (okutulan ürün hangi siparişe?)** — sırayla: (1) en az 1 ürünü zaten kolide olan siparişler,
  (2) tüm ürünleri toplanmış olanlar, (3) tüm ürünleri henüz yığında olanlar, (4) bu ürün sonrası en az ürüne ihtiyacı
  kalanlar, (5) en az ürün içerenler, (6) eşitlikte **en eski sipariş**.
- **Koli seçimi (siparişin kolisi yoksa):** Dolu olmayan (koli başına en fazla sipariş — varsayılan 26 — altındaki) koliler arasından;
  siparişin toplanma oranı düşük-ihtimal eşiğinin (varsayılan %20) altındaysa **en büyük numaralı** koliye, değilse **en küçük
  numaralı** koliye. Uygun koli yoksa yeni koli açılır (★ YENİ KOLİ ★). Bir siparişin tüm ürünleri aynı koliye gider.
- Koli numaraları görev içinde sanaldır; kapanan kolinin numarası sonraki yeni kolide tekrar kullanılır ve `gen` artar.
- Koli içindeki sipariş ve masa ilişkisi sipariş kaydına da yazılır; her okutma operasyon geçmişine girer.
- Sipariş iptali ayrıştırmayı durdurmaz; iptal paketleme (masa) aşamasında yakalanır.

## Adım adım
**Ara ayrıştırma yapma**
1. Toplama Planlama → çok ürünlü görev → **Ara Ayrıştırma**.
2. Arabadaki ürünü okutun; ekranda ve seste gelen numaralı koliye atın. "★ YENİ KOLİ ★" gelirse boş koliyi o numarayla işaretleyin.
3. Kırmızı "İHTİYAÇ YOK" gelirse ürünü depo iadesi yığınına ayırın.
4. Zaman zaman **Koli Duvarı**'na bakıp yeşile dönen kolileri görün.

**Koliyi masaya alma**
1. Koli Duvarı'nda dolmuş/yeşil koli kartında **Zimmete Al → Zimmete Al**.
2. Aynı kartta beliren **Masa Aç**'a dokunun; Masa ekranı açılır (bkz. [Masa ve Paketleme](/rehber/siparis/masa-ve-paketleme/)).

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Kırmızı koliler düşük ihtimalli (ürünleri henüz toplanmamış) siparişleri barındırabilir; önce yeşil/sarı kolileri masaya alın.

> **Dikkat:** Zimmetteki koli başkası tarafından alınamaz; yanlış kişi aldıysa masada koli kapatılıp yeniden açılması gerekir.

> **Not:** "kolisiz sipariş" sayacı, görevde henüz hiç ürünü ayrıştırılmamış sipariş sayısıdır; toplama ilerledikçe azalır.

## İlgili sayfalar
- [Toplama Planlama](/rehber/siparis/toplama-planlama/)
- [Ürün Toplama](/rehber/siparis/urun-toplama/)
- [Masa ve Paketleme](/rehber/siparis/masa-ve-paketleme/)
