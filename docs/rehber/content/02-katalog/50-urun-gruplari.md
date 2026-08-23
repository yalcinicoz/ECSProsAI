---
title: Ürün Grupları
route: /catalog/product-groups
group: Katalog
order: 50
summary: Ürün tiplerinin (Pantolon, Elbise, Ayakkabı…) ve her tipte hangi özelliklerin ürün özelliği ya da varyant ekseni olarak kullanılacağının tanımlandığı şablon ekranı.
---

## Ne işe yarar
Ürün grubu, bir ürünün "ne olduğunu" söyleyen şablondur: Pantolon, Gömlek, Topuklu Ayakkabı gibi. Her grup, özellik
havuzundan seçilmiş bir özellik listesi taşır ve her özellik için şu kararlar verilir: bu özellik varyant ekseni mi
(renk, beden gibi varyantları birbirinden ayırır), ana eksen mi (listeleme ve görsel gruplaması için temel eksen),
zorunlu mu ve hangi sırada gösterilecek. Ürün kartı oluşturulurken grup seçilir; ürün formundaki özellik alanları ve
varyant eksenleri bu şablondan gelir.

Bu ekranı katalog yapısını kuran yöneticiler kullanır: yeni bir ürün tipi açarken, bir gruba özellik eklerken/çıkarırken,
ana ekseni belirlerken ya da varyant eksenine bağlı ölçü bilgileri (alt özellikler) tanımlarken.

> **Dikkat:** Ürün grupları **platform seviyesinde** tanımdır. Ekleme/düzenleme için `catalog.platform.manage` yetkisi
> gerekir. Yetki yoksa sayfa başlığının yanında **Salt Okunur** rozeti görünür; ekran görüntülenir ama butonlar gösterilmez.

> **Not:** Grup ile kategori farklıdır: grup ürünün ne olduğunu (Pantolon), kategori ürünün mağazada nerede listeleneceğini
> (Kadın › Pantolon, Sezon Sonu) tanımlar. Kategoriler menü/kanal tarafında yönetilir.

## Ekran yerleşimi
![Ürün Grupları listesi — Tümü/Aktif anahtarı, Yeni Grup butonu ve grup tablosu](img/catalog-product-groups.webp)
1. **Başlık ve kayıt sayısı** — "Ürün Grupları", yanında (yetki yoksa) Salt Okunur rozeti, altında toplam kayıt sayısı.
2. **Sağ üst araç çubuğu** — `Tümü` / `Aktif` anahtarı ve **Yeni Grup** butonu.
3. **Tablo** — gruplar Sıra'ya, eşitlikte ada göre sıralı; satıra tıklayınca detay açılır.

![Ürün Grubu detayı — özet kartları, Ad (Çeviriler), Özellikler tablosu ve Varyant Ekseni Alt Özellikleri](img/catalog-product-groups-detay.webp)
1. **Kırıntı ve başlık** — `Ürün Grupları › <ad>`; altında kod rozeti ve Aktif/Pasif rozeti; sağda (ürün yoksa) **Grubu Sil**.
2. **Özet kartları** — Toplam Özellik, Varyant Ekseni (sayı), Sıra, Durum.
3. **Ad (Çeviriler) kartı** — çok dilli grup adı, sağ üstte **Kaydet**.
4. **Özellikler kartı** — gruba atanmış özellikler tablosu, **Özellik Ekle** butonu; ürün varsa "Ürün mevcut — ana eksen kilitli" rozeti.
5. **Varyant Ekseni Alt Özellikleri kartı** — yalnız en az bir varyant ekseni varsa görünür; eksen başına alt özellik etiketleri.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| Ad | Grubun Türkçe adı (klasör simgesiyle). |
| Kod | Sistem kodu (örn. `pantolon`); Türkçe addan otomatik üretilir, değişmez. |
| Özellik | Gruba atanmış toplam özellik sayısı. |
| Varyant | Varyant ekseni sayısı — `N eksen` rozeti; eksen yoksa `—`. |
| Sıra | Grubun sıra numarası. |
| Durum | `Aktif` / `Pasif`. |
| › | Satırın detaya açıldığını gösteren ok. |

| Filtre | Ne yapar |
|---|---|
| `Tümü` / `Aktif` anahtarı | `Aktif` seçiliyken yalnız aktif gruplar listelenir. Varsayılan `Tümü`. |

