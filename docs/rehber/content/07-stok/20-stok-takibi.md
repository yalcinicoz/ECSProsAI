---
title: Stok Takibi
route: /inventory/stocks
group: Stok
order: 20
summary: Ürün/varyant bazında depo, kısım ve raf stoklarının (toplam, rezerve, mevcut) sorgulandığı ve barkodla stok giriş/çıkış/düzeltme hareketi kaydedildiği ekran.
---

## Ne işe yarar
Stok Takibi, bir ürünün hangi depo, kısım ve rafta kaç adet bulunduğunu, ne kadarının siparişlere rezerve olduğunu ve satılabilir (mevcut) miktarı gösterir. Depo ve müşteri hizmetleri ekipleri stok sorgular; depo sorumluları **+ Stok Hareketi** ile barkod okutarak giriş, çıkış veya düzeltme kaydeder. Sayfa boş açılır; listelemek için ürün aramak ya da depo seçmek gerekir.

## Ekran yerleşimi
![Stok Takibi — arama/filtre şeridi, ikincil filtre ve stok tablosu](img/inventory-stocks.webp)
1. **Başlık satırı** — "Stok"; altında "Listelemek için ürün arayın veya depo seçin" ya da kayıt sayısı. Sağda arama kutusu + büyüteç, **Tümü / Mevcut** anahtarı, depo listesi, **+ Stok Hareketi**.
2. **İkincil filtre kartı** — yalnız arama yapıldığında görünür: Varyant, Kısım, Raf listeleri.
3. **Tablo** — stok satırları; satırlar tıklanmaz.
4. **Sayfalama** — sayfa başına 30 satır.
5. **Stok Hareketi penceresi**.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| ÜRÜN | Küçük görsel, ürün adı, altında ürün kodu · seçenekler (örn. "Beden: M, Renk: Beyaz"). |
| DEPO | Stok satırının deposu. |
| KISIM | Kısım adı (yoksa —). |
| RAF | Raf kodu (yoksa —). |
| STOK | Fiziksel miktar. |
| REZ. | Onaylı siparişlere rezerve miktar (0'dan büyükse vurgulu). |
| MEVCUT | STOK − REZ.; 0 ve altı kırmızı, 5 ve altı sarı. |

| Filtre | Ne yapar |
|---|---|
| Arama kutusu | Ürün kodu / adı / barkod; Enter ya da büyüteç ile uygulanır. Uygulanınca ikincil filtreler sıfırlanır. |
| Tümü / Mevcut | Mevcut seçiliyken yalnız mevcut miktarı sıfırdan büyük satırlar. |
| Depo listesi | "Tüm Depolar" ya da tek depo; değişince kısım/raf seçimleri sıfırlanır. |
| Varyant (ikincil) | Arama sonucundaki varyantlar, yanında kayıt sayısıyla. |
| Kısım (ikincil) | Depo seçiliyse yalnız o deponun kısımları. |
| Raf (ikincil) | Kısım seçiliyse yalnız o kısmın rafları. |
| İkincil filtreyi temizle | Varyant/Kısım/Raf seçimlerini kaldırır. |

Arama ya da depo seçilmeden tabloda "Ürün kodu/adı/barkod arayın veya bir depo seçin — sonuçlar burada listelenir." yazar; sonuç yoksa "Stok kaydı bulunamadı."

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Büyüteç / Enter | Arama kutusu | Aramayı uygular, 1. sayfaya döner. | — |
| + Stok Hareketi | Başlık sağı | "Stok Hareketi" penceresi açılır; imleç barkod alanına gelir. | `inventory.manage` |
| Büyüteç / Enter | Pencere → Ürün Barkodu | Barkodu arar; bulunursa ürün kartı (ad, SKU, özellikler) görünür. | — |
| Kaydet | Pencere | Hareket kaydedilir, liste yenilenir, pencere kapanır. | Barkod bulunmuş + Depo seçili + Miktar ≠ 0 |
| İptal | Pencere | Formu temizleyip kapatır. | — |

## Form alanları
### Stok Hareketi penceresi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Ürün Barkodu | Evet | Okuyucuyla okutun ya da yazın; Enter/büyüteç ile aranır. Bulunamazsa "Barkod bulunamadı." Bulununca ürün adı, SKU ve özellik özeti gösterilir. |
| Depo | Evet | Hareketin yapılacağı depo. |
| Hareket Tipi | Hayır (varsayılan Düzeltme) | Satın Alma / Satış / Düzeltme / İade / Transfer Giriş / Transfer Çıkış. Kayıt amaçlı etikettir. |
| Miktar | Evet | Tam sayı; **çıkış için eksi** (örn. `-3`), giriş için artı. 0 olamaz. |
| Not | Hayır | Hareket açıklaması. |

## Durumlar ve iş kuralları
- **MEVCUT = STOK − REZ.** Rezervasyon sipariş **onaylanınca** oluşur, sipariş **kargoya verilince** stoktan gerçekten düşer ve rezervasyon kapanır, sipariş **iptal edilince** serbest kalır. İade teslim alındığında stok artar.
- Çıkış hareketi depodaki toplam stoğu aşamaz: "Stok miktarı negatife düşemez."
- **Raf seçimi otomatiktir:** çıkışta depo içinde satışa açık kısımlar önce, toplama sırasına göre düşülür; girişte varyantın o depodaki mevcut rafına, yoksa deponun uygun varsayılan rafına yazılır. Bu pencerede belirli raf seçilemez.
- Her kayıt bir stok hareketi oluşturur; hareketler silinmez — yanlış giriş ters işaretli yeni hareketle düzeltilir.
- Sitede "stokta" görünen miktar yalnız **İnternete satışa açık** kısımlardaki mevcut stoktur (bkz. Depolar). Satışa kapalı kısımdaki (İade/Defo) stok bu listede görünür ama sitede sayılmaz.
- Yazma işlemi `inventory.manage` yetkisi ister; yetkisiz kullanıcı yalnız sorgular.

## Adım adım
**Bir ürünün stoğunu sorgulama**
1. Stok > Stok Takibi'nde arama kutusuna ürün kodu, adı ya da barkodu yazıp Enter'a basın.
2. Gerekirse üstte görünen **Varyant / Kısım / Raf** listeleriyle daraltın; yalnız satılabilir satırlar için **Mevcut**'u seçin.

**Sayım farkını düzeltme (barkodla)**
1. **+ Stok Hareketi**'ne tıklayın, ürün barkodunu okutun; ürün kartının geldiğini doğrulayın.
2. **Depo**'yu seçin, **Hareket Tipi** Düzeltme kalsın.
3. **Miktar**'a farkı yazın (fazla için `+2`, eksik için `-2`), **Not**'a "Sayım" yazın.
4. **Kaydet**'e tıklayın; satır yenilenir.

**Tedarikçiden mal girişi**
1. **+ Stok Hareketi** → barkod okutun → Depo seçin → Hareket Tipi **Satın Alma** → Miktar (artı) → Kaydet.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Sayfa kasıtlı olarak boş açılır; yüz binlerce stok satırını tek seferde listelemek yerine önce ürün arayın ya da depo seçin.

> **Dikkat:** Kaydet butonu pasifse barkod henüz bulunmamış, depo seçilmemiş ya da miktar 0'dır.

> **Dikkat:** "Stok miktarı negatife düşemez." — depodaki toplam stoktan fazla çıkış istediniz; STOK sütununu kontrol edin.

> **Not:** MEVCUT kırmızı (≤0) ama STOK pozitifse miktar rezerve edilmiştir; ilgili siparişler kargoya verilince STOK da düşer.

## İlgili sayfalar
- [Depolar](/rehber/stok/depolar/)
- [Stok Hareketleri](/rehber/stok/stok-hareketleri/)
