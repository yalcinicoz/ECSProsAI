---
title: Kanal Kapsamı
route: /storefront/channel-scope
group: Vitrin
order: 45
summary: Bir satış kanalında hangi ürünlerin söz konusu olacağının (tümü ya da kayıtlı filtre + manuel istisnalar) sorumlu kişilerce belirlendiği ekran; günlük kanala al/çıkar/durdur işlemleri Kanal Ürünleri'ndedir.
---

## Ne işe yarar
Kanalın **ürün kapsamını** tanımlar: bu kanalda hangi ürünler söz konusu? Kapsam dışı ürün o kanalda hiç
görünmez ve Kanal Ürünleri listesine de gelmez. Kapsam kararı günlük bir işlem değildir; katalog/satış
sorumlusu kanal açılırken ya da ticari kural değişince belirler. Kapsamdaki ürünlerin günlük yönetimi
(kanala al / çıkar / satışı durdur) [Kanal Ürünleri](/rehber/vitrin/kanal-urunleri/) sayfasındadır.

## Ekran yerleşimi
1. **Satış Kanalı seçici** — Kanal Ürünleri ile aynı; seçim oturumda ortak hatırlanır.
2. **Kapsam kartı** — doldurma tipi (Tümü / Filtre / Karma) + özet rozeti (kapsamdaki ürün sayısı, son hesaplama).
3. **Filtre düzenleyici** (Filtre/Karma'da) — kanal kategorilerindeki filtre kurucusunun aynısı + kanala özel iki kriter.
4. **Eylem çubuğu** — Eşleşen sayısını göster · Kapsamı Güncelle · Kaydet ve Güncelle.
5. **Manuel kapsam kararları** — ürün koduyla Kapsama ekle / Hariç tut; iki liste.

## Doldurma tipleri
| Tip | Anlamı |
|---|---|
| Tümü | Görselli tüm katalog ürünleri kapsamdadır (varsayılan; bugüne kadarki davranış). |
| Filtre | Yalnız kayıtlı filtreden geçen ürünler kapsamdadır. |
| Karma | Filtreden geçenler + manuel eklenenler; manuel hariç tutulanlar filtreden geçse de dışarıda kalır. |

## Filtre kuralları
Kanal kategorilerindeki kurallara ek olarak:
| Kural | Anlamı |
|---|---|
| Ürün Grupları | Yalnız seçilen gruplar. |
| **Hariç Tutulan Ürün Grupları** | "Tümü, şu gruplar hariç" kurgusu: dahil listesini boş bırakın, hariçleri buraya ekleyin (örn. tüm gruplar satılacak ama iç çamaşırı hariç). Dahil listesiyle birlikte de çalışır (önce dahil, sonra hariçler düşülür). |
| Kanal Fiyatı | Yalnız bu kanalda fiyatı (kanal fiyatı > 0) olan ürünler. |
| Kanal Stok Eşiği | Net stoğu eşiğin altındaki ürünler kapsam dışı; kanala verilen adet = net − eşik + 1. |

## Butonlar ve aksiyonlar
| Buton | Ne olur |
|---|---|
| Eşleşen sayısını göster | Kaydetmeden filtreyi çalıştırır: "N / toplam ürün eşleşiyor". |
| Kaydet ve Güncelle | Tanımı kaydeder ve kapsamı hemen yeniden hesaplar. Filtre tipinde en az bir kural girilmeden kaydedilemez ("Filtre tabanlı kapsam için en az bir kural gerekir."). |
| Kapsamı Güncelle | Kayıtlı filtreyi yeniden çalıştırır (ürünler/stok değiştiyse). Gece 03:00 civarı otomatik de çalışır. |
| Kapsama ekle / Hariç tut | Ürün kodu ile tek ürün istisnası. Hariç tutulan, yeniden hesaplamada geri eklenmez. |
| ✕ (listede) | Manuel kararı kaldırır; ürün bir sonraki hesaplamada filtreye göre değerlendirilir. |

## Durumlar ve iş kuralları
- Kapsam dışı ürün: sitede listelenmez/aranmaz/satın alınamaz, pazaryeri aday listesine girmez, Kanal Ürünleri listesinde görünmez.
- "Tümü"ne geri dönülünce kapsam kısıtı kalkar; kanaldan çıkarma/durdurma kararları korunur.
- Manuel kararlar (eklenen/hariç tutulan) yeniden hesaplamalarda korunur.
- Değişiklik sitede en geç ~1 dakika içinde etkili olur.

## İlgili sayfalar
- [Kanal Ürünleri](/rehber/vitrin/kanal-urunleri/) — günlük kanala al / çıkar / durdur
- [Satış Kanalları](/rehber/sistem/satis-kanallari/) — kanal tanımı ve yetenekleri
