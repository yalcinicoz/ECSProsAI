---
title: Sayım / Depoya Teslim
route: /procurement/sorting
group: Tedarik
order: 40
summary: Etiketlemesi biten ürünlerin depoya teslim için okutularak sayıldığı ve rafa yerleştirilerek stoğa alındığı ekran — okutma modunda her okutma +1; Yerleştirme sekmesinde raf barkodu okutulup seçilen sayımlar stoğa girer.
---

## Ne işe yarar
Tedarik sürecinin **gerçek sayım** noktası: fiziki ayrıştırma ve etiketleme bittikten sonra ürünler kabul alanından
depoya **okutularak** teslim edilir. Sayım varyant başına tek kayıtta birikir; stok girişi Yerleştirme adımında bu
kayıtlardan yapılır. Satın alma/teslim belgeleriyle eşleşme aranmaz; fark dönem raporunda görünür.

## İki sayım modu
| Mod | Nasıl çalışır | Ne zaman |
|---|---|---|
| **Okutma (+1)** | Her barkod okutması o varyantın sayacını 1 artırır; imleç aramada kalır — okut-okut-okut. | Ürünler tek tek okutularak teslim edilirken (varsayılan). |
| **Adet girişi** | Barkod bir kez okutulur (ya da aranıp seçilir), adet yazılıp Enter. Maliyet (ops.) girilebilir. | Yüksek adetli yığınlar; **markalı / kendi etiketli ürünler** — üretici barkodu katalogdaki varyant barkoduyla eşleşiyorsa bizim etiketimiz hiç basılmadan sayılır. |

## Ekran
1. **Parti** seçici (Teslim Alındı/Ayrıştırılıyor partiler ya da Partisiz sayım; Mal Kabul detayındaki
   "Sayım / Teslim →" butonu partiyi seçili getirir) + **mod** anahtarı; "Son: …" ibaresi son işlemi gösterir.
2. **Arama kutusu** (otomatik odak, barkod okuyucu dostu). Tam eşleşen tek aday okutma modunda **anında +1 sayar**.
3. **Açık "kart eksik" bildirimleri** — katalogda olmayan ürün için kart AÇILMAZ; **Kart Eksik Bildir** kuyruğa düşer,
   katalog sorumlusu kartı açınca "çöz" ile kapatılır ve sayım yapılır (K9).
4. **Sayım listesi** — ürün başına biriken adet (yanlış okutma satırdaki adet kutusundan düzeltilir ya da satır silinir),
   maliyet, yerleştirme durumu (`Bekliyor` / `Yerleşti` — Yerleşti kayıtlar kilitlidir, stok girmiştir).

## Yerleştirme sekmesi (stok girişi)
Sayımı biten ürünler rafa konurken **Yerleştirme** sekmesi kullanılır:
1. **Birim (raf) barkodunu okutun** (ya da kodunu yazın) — kısım/depo adıyla doğrulanır; satışa kapalı kısım
   seçilirse uyarı görünür (ürün stoğa girer ama sitede satılmaz).
2. Bekleyen sayımlardan yerleştirilecekleri **işaretleyin**; kısmi yerleştirmede seçili satıra adet yazın
   (boş = tamamı; kalan bekleyen kayıtta kalır).
3. **Seçilenleri Yerleştir** → stok seçilen birime girer (hareket: `Satın Alma`, belge: sayım kaydı, birim
   düzeyinde izlenebilir). Satışa açık kısımdaysa ürün kısa süre içinde sitede stoklu görünür.

Kurallar: partili sayım yalnız **partinin teslim alındığı depodaki** birime yerleştirilebilir; partisiz sayım
her birime konabilir. Yerleşen kayıt düzenlenemez/silinemez.

## Kurallar
- İlk sayımda `Teslim Alındı` parti kendiliğinden `Ayrıştırılıyor` olur; tamamlanmış partiye sayım eklenemez (önce Geri Aç).
- Adet 0 olamaz; aynı varyantın okutmaları/girişleri **tek kayıtta birikir**.
- Etiket basımı bu ekrandan bağımsızdır (bkz. [Etiket Basımı](/rehber/tedarik/etiket-basimi/)).
- Yetki: **Ayrıştırma / Yerleştirme** (`procurement.sort`).

## İlgili sayfalar
- [Mal Kabul](/rehber/tedarik/mal-kabul/) · [Etiket Basımı](/rehber/tedarik/etiket-basimi/) · [Etiket Şablonları](/rehber/tedarik/etiket-sablonlari/)
