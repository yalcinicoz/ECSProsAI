---
title: Şans Oyunları
route: /promotion/games
group: Pazarlama
order: 35
summary: Mobil uygulamadaki Çarkıfelek, Salla Kazan ve Kazı Kazan kampanyalarının tanımlandığı, ödül ve hak kurallarının yönetildiği ekran.
---

## Ne işe yarar
Şans oyunları günlük kampanya araçlarıdır: bugün çark, yarın kazı kazan yayına alınır. Yayındaki oyun mobil uygulamanın ana
sayfasında taşınabilir yuvarlak bir ikon olarak çıkar; müşteri oynar, kazandığı kupon otomatik olarak Kuponlarım'a düşer.
**Sonucu her zaman sunucu belirler**: hangi dilimde duracağı, kutucuklarda ne çıkacağı, kupon kodu — hepsi burada tanımlanan
ödül ağırlıklarına göre sunucuda hesaplanır; uygulama yalnız animasyon oynatır. Pazarlama personeli kullanır.

## Ekran yerleşimi
![Şans Oyunları listesi](img/promotion-games.webp)
1. **Başlık** — "Şans Oyunları" + oyun sayısı; sağda **+ Yeni Oyun**.
2. **Sekmeler** — Yayında (bugün aktif olanlar) / Tümü.
3. **Oyun tablosu** — satıra tıklayınca oyun detayı açılır.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| OYUN | Ad ve altında kod (`/oyunlar/{kod}` derin linki). |
| TİP | Çarkıfelek / Salla Kazan / Kazı Kazan. |
| HAK | Hak dönemi ve sayısı (Günde 1, Haftada 3, Toplam 1). |
| KAZANMA | `Herkes kazanır` (Pas yok) ya da `Şanslı` (Pas dilimi/kaybetme olasılığı var). |
| ÖDÜL | Aktif ödül sayısı. |
| OYNANIŞ | Toplam oynanış / kazanan sayısı. |
| TARİH | Başlangıç → bitiş (boş = süresiz). |
| DURUM | `Yayında` (aktif + tarih aralığında, mobilde ikon çıkar) · `Aktif (tarih dışı)` · `Pasif`. |

Arama kutusu ad ve kodda arar; her sütun başlığında filtre vardır.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Oyun | Liste sağ üst | Boş oyun formu açılır (3 örnek ödül satırıyla). | `promotion.manage` |
| Kaydet | Detay sağ üst | Genel + Ödüller + Metinler birlikte kaydedilir; hata varsa ilgili sekmeye dönülür. | Platform, kod, ad ve en az bir kazandıran ödül |
| Sil ⚠️ | Detay sağ üst | Hiç oynanmamış oyun silinir; oynanmış oyun silinemez ("pasife alın"). | `promotion.manage` |
| ⓘ bilgi ikonu | Alan etiketleri | Alanın açıklaması. | — |

## Form alanları

### Genel
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Platform | Evet | Oyunun çıkacağı satış kanalı (mobil uygulamanın kanalı). |
| Oyun tipi | Evet | Çark: ödüller dilim sırasıyla döner. Salla kazan: kutudan tek ödül çıkar. Kazı kazan: 6 kutucuk, 3 aynı = kazandı. |
| Kod | Evet | Küçük harf/rakam/tire; kanal içinde benzersiz (ör. `cark`). Mobil derin link ve push anahtarı. |
| Ad, Alt başlık, Açıklama | Ad zorunlu | Oyun ekranı metinleri. |
| Hak dönemi / Hak sayısı | Evet | Günde n (İstanbul günü), Haftada n (ISO hafta) ya da Toplam n (kampanya boyunca). |
| Kupon geçerliliği (gün) | Evet | Kazanılan kuponun son kullanma süresi (varsayılan 7). |
| Başlangıç / Bitiş | Başlangıç | Yayın aralığı; bitiş boş = süresiz. |
| İkon görseli (URL), Tema rengi, Vurgu rengi | Hayır | Mobil ikon ve oyun ekranı renkleri; boşsa uygulama varsayılanı. |
| Düğme metni, Sıra | Hayır | Ana düğme metni; birden çok oyun yayındaysa ikon sırası. |
| Aktif | — | Kapalıysa tarih aralığında bile yayında değildir. |
| Herkes kazanır | — | Açıkken Pas ödül tanımlanamaz, her oynanış kazanır. |

