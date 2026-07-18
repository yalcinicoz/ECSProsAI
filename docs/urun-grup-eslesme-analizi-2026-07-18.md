# Ürün–Grup Eşleşme Analizi (Panel testi bulgusu B-09)

**Tarih:** 2026-07-18 · **Durum:** ✅ DÜZELTİLDİ (aynı gün, kullanıcı onayıyla Seçenek 1)
**Uygulanan:** MigrationTool **Faz 21** (`dotnet run 21`) — `grup_eslesme.md` artık koddan okunan tam
harita; **10.482 ürünün grubu düzeltildi** (Pantolon 13.572→3.090), ANALYZE yapıldı. Faz 5'teki
sessiz fallback kaldırıldı: eşlenemeyen grup kalırsa aktarım raporlayıp DURUR (yedek:
`~/yedekler/urun-grup-atamalari-oncesi-2026-07-18.csv`). Canlı doğrulama: stiletto → Topuklu
Ayakkabı; site breadcrumb'ları düzeldi (kaban: "Kadın > Pantolon" yerine "Kadın").
**Tetikleyici:** Panel testinde "Kadın Kaşe Kaban / Deri Trençkot" gibi ürünlerin grup kolonunda
"Pantolon" görünmesi (bulgu B-09).

## Sonuç (özet)

Sorun tekil birkaç üründe değil — **kataloğun ~%37'si yanlış grupta**:

| Metrik | Değer |
|---|---|
| Pantolon (grp_3) grubundaki toplam ürün | **13.572** |
| Eski sistemde de gerçekten pantolon olan | 3.090 |
| **Yanlışlıkla Pantolon'a düşen** | **10.482** (110 farklı eski gruptan) |
| Katalog toplamı | 28.651 |

Örneklem doğrulaması: Pantolon'daki kaban/trençkot adlı 30 üründen 30'u eski MySQL'de
**MK — "Kadın Mont"** grubunda (kaynak veri doğru). Yanlış Pantolon'a düşen en kalabalık
eski gruplar: Erkek T-Shirt (854), Kadın Takımlar (851), Erkek Pantolon dışı ayakkabı grupları
(Spor 527+524, Topuklu 382, Günlük 178+178, Peluş Terlik 243), Kadın Ceket (437),
Kadın Mont (408), Erkek Eşofman (387), Kadın Eşofman (372), Erkek İç Giyim (355),
Erkek Sweatshirt (341), Erkek Gömlek (309), Erkek Çocuk T-Shirt (302), Erkek Triko (261)…

## Kök Neden

`tools/MigrationTool/Program.cs` ürün aktarımında (Faz 5):

```csharp
var groupId = productGroupMap.TryGetValue(grupId, out var g) ? g : defaultGroupId;
```

Eski grubun karşılığı `productGroupMap`'te bulunamayan HER ürün sessizce **defaultGroupId'ye
(= Pantolon/grp_3)** yazılıyor. 110 eski grup haritada yok → 10.482 ürün varsayılana düştü.
`docs/grup_eslesme.md` dokümanı doğru karşılıkları zaten tanımlıyor (ör. satır 79:
`MK | Kadın Mont | Mont (grp_73)`), fakat `EnsureProductGroupMap()` bu eşlemenin tamamını
kuramıyor (muhtemel nedenler: mükerrer eski grup kodları — iki ayrı "ET"/"TT"/"EP" var —
ad bazlı eşlemede çakışıp atlanıyor; 2026-07-01'de silinen 7 grup; sonradan eklenen gruplar).

## Etkileri

- Grup şeması (beden seti, özellik listesi, varyant eksenleri) grup üzerinden geldiği için
  yanlış gruptaki ürünlerin **özellik/beden şablonları da yanlış**.
- Panelde grup filtresi ve sitede grup-temelli her davranış (facet dolumu, kategori-grup
  sorumlulukları) bu 10.5K ürün için hatalı.

## Düzeltme Seçenekleri

1. **MigrationTool'a kalıcı "grup düzeltme" fazı (ÖNERİLEN):** `grup_eslesme.md`'yi tek doğruluk
   kaynağı yapıp (veya DB'de eşleme tablosuna alıp) eski `urunGrupId` → yeni grup eşlemesini
   TAM LİSTE olarak kur; eşlenemeyen grup kalırsa ürünleri varsayılana atmak yerine **raporla ve
   durdur**. Faz, mevcut ürünlerin `ProductGroupId`'sini yerinde günceller (tekrar çalıştırılabilir —
   dev aşaması kuralı: veri geçici, go-live aktarımında da aynı faz kullanılır). Ardından
   `ANALYZE catalog.products` + facet/site cache temizliği.
2. Tek seferlik SQL düzeltmesi: bu analizde çıkarılan `urunKodu → eski grup` tablosuyla doğrudan
   UPDATE. Hızlı ama tekrarlanabilir değil (go-live aktarımında aynı hata geri gelir) — önerilmez.
3. Hiçbir şey yapma: go-live'da zaten baştan aktarılacak — ANCAK aynı MigrationTool koşacağı için
   hata aynen tekrarlanır; en azından Seçenek 1'deki harita düzeltmesi şart.

## Not

- Analiz verisi: eski MySQL (`juludedb.apurunler` ⋈ `dfurungruplari`, yalnız `yeniurunkodlari`)
  28.651 satır dışa aktarılıp `catalog.products` ile karşılaştırıldı (2026-07-18).
- Grup düzeltmesi yapılınca ürünlerin mevcut özellik satırları (yanlış şemayla girilmişse)
  ayrıca gözden geçirilmeli — ilk aşamada yalnız `ProductGroupId` düzeltmesi yeterli, özellikler
  zaten büyük oranda değer havuzundan geliyor.
