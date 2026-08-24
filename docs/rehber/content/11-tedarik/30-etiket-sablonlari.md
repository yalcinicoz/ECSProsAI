---
title: Etiket Şablonları
route: /settings/label-templates
group: Tedarik
order: 30
summary: Ürün ve birim/raf etiketlerinin kullanıcı tarafından tasarlandığı ekran — kağıt ölçüsü, barkod/alan/metin/fiyat elemanları, sürükleyerek yerleşim, canlı önizleme ve test yazdırma; sabit format yoktur.
---

## Ne işe yarar
Etiketlerin **sabit bir formatı yoktur** — kağıt ölçüsünü ve içerikleri kendiniz tasarlarsınız. Ürün etiketi
(barkod, ad, renk/beden, fiyat…) ayrıştırma ekranından basılır; birim/raf etiketi depo birimlerine yapıştırılır.
Hedef tip başına bir **varsayılan** şablon seçilir; basım ekranları ilk onu kullanır.

## Ekran yerleşimi
1. **Şablon listesi** (sol) — ad, hedef (Ürün / Birim-Raf), ölçü, varsayılan/pasif işaretleri; **Yeni**.
2. **Başlık çubuğu** — şablon adı, hedef, En/Boy (mm, 10-500), Varsayılan, Aktif, **Kaydet**, **Sil**.
3. **Önizleme** — kağıt gerçek oranında, örnek ürün verisiyle; elemanlar **sürüklenerek** yerleştirilir,
   tıklanan eleman sağ panelde düzenlenir. Kağıt dışına taşan eleman varsa üstte ⚠ uyarı görünür.
4. **Eleman paneli** — Barkod / Alan / Metin / Fiyat ekleme; seçili elemanın veri alanı, X-Y-En-Boy (mm),
   yazı boyutu (pt), hiza, kalınlık ayarları; **Test yazdır** (ürün kodu ile ilk varyanttan 3 kopya).

## Elemanlar
| Tip | İçerik |
|---|---|
| Barkod | Ürün: varyant barkodu (13 haneli sayısal ise EAN-13, değilse CODE128 çizilir). Birim: birim barkodu. |
| Alan | Ürün: ürün adı, renk, beden, SKU, barkod değeri, ürün kodu. Birim: birim kodu, kısım, depo. |
| Metin | Serbest sabit metin (örn. mağaza adı). |
| Fiyat | Satış fiyatı `1.234,50 ₺` biçiminde (varyant fiyatı; yoksa ürün baz fiyatı). |

## Durumlar ve iş kuralları
- Varsayılan işaretlenince aynı hedef tipteki önceki varsayılan kendiliğinden kalkar.
- Kod, addan otomatik türetilir ve benzersizdir; ölçü 10-500 mm aralığındadır.
- Yazdırma sayfası ayrı sekmede açılır; üstteki gri çubuk basıma girmez, **Yazdır** düğmesi tarayıcı
  yazdırmasını açar (yazıcı ayarında kağıt ölçüsü şablonla aynı olmalıdır).
- Kaydedilmemiş değişiklikler test basımına yansımaz.
- Yetki: görüntüleme ve düzenleme **Tedarik Yönetimi** (`procurement.manage`).

## İlgili sayfalar
- [Satın Almalar](/rehber/tedarik/satin-almalar/) · [Mal Kabul](/rehber/tedarik/mal-kabul/)
