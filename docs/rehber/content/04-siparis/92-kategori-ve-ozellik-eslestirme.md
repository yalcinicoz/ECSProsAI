---
title: Kategori ve Özellik Eşleştirme
route: /marketplaces/eslestirme
group: Sipariş Yönetimi
order: 92
summary: Ürün gruplarınızı, özelliklerinizi ve özellik değerlerinizi pazaryeri kategori/özellik/değerleriyle eşlediğiniz ekran; kırılan veya değişen eşlemelerin gözden geçirilmesi.
---

## Ne işe yarar
Pazaryerine ürün gönderebilmek için her ürünün pazaryerinde hangi kategoriye gideceği ve pazaryerinin zorunlu özelliklerinin bizdeki hangi özellik/değerden doldurulacağı bilinmelidir. Bu ekranda **ürün gruplarınızı** pazaryeri kategorilerine, pazaryeri kategorisinin **özelliklerini** kendi özelliklerinize ve **değerlerini** kendi değerlerinize eşlersiniz. Eşlemeler firma genelinde geçerlidir; pazaryeri kategoriyi kaldırdığında ya da bir özelliği zorunlu yaptığında etkilenen eşlemeler **Gözden Geçir** sekmesine düşer.

Eşleme tamamlanmadan mağaza detayındaki hazırlık denetimi ürünleri **Eksik** sayar; ürün gönderimi yalnız **Hazır** ürünlerle çalışır (bkz. [Pazaryerleri](/rehber/siparis/pazaryerleri/)).

## Ekran yerleşimi
![Pazaryeri Eşleştirme — pazaryeri çipleri, sayaçlar ve Kategori Eşleme sekmesi](img/marketplaces-eslestirme.webp)
1. **Başlık** — "Pazaryeri Eşleştirme".
2. **Pazaryeri çipleri** — logo + ad; yalnız referans verisi indirilmiş pazaryerleri seçilebilir. Sağda sayaçlar: **N eşli · N eşsiz · N gözden geçirilecek**.
3. **Sekmeler** — `Kategori Eşleme` · `Özellik & Değer` · `Gözden Geçir (N)`.
4. **İçerik** — Kategori sekmesinde solda ürün grubu listesi, sağda eşleme editörü; diğer sekmelerde tablo.

*(1) Başlık · (2) Pazaryeri çipleri ve sayaçlar · (3) Sekmeler · (4) Sol liste + sağ editör*

Sayfaya Pazaryerleri sayfasındaki **Eşleştirme** butonundan gelinir. Seçili pazaryeri ve sekme adres çubuğunda taşınır (`?mp=trendyol&tab=kategoriler`).

## Liste ve filtreler
| Filtre / öğe | Ne yapar |
|---|---|
| Pazaryeri çipi | Hangi pazaryerinin eşlemeleri üzerinde çalışıldığını seçer. Referans verisi indirilmemiş pazaryeri soluk ve tıklanamaz; üzerine gelince "Önce referans verisini indirin (Pazaryerleri → Referans Verisi)" ipucu çıkar. |
| N eşli / N eşsiz / N gözden geçirilecek | Seçili pazaryeri için ürün grubu sayaçları. Eşsiz sıfırdan büyükse sarı, gözden geçirilecek sıfırdan büyükse kırmızı. |
| Grup ara… (Kategori sekmesi, sol liste) | Ürün grubu adı/kodu üzerinde yazdıkça filtreler. |
| Yalnız eşsiz / gözden geçirilecekler (onay kutusu) | Sol listeyi eşlemesi olmayan ya da durumu sağlıklı olmayan gruplarla sınırlar. |

Sol listede her satır: durum simgesi (`—` eşleme yok · `✓` sağlıklı · `⚠` gözden geçirilmeli · `⛔` kırıldı), grup adı ve sağda ürün sayısı. Satıra tıklayınca sağda o grubun editörü açılır; hiçbir grup seçili değilse "Soldan bir ürün grubu seçin." yazar.

## Sekmeler

### Kategori Eşleme
Editör başlığında grup adı, kodu ve ürün sayısı. Eşleme sağlıklı değilse üstte renkli kutu: `⛔` kırmızı (kırıldı) ya da `⚠` sarı (gözden geçirilmeli) + açıklama + "Düzeltip kaydedin — kayıt eşlemeyi onaylanmış sayar."

