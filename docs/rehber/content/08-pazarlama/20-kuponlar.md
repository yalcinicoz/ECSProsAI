---
title: Kuponlar
route: /promotion/coupons
group: Pazarlama
order: 20
summary: Müşterilerin sepette kod girerek kullandığı indirim kuponlarının listelendiği, oluşturulduğu, düzenlendiği, kullanım kayıtlarının izlendiği ve (hiç kullanılmamışsa) silindiği ekran.
---

## Ne işe yarar
Kuponlar, müşterinin sepette/ödeme adımında **kod girerek** aldığı yüzde ya da tutar indirimleridir (ör.
`HOSGELDIN10`). Pazarlama ekibi burada kupon tanımlar, kullanım limitlerini ve geçerlilik tarihlerini belirler,
hangi siparişlerde kullanıldığını izler. Kuponlar kampanyalardan bağımsızdır; kampanya kendiliğinden uygulanır,
kupon müşterinin kod girmesini ister.

## Ekran yerleşimi
![Kuponlar listesi](img/promotion-coupons.webp)
1. **Başlık ve sayaç** — "Kuponlar" ve toplam kayıt sayısı; sağda **+ Yeni Kupon**.
2. **Sekmeler** — `Aktif` / `Tümü`.
3. **Arama şeridi** — "Kupon kodu ara…" kutusu ve **Ara** butonu.
4. **Tablo** — kupon satırları; satıra tıklayınca düzenleme penceresi açılır.
5. **Sayfalama** — "← Önceki · sayfa / toplam · Sonraki →" (20 kayıt/sayfa).

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Kupon kodu (büyük harf). |
| AD | Kuponun adı (Türkçe). |
| İNDİRİM | `%10` ya da `50,00 ₺`; varsa yanında `(min 500 ₺)` en az sepet tutarı. |
| KULLANIM | Kullanım sayısı; toplam limit varsa `3 / 100` biçiminde. |
| GEÇERLİLİK | Başlangıç → bitiş; bitiş boşsa `süresiz`. |
| DURUM | `Aktif` / `Pasif` rozeti. |
| (son sütun) | **Kullanımlar** bağlantısı + "Düzenle →" ipucu. |

| Sekme / Filtre | Ne yapar |
|---|---|
| `Aktif` | Yalnız Aktif işaretli kuponlar (varsayılan sekme). |
| `Tümü` | Pasifler dahil tüm kuponlar. |
| Kupon kodu ara… + Ara | Kodun bir bölümüyle arar; Enter ya da **Ara** ile uygulanır. |

Liste oluşturma tarihine göre (yeniden eskiye) sıralıdır.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Kupon | Sağ üst | "Yeni Kupon" penceresi açılır. | Panele giriş yeterli. |
| Satır tıklama | Tablo | "Kupon: KOD" düzenleme penceresi açılır. | — |
| Kullanımlar | Satır sonu | "Kullanımlar: KOD (n)" penceresi: her kullanımda indirim tutarı, sipariş numarasının başı ve tarih; 20'şerli sayfalama. | — |
| Kaydet | Pencere altı | Kupon oluşturulur/güncellenir, pencere kapanır. | Kod ≥ 3 karakter, Ad dolu, İndirim değeri > 0. |
| Vazgeç | Pencere altı | Değişiklik kaydedilmeden kapanır. | — |
| Sil ⚠️ | Pencere sol altı (yalnız kullanım sayısı 0 ise) | "'KOD' kuponu silinsin mi? Bu işlem geri alınamaz." onayı → kupon silinir. | **Yalnız hiç kullanılmamış kupon** silinebilir; kullanılmış kuponda buton yerine "Kullanılmış kupon silinemez; pasife alabilirsiniz." notu görünür. |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kupon Kodu | Evet | En az 3 karakter; otomatik büyük harfe çevrilir (`HOSGELDIN10`). Kayıt sonrası değiştirilemez. Aynı kod ikinci kez tanımlanamaz ("'X' kupon kodu zaten mevcut."). |
| Ad | Evet | Görünen ad, ör. "Hoş geldin indirimi". |
| İndirim Tipi | — | `Yüzde (%)` ya da `Tutar (₺)`. |
| İndirim Değeri | Evet | Sıfırdan büyük; yüzde 100'ü aşamaz. |
| Toplam Limit | Hayır | Kuponun toplam kaç kez kullanılabileceği; boş = sınırsız. |
| Üye Başı Limit | Hayır | Aynı üyenin kaç kez kullanabileceği; boş = sınırsız. |
| En Az Sepet (₺) | Hayır | Kuponun geçerli olması için en düşük sepet tutarı; boş = yok. |
| Başlangıç | — | Geçerlilik başlangıcı (günün başından). |
| Bitiş (boş = süresiz) | Hayır | Son geçerli gün (günün sonuna kadar). |
| Yalnız ilk siparişte geçerli | — | İşaretliyse daha önce siparişi olan üye kullanamaz. |
| Aktif | — | Yalnız düzenlemede görünür; kaldırılırsa kupon sepette kabul edilmez. Yeni kupon Aktif başlar. |