- Arama kutusu ve sayfalama yoktur; tüm gruplar tek sayfada, **Sıra** (küçükten büyüğe) ve sonra ada göre sıralıdır.
- **Satıra tıklayınca** grubun detay sayfası açılır (`/catalog/product-groups/<id>`).
- Kayıt yoksa "Ürün grubu bulunamadı" yazısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Yeni Grup | Liste, sağ üst | "Yeni Ürün Grubu" penceresi açılır; Kaydet sonrası yeni grubun detayına gidilir. | `catalog.platform.manage` |
| Grubu Sil | Detay, başlığın sağı | ⚠️ "Ürün Grubunu Sil" onayı: *"… grubu kalıcı olarak silinecek. Bu işlem geri alınamaz."* Onaylanınca grup silinir ve listeye dönülür. | `catalog.platform.manage`; **gruba bağlı ürün olmamalı** (ürün varsa buton hiç görünmez) |
| Kaydet (Ad) | Detay, Ad (Çeviriler) kartı | Ad çevirileri kaydedilir; "Kaydedildi" yazısı görünür. Değişiklik yoksa pasiftir. | `catalog.platform.manage` |
| Özellik Ekle | Detay, Özellikler kartı | "Özellik Ekle" penceresi açılır. Havuzdaki tüm özellikler zaten ekliyse buton pasiftir. | `catalog.platform.manage` |
| Yıldız (Ana eksen yap) | Özellikler tablosu, Ana Eksen sütunu | Boş yıldıza tıklayınca o eksen **ana eksen** olur (dolu sarı yıldız); önceki ana eksenin işareti kalkar. | `catalog.platform.manage`; özellik varyant ekseni olmalı; **grupta ürün yoksa** (ürün varsa kilit simgesi görünür, tıklanamaz) |
| Düzenle (kalem) | Özellik satırı, sağ | "Özellik Düzenle — <ad>" penceresi açılır (varyant ekseni, zorunlu, sıra). | `catalog.platform.manage` |
| Sil (çöp kutusu) | Özellik satırı, sağ | ⚠️ "Özelliği Sil" onayı: *"… özelliği bu gruptan kaldırılacak. Emin misiniz?"* Onaylanınca özellik gruptan çıkarılır (özellik tipi havuzda kalır). | `catalog.platform.manage` |
| Alt Özellik Ekle | Alt Özellikler kartı, sağ üst | "Eksen Alt Özelliği Ekle" penceresi eksen seçimi boş açılır. | `catalog.platform.manage`; en az bir varyant ekseni |
| + Alt Özellik | Her eksen başlığının sağı | Aynı pencere, eksen önceden seçili açılır. | `catalog.platform.manage` |
| Düzenle (küçük kalem) | Alt özellik etiketi | "Alt Özellik Düzenle — <ad>" (zorunlu, sıra). | `catalog.platform.manage` |
| × (kaldır) | Alt özellik etiketi | ⚠️ "Alt Özelliği Sil" onayı; onaylanınca alt özellik eksenden kaldırılır. | `catalog.platform.manage` |
| Geri Dön | Detay (kayıt bulunamadığında) | Listeye döner. | — |

## Form alanları

### Yeni Ürün Grubu penceresi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Sıra | Hayır | Tam sayı; listedeki sıra. Varsayılan 0. |
| Ad | Evet (kaynak dil) | Çok dilli (TR/EN…). Türkçe ad koddan üretim için de kullanılır. |
| Otomatik Kod | — (salt okunur) | Türkçe addan canlı üretilir (örn. "Topuklu Ayakkabı" → `topuklu_ayakkabi`). Kayıt sonrası değiştirilemez. |

Hata olursa pencerede "Hata oluştu. Lütfen tekrar deneyin." görünür (örn. addan kod üretilemediğinde).

### Özellik Ekle / Özellik Düzenle pencereleri
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Özellik Tipi | Evet | Yalnız eklemede. Aranabilir liste; bu gruba zaten ekli tipler listede çıkmaz. Ekle butonu seçim yapılana kadar pasiftir. |
| Varyant Ekseni | Hayır | İşaretliyse bu özellik bu grupta varyantları ayıran eksen olur (örn. Renk, Beden). İşaretsizse tüm varyantlara ortak ürün özelliğidir (örn. Kumaş). |
| Zorunlu | Hayır | İşaretliyse ürün kartında bu özellik boş bırakılamaz. |
| Sıra | Hayır | Tam sayı; ürün formundaki ve tablodaki sıra. Eklemede (mevcut özellik sayısı × 10) önerilir. |

