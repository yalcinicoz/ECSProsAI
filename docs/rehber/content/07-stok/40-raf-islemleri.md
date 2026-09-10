---
title: Raf İşlemleri
route: /inventory/shelf
group: Stok Yönetimi
order: 40
summary: Tablet okutma ekranıyla göz (raf) içeriği görme, göze ürün yerleştirme, göz→göz taşıma, iadeden rafa alma, raf sayımı ve mağaza reyon taşıma; sayım raporları listesi.
---

## Ne işe yarar
Depodaki **göz** (raf birimi) işlemlerinin tek ekranı. Eski paneldeki "Rafa Ürün Yerleştir", "Raftan Rafa Transfer",
"İadeden Rafa", "Raf Sayım", "Raf Ürün Listesi" ve "Mağaza Depodan Reyona / Reyondan Çıkar" sayfalarının karşılığıdır.
Tek okutma kutusu vardır: okutulan barkod **göz barkodu** ise göz, **ürün barkodu** ise ürün olarak tanınır.
Liste `/inventory/shelf`, sayım raporları `/inventory/bin-counts`.

> **Dikkat — Aynalama kipi:** Eski sistemle stok senkronu açıkken (go-live öncesi) göz adetleri her 10 dakikada eski
> sistemden yeniden yazılır. Bu sürede ekranın üstünde sarı şerit görünür; **yalnız görüntüleme ve sayım raporu**
> çalışır, yazan işlemler (yerleştir, taşı, iadeden rafa, mağaza taşıma, sayım farkını uygula) sunucuda reddedilir
> ("Stok otoritesi eski sistemde"). Raf hareketleri o sürede eski panelden yapılır. Go-live'da tek ayarla açılır.

## Ekran yerleşimi
1. **Kip sekmeleri** — Raf İçeriği · Rafa Yerleştir · Raftan Rafa · İadeden Rafa · Raf Sayım · Mağaza Reyon; sağda "Sayım raporları →".
2. **Okutma kutusu** — büyük, odak hep burada (el terminali). Sağ üstte **Adet** (Raf İçeriği dışında).
3. **Bağlam kartları** — göz içeriği, hedef göz, ürün ve bulunduğu gözler, sayım tablosu.
4. **Bu oturum** — yapılan işlemlerin/hataların günlüğü (sayfa yenilenince silinir).

## Kipler
| Kip | Adımlar | Sunucu etkisi |
|---|---|---|
| Raf İçeriği | Göz okut → içindekiler (adet, rezerve). Ürün okut → hangi gözlerde. | Yok (salt okunur). |
| Rafa Yerleştir | Hedef gözü okut → ürünleri okut (her okutma **Adet** kadar). Not zorunlu (nereden geldiği). | Göze `adjustment(+)` hareketi. Mal kabul kolisi için **Tedarik › Sayım/Teslim** kullanılır. |
| Raftan Rafa | Kaynak gözü, sonra hedef gözü okut → ürün okut (**Adet** kadar) ya da **Tümünü … gözüne taşı**. | Aynı depo içi `transfer`. Adetli taşıma yalnız serbest (rezerve olmayan) adetten; **Tümünü taşı** rezervasyonları da hedefe taşır. Depolar arası için Mağaza Reyon ya da Stok Hareketleri. |
| İadeden Rafa | Ürünü okut → iade/defo (satışa kapalı) kısmındaki gözü bulunur → hedef gözü okut. | `return_to_shelf` hareketi. Kaynak göz satışa açık kısımdaysa reddedilir ("Raftan rafa kullanın"). |
| Raf Sayım | Gözü okut → sayım açılır (açık sayım varsa ona devam) → ürünleri okut → **Bitir** → fark tablosu → **Farkı Uygula**. | Bitir: sayım kaydı (rapor). Uygula: fark kadar `adjustment`; ayrı yetki `inventory.count.apply`. Beklenen adetler sayım bitene kadar gizlidir. |
| Mağaza Reyon | Kaynak ve hedef depoyu seç → ürünü okut. | Depolar arası anlık taşıma (`store_move`) + tamamlanmış transfer kaydı. Rezerve adet taşınmaz. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kip sekmesi | Üst şerit | Kipi değiştirir; bağlam ve oturum günlüğü sıfırlanır. | `inventory.view` |
| Tümünü … gözüne taşı | Kaynak göz kartı (Raftan Rafa) | Kaynak gözün tüm satırları rezervasyonlarıyla hedefe geçer. | Hedef göz okutulmuş; otorite panelde; `inventory.manage` |
| Bitir | Sayım kartı | Sayımı bitirir, fark tablosu açılır. | Sayım açık |
| İptal | Sayım kartı / rapor penceresi | Sayım iptal edilir (stok değişmez). | Sayım açık ya da bitmiş |
| Farkı Uygula | Sayım kartı / rapor penceresi | Fark kadar stok düzeltmesi yazılır, sayım "Uygulandı" olur. | Sayım bitmiş; `inventory.count.apply`; otorite panelde |
| Sayım raporları → | Üst şerit | Raf Sayımları listesine gider. | — |

## Raf Sayımları listesi (`/inventory/bin-counts`)
| Sütun | Anlamı |
|---|---|
| GÖZ | Göz kodu; altında depo. Filtre: göz kodu / göz barkodu. |
| DURUM | Sayılıyor · Bitti (uygulanmadı) · Uygulandı · İptal. |
| BEKLENEN / SAYILAN / FARK | Sistem adedi (açık sayımda gizli), okutulan adet, fark (yeşil 0 / kırmızı). |
| BAŞLANGIÇ | Sayımın açıldığı an; filtre: başlangıç / bitiş tarihi. |

Satıra tıklayınca sayım penceresi: satır bazlı beklenen/sayılan/fark, **Sayımı İptal Et**, **Farkı Uygula** (yetkili). Excel'e aktarma vardır.

## Durumlar ve iş kuralları
- Göz barkodları **Depolar › Depo Detay**'da tanımlanır, **Etiket Basımı**'ndan basılır. Pasif göz/kısım/depoya yazılmaz.
- Rezerve (siparişe ayrılmış) adet: adetli taşımada kaynakta kalır; "Tümünü taşı" ile satırla birlikte hedefe geçer; depo değiştiremez.
- Sayım farkı uygulanırken sayılan adet rezerve adedin altına düşüremez; o ürünler listelenir, hiçbir satır uygulanmaz.
- Boşalan satır (0 adet, 0 rezerve) silinir; hareketler Stok Hareketleri'nde `shelf` / `bin_count` referansıyla görünür.
- Sayım açıkken **Adet** kutusuna negatif değer yazıp okutarak yanlış okutma geri alınır.

## Adım adım
**Gelen ürünü rafa koyma (serbest)**
1. Rafa Yerleştir → not yazın (örn. "stüdyodan döndü") → gözü okutun → ürünleri okutun.

**Raf sayımı**
1. Raf Sayım → gözü okutun. 2. Gözdeki her ürünü okutun (aynı üründen birden fazla varsa her birini). 3. **Bitir**.
4. Depo sorumlusu fark tablosunu inceler → **Farkı Uygula** (ya da Raf Sayımları listesinden).

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** "Farkı Uygula" geri alınamaz; fark yanlışsa yeni sayım açın.

> **Not:** "'…' ne göz ne ürün barkodu" hatası: göz barkodu tanımsız ya da ürün barkodu katalogda yok.

## İlgili sayfalar
- [Depolar](/rehber/stok/depolar/)
- [Stok Takibi](/rehber/stok/stok-takibi/)
- [Stok Hareketleri](/rehber/stok/stok-hareketleri/)