## Durumlar ve iş kuralları
- Sepette kod girildiğinde şu kontroller yapılır ve müşteriye ilgili mesaj gösterilir: kod yok/pasif → "Geçersiz veya
  pasif kupon kodu."; başlangıç gelmemiş → "Kupon henüz aktif değil."; bitiş geçmiş → "Kupon süresi dolmuş."; toplam
  limit dolmuş → "Kupon kullanım limiti dolmuş."; sepet küçük → "Bu kupon için minimum sepet tutarı … ₺ olmalıdır.";
  ilk-sipariş kuralı → "Bu kupon yalnızca ilk sipariş için geçerlidir."; üye başı limit dolmuş → "Bu kuponu zaten
  kullandınız."
- Yüzde indirim sepet toplamı üzerinden hesaplanır; tutar indirimi sabit düşer. Kupon indirimi sipariş kalemlerine
  satır tutarı oranında dağıtılır (iade tutarı doğru çıksın diye).
- Kupon **yalnız kart ile ödemede** geçerlidir; müşteri kapıda ödeme seçerse uygulanmış kupon sepetten kaldırılır ve
  ödeme sayfasında bilgi notu gösterilir.
- Misafir (üyeliksiz) siparişte kupon kullanılabilir ama üye başı sayaç tutulmaz.
- Kullanılmış kupon silinemez; "Aktif" kutusunu kaldırarak pasife alın. Kullanım geçmişi korunur.

## Adım adım
**Hoş geldin kuponu tanımlama**
1. **+ Yeni Kupon** → Kupon Kodu `HOSGELDIN10`, Ad "Hoş geldin indirimi", İndirim Tipi Yüzde, İndirim Değeri `10`.
2. Üye Başı Limit `1`, "Yalnız ilk siparişte geçerli" işaretleyin; isterseniz En Az Sepet `300`.
3. Bitiş tarihini boş bırakın (süresiz) → **Kaydet**.

**Kuponun nerede kullanıldığını görmek**
1. Satırdaki **Kullanımlar** bağlantısına tıklayın; indirim tutarı, sipariş ve tarih listelenir.

**Kuponu kapatma**
1. Satıra tıklayın; hiç kullanılmadıysa **Sil**, kullanıldıysa **Aktif** kutusunu kaldırıp **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Kupon kodu kayıt sonrası değiştirilemez; yazım hatası varsa kuponu silip (kullanılmamışsa) yeniden
> oluşturun.

> **İpucu:** Müşteri "kupon geçmiyor" dediğinde sırasıyla Durum (Aktif mi), GEÇERLİLİK, KULLANIM (limit doldu mu),
> En Az Sepet ve ödeme yöntemini (kapıda ödemede kupon geçmez) kontrol edin.

> **Not:** "Kaydet" butonu Kod 3 karakterden kısa, Ad boş ya da İndirim Değeri 0 iken pasiftir.

## İlgili sayfalar
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
- [Hediye Kartı](/rehber/pazarlama/hediye-karti/)