Aynı özellik ikinci kez eklenmeye çalışılırsa sunucu "Bu özellik zaten bu gruba ekli." hatası döner.

### Eksen Alt Özelliği Ekle / Alt Özellik Düzenle pencereleri
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Varyant Ekseni | Evet | Yalnız eklemede; grubun varyant eksenlerinden biri (örn. Beden). Seçilmeden alt özellik listesi "— Önce eksen seçin —" gösterir. |
| Alt Özellik Tipi | Evet | Özellik havuzundan, eksenin kendisi ve bu eksene zaten ekli olanlar hariç (örn. Paça Boyu, Bel Genişliği). Seçenek kalmadıysa "Bu eksen için eklenebilecek özellik kalmadı." yazısı görünür. |
| Zorunlu | Hayır | İşaretliyse etiket üzerinde turuncu `*` görünür; ürün kartında her eksen değeri için doldurulması beklenir. |
| Sıra | Hayır | Tam sayı; etiket üzerinde `#N` olarak görünür. |

Sunucu hataları pencerede gösterilir: "Bu özellik bu grupta varyant ekseni olarak tanımlı değil.", "Bu alt özellik zaten eklenmiş.", "Bir özellik kendi ekseninin alt özelliği olamaz."

## Detay sayfası — Özellikler tablosu
| Sütun | Anlamı |
|---|---|
| Özellik Tipi | Ad + kod rozeti. Katman simgesi = varyant ekseni, etiket simgesi = ürün özelliği. |
| Varyant Ekseni | `Evet` rozeti ya da `—`. |
| Ana Eksen | Yalnız varyant eksenlerinde: dolu sarı yıldız = ana eksen; boş yıldız = tıklayıp ana eksen yapılabilir; kilit = grupta ürün olduğu için değiştirilemez. |
| Zorunlu | `Zorunlu` (turuncu) rozeti ya da `—`. |
| Sıra | Sıra numarası; tablo bu sıraya göre dizilir. |
| (işlemler) | Düzenle / Sil — yalnız yetkili kullanıcıda. |

Özellik yoksa "Henüz özellik eklenmemiş" yazısı görünür.

### Varyant Ekseni Alt Özellikleri
Her varyant ekseni (örn. *Beden ekseni*) bir bölüm olarak listelenir; altında o eksene bağlı alt özellikler etiket olarak
dizilir: `ad` `*`(zorunluysa) `#sıra` + kalem + ×. Açıklama satırı: *"Her varyant değerinin kendine özgü ölçülebilir
özellikleri (örn. Beden 38 → Paça Boyu: 74 cm)"*. Alt özelliği olmayan eksende "Bu eksen için henüz alt özellik
tanımlanmamış" yazar. Ürün kartında her beden değeri için bu alt özelliklerin değerleri ayrı ayrı girilir (ölçü tablosu).

## Durumlar ve iş kuralları
- **Aktif / Pasif:** Pasif grup `Aktif` filtresinde gizlenir. Bu ekranda grubun Sıra ve Durum değerleri özet kartında gösterilir; detaydan düzenlenen tek alan **Ad (Çeviriler)**'dir.
- **Kod değişmez:** Türkçe addan bir kez üretilir; ad sonradan değişse de kod aynı kalır.
- **Varyant ekseni:** Gruptaki varyant eksenleri ürünün varyant matrisini belirler. Eksen yoksa ürün tek varyantlı (varsayılan varyant) oluşturulur; bir eksen tekil liste, birden fazla eksen kombinasyon (örn. Renk × Beden) üretir.
- **Ana eksen:** Varyant eksenlerinden **en fazla biri** ana eksendir; mağaza listelemesi ve ürün görsellerinin gruplanması (örn. renk başına görsel) bu eksene göre yapılır. Ana eksen yalnız varyant ekseni olan özellikte seçilebilir; **gruba bağlı ürün varsa değiştirilemez** ("Ürün mevcut — ana eksen kilitli" rozeti, kilit simgesi; sunucu hatası: "Bu gruba ait ürünler mevcut olduğundan ana eksen değiştirilemez.").
- **Grup silme:** Yalnız hiç ürünü olmayan grup silinebilir; ürün varsa buton görünmez ve sunucu "Bu gruba atanmış ürünler bulunduğu için silinemez." döner. Silme geri alınamaz ⚠️.
- **Özelliği gruptan çıkarma:** Özellik yalnız bu grubun şablonundan kaldırılır; özellik tipi ve değer havuzu silinmez. Mevcut ürünlerde girilmiş değerler veri olarak kalır ancak formda bu grubun şablonunda görünmez.
- **Doğru grup önemlidir:** Ürünün özellik alanları, zorunlu alanları, varyant eksenleri ve beden/ölçü şablonu gruptan gelir. Yanlış gruba atanmış ürünün özellik/beden şablonu da yanlış olur; mağazadaki grup temelli davranışlar (filtre dolumu, ölçü tablosu) bundan etkilenir.
- **Özellik tipi ile grup ayarı ayrımı:** Veri tipi (seçim listesi, metin…) ve filtrede kullanım özellik tipinde; varyant ekseni / ana eksen / zorunlu / sıra ise grup–özellik bağında tutulur. Aynı özellik farklı gruplarda farklı rol alabilir.