Eşleme kipi (radyo): **Birebir** · **Koşullu** · **Havuz**.

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Birebir → HEDEF KATEGORİ | Evet | Pazaryeri kategorisi arama kutusu (en az 2 harf; sonuç tam yol olarak listelenir, örn. `Ayakkabı > Spor Ayakkabı > Sneaker`). Altında sistem önerileri "Ad %skor" çipleri — tıklayınca hedef olur. Seçileni `×` ile temizlersiniz. |
| Koşullu → KURALLAR | En az bir kural önerilir | "İlk eşleşen kazanır" sıralı kural listesi. Her kural: **Özellik seç…** (kendi özellik tipleriniz) `=` **Değer seç…** (o özelliğin değerleri) → **Bu kuralın hedef kategorisi…** (pazaryeri kategori arama). Sağda ↑ ↓ sıralama ve 🗑 kuralı sil. **Kural Ekle** yeni boş kural ekler. Örn. `cinsiyet = Kadın → Kadın Pantolon`. |
| Koşullu → VARSAYILAN HEDEF | Hayır | Hiçbir kural tutmazsa kullanılacak kategori. Boş bırakılırsa kural tutmayan ürün denetimde "Hiçbir kategori kuralı tutmadı" nedeniyle **Eksik** listesine düşer. Öneri çipleri burada da çıkar. |
| Havuz → ADAY KATEGORİLER | En az bir aday | Grubun ürünlerinin gidebileceği aday pazaryeri kategorileri listesi (`×` ile çıkarılır). **Aday kategori ekle…** arama kutusu ve öneri çipleriyle eklenir. Ürün bazında hangi adayın kullanılacağı mağaza detayı → Ürünler → **Tamamla** penceresinde seçilir; seçilmemiş ürünler "Kategori ataması bekliyor (havuz)" nedeniyle **Eksik** görünür. |

| Buton/Aksiyon | Nerede | Ne olur | Ön koşul |
|---|---|---|---|
| Kaydet | Editör sağ alt | Eşlemeyi kaydeder ("Eşleme kaydedildi."), sağlıksız eşlemeyi onaylanmış sayar, sayaçları tazeler. | Kip için gerekli hedef(ler) seçili |
| Eşlemeyi Kaldır | Editör sol alt (kırmızı) | Grubun eşlemesini siler ("Eşleme kaldırıldı."). ⚠️ Grubun ürünleri hazırlık denetiminde "Kategori eşlemesi yok" nedeniyle Eksik'e düşer. | Kayıtlı eşleme var |
| Öneri çipi (Ad %skor) | Hedef alanlarının altında | Çipteki pazaryeri kategorisini hedef/aday olarak seçer. Skor, grup adı ile kategori yolu arasındaki benzerliktir. | — |

### Özellik & Değer
Pazaryeri özellikleri **kategoriye bağlıdır**; bu yüzden sekme, en az bir grup eşlendikten sonra çalışır. Hiç eşleme yoksa "Önce Kategoriler sekmesinden en az bir grup eşleyin — özellikler hedef kategoriye göre listelenir." mesajı görünür.

| Filtre | Ne yapar |
|---|---|
| PAZARYERİ KATEGORİSİ (açılır liste) | Eşlenmiş hedef kategoriler; her seçenek "kategori yolu — (eşleyen grup adları)" biçimindedir. Seçilen kategorinin özellikleri tabloya gelir. |

Tablo sütunları:
| Sütun | Anlamı |
|---|---|
| Pazaryeri Özelliği | Özellik adı ve rozetleri: `Zorunlu` (kırmızı — gönderimde doldurulması şart), `Varyant ekseni` (mavi — beden gibi; değeri varyanttan gelir), `Gözden geçir` (sarı — pazaryeri tarafında değişti). Altında sarı açıklama notu olabilir. |
| Tip | `Liste (N)` (pazaryerinin N seçenekli değer listesi), `Serbest` (metin), `Liste (N) + serbest` (listeden seçilebilir ya da serbest yazılabilir). |
| Bizim Karşılık | Strateji açılır listesi + ikinci alan (aşağıda). Değişiklik **anında kaydedilir**; satır altında "✓ kaydedildi" ya da hata görünür. |
| Değerler | `Değer eşle` stratejisinde ve özellik seçiliyken "eşlenen/toplam" (tamamı eşliyse yeşil, değilse sarı); diğer stratejilerde `—`. |
| ▾ / ▴ | Satırı açıp kapatır; açılınca altta **Değer eşleme paneli** gelir. |

