---
title: Cari Grupları
route: /accounts/groups
group: Cari
order: 15
summary: Cari kartlarını sınıflandırmak için kullanılan grupların (müşteri/tedarikçi grupları) listelendiği ve yönetildiği ekran.
---

## Ne işe yarar
Cari Grupları, cari kartlarını (müşteri, tedarikçi, pazaryeri satıcısı) raporlama ve filtreleme için kümelemenizi sağlar; örneğin "Online Müşteriler", "Yurt Dışı Tedarikçiler". Grup, cari kartı formundaki **Grup** alanından seçilir ve Cari Kartlar listesinde hem sütun hem filtre olarak görünür. Finans/satın alma ekipleri kart açmadan önce grupları burada tanımlar.

## Ekran yerleşimi
![Cari Grupları listesi ve Yeni Grup penceresi](img/accounts-groups.webp)
1. **Başlık satırı** — "Cari Grupları", kayıt sayısı; sağda **Tümü / Aktif** anahtarı ve **+ Yeni Grup** butonu.
2. **Tablo** — gruplar; her satırın sonunda **Düzenle** butonu (satırın kendisi tıklanmaz).
3. **Pencereler** — "Yeni Cari Grubu" ve "Grup Düzenle" formları aynı alanları taşır.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Grup kodu (büyük harf, benzersiz). |
| AD | Grup adı; altında açıklama (varsa). |
| TİP | `Müşteri` / `Tedarikçi` / `Her İkisi` rozeti. |
| CARİ SAYISI | Bu gruba bağlı cari kartı sayısı. |
| DURUM | `Aktif` / `Pasif`. |
| Düzenle | Düzenleme penceresini açar. |

| Filtre | Ne yapar |
|---|---|
| Tümü / Aktif | Aktif seçiliyken pasif gruplar gizlenir. Varsayılan Tümü'dür. |

Sayfalama yoktur; tüm gruplar tek listede gelir. Kayıt yoksa "Grup bulunamadı." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Grup | Başlık sağı | "Yeni Cari Grubu" penceresi boş formla açılır. | Panele giriş |
| Düzenle | Satır sonu | "Grup Düzenle" penceresi dolu formla açılır. | — |
| Oluştur | Yeni Cari Grubu penceresi | Grup kaydedilir, liste yenilenir. | Kod ve Grup Adı dolu |
| Kaydet | Grup Düzenle penceresi | Değişiklikler kaydedilir. | — |
| İptal | Her iki pencere | Kaydetmeden kapatır. | — |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet (yalnız oluştururken) | Yazdıkça büyük harfe çevrilir. Örn. `ONLINE-MUS`. Benzersiz; aynı kod varsa "'ONLINE-MUS' kodu zaten mevcut." hatası. Düzenlemede değiştirilemez (alan görünmez). |
| Grup Adı | Evet | Listede AD sütunu ve cari formundaki Grup seçeneği. |
| Tip | Hayır (varsayılan Müşteri) | Müşteri / Tedarikçi / Her İkisi. |
| Açıklama | Hayır | Listede adın altında gösterilir. |
| Sıra | Hayır (varsayılan 0) | Tam sayı; sıralama için. |
| Aktif | — | Yalnız düzenlemede görünür; işareti kaldırınca grup `Pasif` olur. |

## Durumlar ve iş kuralları
- `Aktif` / `Pasif`: pasif grup silinmez; Tümü/Aktif anahtarıyla gizlenir. Gruba bağlı cari kartları etkilenmez.
- Grup **Tip**'i bilgi amaçlı bir etikettir; cari kartı formundaki Grup listesinde tip ayrımı yapılmadan tüm gruplar sunulur.
- Grup silme işlemi yoktur; kullanılmayan grubu pasife alın.
- **CARİ SAYISI** otomatik hesaplanır; kart açıp kapattıkça değişir.

## Adım adım
**Yeni grup tanımlama**
1. Cari > Cari Grupları'nda **+ Yeni Grup**'a tıklayın.
2. **Kod** (örn. `YURTDISI-TED`) ve **Grup Adı** yazın; **Tip**'i seçin.
3. İsterseniz açıklama ve sıra girin, **Oluştur**'a tıklayın.
4. Cari Kartlar'da ilgili kartı düzenleyip **Grup** alanından yeni grubu seçin.

**Grubu pasife alma**
1. Satırın sonundaki **Düzenle**'ye tıklayın.
2. **Aktif** işaretini kaldırın, **Kaydet**'e tıklayın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Kod sonradan değiştirilemediği için kısa ve anlamlı bir kural belirleyin (örn. `MUS-`, `TED-` ön ekleri).

> **Dikkat:** "'X' kodu zaten mevcut." hatası, pasif gruplar dahil tüm gruplar arasında kontrol edilir; Tümü görünümünde aynı kodlu pasif grubu arayın.

## İlgili sayfalar
- [Cari Kartlar](/rehber/cari/cari-kartlar/)