## Adım adım

### Yeni ürün grubu oluşturma ve şablonunu kurma
1. **Katalog › Ürün Grupları**'na girin, **Yeni Grup**'a tıklayın.
2. **Ad**'ı (Türkçe ve varsa diğer diller) yazın, gerekirse **Sıra** verin; **Otomatik Kod**'u kontrol edip **Kaydet**'e tıklayın. Detay sayfası açılır.
3. **Özellik Ekle** ile ürün özelliklerini ekleyin (örn. Kumaş Türü, Desen) — *Varyant Ekseni* işaretsiz, gerekiyorsa *Zorunlu*.
4. Varyant oluşturacak özellikleri (örn. Renk, Beden) **Varyant Ekseni** işaretli olarak ekleyin.
5. Ana Eksen sütununda listelemeye temel olacak eksenin (genellikle Renk) yıldızına tıklayın.
6. Gerekirse **Alt Özellik Ekle** ile eksene ölçü alt özellikleri bağlayın (Beden → Paça Boyu, Bel Genişliği…).

> **Dikkat:** Ana ekseni **ilk ürünü oluşturmadan önce** belirleyin; gruba ürün girildikten sonra kilitlenir.

### Mevcut gruba özellik ekleme / düzenleme
1. Listede gruba tıklayın.
2. **Özellik Ekle** → **Özellik Tipi**'ni seçin, *Varyant Ekseni* / *Zorunlu* işaretlerini ve *Sıra*'yı verin → **Ekle**.
3. Var olan bir özelliği değiştirmek için satırın sağındaki kalem simgesine tıklayın, **Kaydet**'e basın.

### Eksene alt özellik (ölçü) tanımlama
1. Detayda **Varyant Ekseni Alt Özellikleri** kartında ilgili eksenin yanındaki **+ Alt Özellik**'e tıklayın.
2. **Alt Özellik Tipi**'ni seçin (örn. Paça Boyu); gerekiyorsa **Zorunlu** işaretleyin ve **Sıra** verin → **Ekle**.
3. Alt özellik havuzda yoksa önce [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/) sayfasında oluşturun.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Özellik Ekle butonu pasifse havuzdaki tüm özellik tipleri bu gruba zaten eklidir; yeni bir özellik için önce Özellik Tipleri'nde tip oluşturun.

> **İpucu:** Sıra alanına 10'ar aralık bırakın (0, 10, 20…); sonradan araya özellik eklemek kolaylaşır.

> **Dikkat:** Ana Eksen sütununda kilit simgesi görüyorsanız grupta ürün vardır; ana eksen artık değiştirilemez. Yeni bir şablon gerekiyorsa yeni bir grup açıp ürünleri o gruba taşımanız gerekir.

> **Dikkat:** **Grubu Sil** butonu yoksa gruba bağlı ürünler vardır; önce ürünler başka gruba alınmalıdır.

> **Not:** Alt özellik eklerken "Bu eksen için eklenebilecek özellik kalmadı." mesajı, havuzdaki tüm tiplerin bu eksene zaten bağlı olduğu (ya da eksenin kendisi olduğu) anlamına gelir.

## İlgili sayfalar
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/) — özellik havuzu ve seçim değerleri
- [Ürün Kartları](/rehber/katalog/urun-kartlari/) — grup seçimi, varyant ve ölçü girişi
- [Katalog Ayarları](/rehber/katalog/katalog-ayarlari/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