### Ödüller
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Sıra (▲▼) | — | Çarkta dilim sırası (saat yönünde, üstten). |
| Ad / Kısa | Ad | "50 TL İndirim" / dilim ve kutucuk metni "50 TL". |
| Tür | Evet | Kupon (indirim), Puan, Pas (kaybetti). |
| Değer | Kupon/puan | Kupon: TL ya da % + değer (yüzde 100'ü aşamaz). Puan: sadakat puanı. |
| Asgari sepet / Koşul metni | Hayır | Kuponun asgari sepet tutarı; müşteriye görünen koşul cümlesi ("300 TL üzeri"). |
| Renk | Hayır | Dilim/kutucuk rengi; boşsa mobil paleti. |
| Ağırlık | Evet | Olasılık payı; tablo yanında yüzdesi hesaplanır. 0 = hiç çıkmaz (kazı kazanda yalnız dolgu değeri). |

> **Dikkat:** Kazı kazanda en az 3 farklı kazandıran ödül gerekir (kazanan 3 kutucuk + hiçbir değerin 3 kez geçmediği dolgu).
> Çarkta en az 2 dilim gerekir.

### Metinler
Mobil uygulama hiçbir metin üretmez; durum etiketleri ("Bugün 1 hakkın var", "Yarın tekrar gel", "Hakkın bitti", "Oynamak için
giriş yap", "Kampanya bitti") ve sonuç panelindeki başlık/açıklamalar burada yazılır. Yer tutucular: `{n}` kalan hak, `{next}`
sonraki hak günü, `{prize}` ödül adı. Kurallar metni (i) ikonuyla açılır.

### Oynanışlar
Kim, ne zaman, hangi dönemde, sonuç, ödül ve üretilen kupon kodu / puan.

## Durumlar ve iş kuralları
- **Yayında:** Aktif + başlangıç ≤ bugün + (bitiş boş ya da ≥ bugün). Yayındaki her oyun için mobilde ayrı ikon çıkar.
- **Misafir oynayamaz:** kupon üyeye bağlandığı için oyun kartını görür, oynamak için girişe yönlenir.
- **Hak düşümü ve tekrar:** her oynanış hakkı düşürür. Hak bitmişken tekrar dokunulursa yeni ödül üretilmez; aynı dönemin
  son sonucu aynen gösterilir.
- **Kupon:** kazanınca üyeye özel, tek kullanımlık kupon üretilir (`OYUNKODU+DEĞER-XXXX`, ör. `CARK50-7K2M`) ve Kuponlarım'a
  düşer; geçerlilik "kupon geçerliliği (gün)" ayarıdır. Puan ödülü sadakat hesabına işlenir.
- **Silme:** oynanmış oyun silinmez; yayından kaldırmak için Aktif kutusunu kaldırın ya da bitiş tarihi verin.
- **Denetim:** oyun ekleme/düzenleme/silme Ayarlar › Denetim Logları'na (varlık tipi `Game`) yazılır.

## Adım adım
**Günlük çark kampanyası**
1. **+ Yeni Oyun** → Platform, tip Çarkıfelek, kod `cark`, ad "Çarkıfelek", Hak: Günde 1, Başlangıç bugün, Bitiş ay sonu.
2. Ödüller: "%10 İndirim" (Kupon, %, 10, ağırlık 3), "50 TL İndirim" (Kupon, TL, 50, asgari 300, ağırlık 1), "Bir dahaki sefere" (Pas, ağırlık 4).
3. Metinler sekmesinde etiketleri kontrol edin → **Kaydet**. Uygulamada ikon anında görünür.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Kayıp olasılığını Pas ödülünün ağırlığıyla ayarlayın; olasılık sütunu anlık yüzdeyi gösterir.

> **Dikkat:** "Herkes kazanır" açıkken Pas satırı varsa kayıt reddedilir: satırı kaldırın ya da pasife alın.

## İlgili sayfalar
- [Kuponlar](/rehber/pazarlama/kuponlar/) — kazanılan kuponlar burada listelenir (kişiye özel).
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