Strateji seçenekleri (Bizim Karşılık):
| Strateji | Ne yapar | Ek alan |
|---|---|---|
| Değer eşle | Kendi özelliğinizin değerleri pazaryeri değerlerine tek tek eşlenir; gönderimde eşlenen değer gider. | **— seç —**: kendi özellik tipiniz |
| Serbest geçir | Kendi değerinizin metni aynen gönderilir. Yalnız `+ serbest` / `Serbest` tipli özelliklerde seçilebilir (aksi halde seçenek devre dışı). | kendi özellik tipiniz |
| Sabit değer | Her üründe aynı sabit değer gönderilir. | **Sabit değer…** metin kutusu (kutudan çıkınca kaydedilir) |

Satır açıldığında strateji `Değer eşle` değilse ya da özellik seçilmemişse panel yerine "Değer eşlemek için stratejiyi "Değer eşle" yapıp bizim özellik tipini seçin." notu görünür.

#### Değer eşleme paneli
| Öğe | Anlamı |
|---|---|
| N/M eşlendi | Kendi değerlerinizden kaçının pazaryeri değeriyle eşli olduğu. |
| Önerileri Uygula (N) | %90 ve üzeri benzerlikteki önerileri taslağa doldurur; **Kaydet**'e basana kadar yazılmaz. Uygulanacak öneri yoksa devre dışı. |
| Değer satırı | Kendi değeriniz → açılır liste (`— eşleme yok —` + pazaryeri değerleri). Sağda: `✓` eşli · "öneri: X %skor" · `değer kalktı` (kırmızı — pazaryeri değeri kaldırmış, yeni değer seçin). |
| Kaydet | Değişen satırları kaydeder: "N değer kaydedildi." Değişiklik yoksa devre dışı. |

### Gözden Geçir
Pazaryeri referans verisi güncellendiğinde sağlığı bozulan eşlemeler burada listelenir. Boşsa "Gözden geçirilecek eşleme yok — her şey sağlıklı."

| Sütun | Anlamı |
|---|---|
| Durum rozeti | `Kırıldı` (kırmızı — hedef kategori/değer pazaryerinden kaldırıldı) · `Gözden geçir` (sarı — ad değişti, serbest özellik listeye bağlandı vb.). |
| Tür | `Kategori` · `Özellik` · `Değer`. |
| Başlık / not | Etkilenen eşleme ve ne değiştiği. |
| Eşlemeye Git | Yalnız kategori satırlarında; Kategori Eşleme sekmesinde ilgili grubu açar. |
| Onayla | Eşlemeyi olduğu gibi onaylar, listeden düşer. |

## Durumlar ve iş kuralları
- **Eşleme durumları:** `active` (✓ sağlıklı) · `needs_review` (⚠ gözden geçirilmeli) · `broken` (⛔ kırıldı). Kırık ya da gözden geçirilecek eşleme sayaçta "gözden geçirilecek" olarak sayılır; kırık eşlemenin ürünleri denetimde "Kategori eşlemesi kırık" nedeniyle **Eksik** olur.
- **Sağlık güncellemesi** her referans senkronundan sonra otomatik çalışır: kategori kaldırıldı → eşleme `broken`; kategori/özellik adı değişti → `needs_review`; serbest girişli özellik listeye bağlandı → o özelliği "Serbest geçir" gönderen eşleme `needs_review`; değer kaldırıldı → değer eşlemesi `broken`; kaldırılan kayıt geri gelirse durum kendiliğinden düzelir. Editörde **Kaydet** ya da Gözden Geçir'de **Onayla** eşlemeyi yeniden `active` yapar.
- **Gönderimde kategori önceliği:** ürün istisnası (Tamamla penceresi / pazaryeri reddi / havuz ataması) > koşullu kural sonucu > birebir eşleme. Ürün istisnaları buradaki genel eşlemeyi değiştirmez.
- **Gönderimde özellik değeri önceliği:** ürün-özel pazaryeri değeri (Tamamla penceresinden) > değer eşlemesi > sabit değer > serbest geçirme. Varyant ekseni özelliklerin (beden vb.) değeri varyant verisinden gelir, burada eşlenmez ve denetimde sayılmaz.
- **Bizim kategori = ürün grubu:** eşleme birimi katalogdaki ürün grubudur; her ürünün tam bir grubu vardır. Site menü kategorileri eşlemede kullanılmaz.
- Eşlemeler **firma genelidir**; mağaza bazlı ayrı eşleme ekranı yoktur.
- **Yetki:** Sayfa giriş yapmış panel kullanıcılarına açıktır; ayrı izin aranmaz.

