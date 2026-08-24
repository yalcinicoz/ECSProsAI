---
title: Ayrıştırma
route: /procurement/sorting
group: Tedarik
order: 40
summary: Teslim alınan ürünlerin barkod okutularak sayılıp etiketlendiği operasyon ekranı — sayım stok girişinin tek kaynağıdır; katalogda olmayan ürün için kart açılmaz, "kart eksik" bildirimi düşülür.
---

## Ne işe yarar
Tedarik sürecinin **kalbi**: gelen koliler açılır, ürünler model/renk/beden olarak ayrıştırılır, sayılır ve
etiketlenir. **Sayım = gerçek** — stok girişinin tek kaynağı buradaki kayıtlardır (raf ataması ve stok artışı
Yerleştirme adımında yapılır). Satın alma/teslim belgeleriyle eşleşme aranmaz; fazla/eksik dönem raporunda görünür.

## Ekran yerleşimi
1. **Parti seçici** — ayrıştırılabilir partiler (Teslim Alındı / Ayrıştırılıyor) ya da **Partisiz ayrıştırma**;
   seçim oturumda hatırlanır. Mal Kabul detayındaki "Ayrıştırma Ekranı →" butonu partiyi seçili getirir.
2. **Etiket şablonu** — ürün şablonları; varsayılan önseçilir.
3. **Arama kutusu** (barkod okuyucu dostu, otomatik odak) — barkod okutun ya da SKU/kod/ad yazıp Enter.
4. **Kayıt formu** — bulunan varyant kartı + Adet, Maliyet (ops.), Etiket adedi (boş = adet).
5. **Açık "kart eksik" bildirimleri** ve **sayım listesi** (parti bazlı).

## Akış
1. Barkodu okutun → **tam eşleşme tek adaysa doğrudan seçilir**, imleç Adet'e geçer.
2. Adet yazıp Enter (ya da **Kaydet + Etiket Bas**) → kayıt açılır, etiket sekmesi açılır, imleç aramaya döner.
   `Yalnız Kaydet` etiket basmaz (sonradan listeden **Bas** ile basılır, sayaç tutulur).
3. İlk sayımda `Teslim Alındı` parti kendiliğinden `Ayrıştırılıyor` olur.
4. Aynı varyanta birden çok kayıt açılabilir; sayım toplamı geçerlidir.

## "Kart eksik" (K9)
Ürün katalogda yoksa **kayıt açılamaz ve kart AÇILMAZ** — sarı uyarıdaki **Kart Eksik Bildir** düğmesi aranan
metni bildirime yazar. Katalog sorumlusu kartı açınca bildirim "Kart açıldı — çöz" ile kapatılır ve sayım yapılır.

## Durumlar ve iş kuralları
- Adet 0 olamaz; maliyet negatif olamaz; tamamlanmış partiye sayım eklenemez (önce Geri Aç).
- **Yerleşti** durumundaki kayıt düzenlenemez/silinemez (stok girmiştir); **Bekliyor** kayıtlar serbesttir.
- Partisiz sayımlar "Partisiz ayrıştırma" görünümünde listelenir; sonradan izlenebilirler.
- Yetki: **Ayrıştırma / Yerleştirme** (`procurement.sort`) — menüde bu yetkiyle görünür.

## İpuçları
- Barkod okuyucuyla klavyesiz akış: okut → adet → Enter. Etiket adedi boşsa adet kadar basılır.
- Etiket çıkışında yazıcı kağıt ölçüsü şablonla aynı olmalıdır (bkz. [Etiket Şablonları](/rehber/tedarik/etiket-sablonlari/)).

## İlgili sayfalar
- [Mal Kabul](/rehber/tedarik/mal-kabul/) · [Etiket Şablonları](/rehber/tedarik/etiket-sablonlari/)
