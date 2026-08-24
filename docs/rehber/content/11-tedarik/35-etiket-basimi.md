---
title: Etiket Basımı
route: /procurement/labels
group: Tedarik
order: 35
summary: Ayrıştırılan ürün yığınları için deste deste etiket basılan ekran — basım keyfîdir ve sayım üretmez; gerçek sayım depoya teslim okutmasıdır.
---

## Ne işe yarar
Fiziki ayrıştırma (koli açma, masalara sınıflama, kalite kontrol) **sistem dışıdır**. Yetkili personel bu ekranda,
beklenen ya da göz kararı adede göre her yığın için **etiket destesi** basar; desteler yığınların üzerine bırakılır,
normal personel yapıştırır. Eksik çıkarsa aynı satırdan ek basılır; fazlası çöpe atılır. **Basım sayım üretmez** —
gerçek sayım [Sayım / Teslim](/rehber/tedarik/sayim-teslim/) okutmasıdır. Kendi etiketi olan (markalı) ürünler
için basım gerekmez.

## Kullanım
1. **Etiket şablonu** seçin (varsayılan önseçilir — bkz. [Etiket Şablonları](/rehber/tedarik/etiket-sablonlari/)).
2. Barkod okutun ya da SKU/kod/ad arayın → **Deste adedi** girin → **Listeye Ekle** (aynı ürün tekrar eklenirse adet birikir).
3. Yığınlar alt listede toplanır; **Tümünü Yazdır** tek sekmede tüm desteleri art arda basar (her yığının
   destesi kendi ürünüyle); satırdaki **Bu desteyi bas** tek deste basar. Deste adedi listede düzeltilebilir.

## Kurallar
- Tek basımda en çok 50 farklı ürün / 2000 etiket.
- Katalogda olmayan ürün için etiket basılamaz; kart açılmadan işlem yapılmaz ("kart eksik" bildirimi Sayım/Teslim ekranından düşülür).
- Liste geçicidir (kaydedilmez) — basım bittiğinde temizlenebilir.
- Yetki: **Tedarik Yönetimi** (`procurement.manage`).