## Adım adım

**1. Bir ürün grubunu birebir eşleme**
1. Pazaryeri çipini seçin → **Kategori Eşleme** sekmesi.
2. Solda grubu bulun (Grup ara… ya da "Yalnız eşsiz / gözden geçirilecekler").
3. Kip **Birebir** → öneri çiplerinden uygun olana tıklayın ya da HEDEF KATEGORİ kutusuna en az 2 harf yazıp tam yolu seçin.
4. **Kaydet** → "Eşleme kaydedildi." Sayaçlarda eşli sayısı artar.

**2. Zorunlu özellikleri eşleme**
1. **Özellik & Değer** sekmesi → PAZARYERİ KATEGORİSİ listesinden eşlediğiniz kategoriyi seçin.
2. `Zorunlu` rozetli satırlarda Bizim Karşılık: strateji **Değer eşle** + kendi özellik tipinizi seçin (örn. Cinsiyet → cinsiyet). Bizde karşılığı olmayan zorunlu özellik için **Sabit değer** girin ya da ürün bazında Tamamla penceresinden doldurulmasını bekleyin.
3. Satırı açın → **Önerileri Uygula (N)** → kalanları açılır listeden seçin → **Kaydet**.
4. Değerler sütununda "M/M" yeşil olunca özellik tamamdır.

**3. Gözden geçirilecekleri temizleme**
1. **Gözden Geçir (N)** sekmesini açın.
2. `Kırıldı` kategori satırında **Eşlemeye Git** → editörde yeni hedef seçip **Kaydet**.
3. Yalnız ad değişimi gibi zararsız durumlarda **Onayla**.
4. Mağaza detayı → Ürünler → **Denetle** ile etkilenen ürünleri yeniden denetleyin.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** "Koşullu" kip, bizde tek grup olan ama pazaryerinde cinsiyete/yaşa göre ayrılan kategoriler için (Pantolon → Kadın Pantolon / Erkek Pantolon); "Havuz" kip, kategori seçiminin ürüne bakmadan yapılamadığı durumlar için (Bot → Bağcıklı Bot / Outdoor Bot / Günlük Bot).

> **Dikkat:** Havuz kipinde kaydetmek ürünleri hazır yapmaz; her ürün için aday seçimi mağaza detayındaki **Tamamla** / **Toplu Tamamla** penceresinden yapılır.

> **Dikkat:** Bir özellikte **Serbest geçir** seçeneği soluksa pazaryeri o özellik için serbest metin kabul etmiyordur — Değer eşle ya da Sabit değer kullanın.

> **Not:** Özellik senkronu yapılmamış bir kategoriye eşleme yapabilirsiniz, ancak Özellik & Değer sekmesinde özellik listesi boş gelir ve ürünler denetimde "Kategori özellikleri henüz indirilmedi" nedeniyle Eksik kalır. Pazaryerleri → **Referans Verisi** → `Özellikler + Değerler` kapsamını çalıştırın.

> **Not:** Pazaryeri çipleri soluksa önce Pazaryerleri sayfasından **Referans Verisi** ile kategori ağacını indirin.

## İlgili sayfalar
- [Pazaryerleri](/rehber/siparis/pazaryerleri/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
- [Özellik Tipleri](/rehber/katalog/ozellik-tipleri/)
