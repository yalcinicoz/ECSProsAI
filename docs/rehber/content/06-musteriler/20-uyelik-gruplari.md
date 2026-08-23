---
title: Üye Grupları
route: /crm/member-groups
group: Müşteriler
order: 20
summary: Üyelerin bağlandığı grupların (perakende, bayi/B2B vb.) ve grup özelliklerinin (en az sipariş tutarı, vade, onaylı sipariş) tanımlandığı ekran.
---

## Ne işe yarar
Üye Grupları, üyeleri davranış ve ticari koşullarına göre sınıflandırır: varsayılan perakende grubu, toptan (B2B) bayi grubu gibi. Her üye bir gruba bağlıdır (Üyeler > üye detayı > Üye Grubu). Grup kartında en az sipariş tutarı, vade günü, sipariş onayı ve girişsiz fiyat görünürlüğü gibi özellikler saklanır; kişiselleştirme segmentleri de üye grubuna bakar. Sol menüde **Müşteriler > Gruplar** olarak görünür.

## Ekran yerleşimi
![Üye Grupları listesi ve grup penceresi](img/crm-member-groups.webp)
1. **Başlık satırı** — "Üye Grupları", grup sayısı; sağda **+ Yeni Grup**.
2. **Tablo** — satıra tıklayınca düzenleme penceresi açılır.
3. **Pencere** — "Yeni Üye Grubu" ya da "Grup: kod" başlıklı form.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Grup kodu (küçük harf). |
| AD | Grup adı; varsayılan grupta **Varsayılan** rozeti. |
| ÜYE | Gruba bağlı üye sayısı. |
| ÖZELLİKLER | Özet: `B2B` · `onaylı sipariş` · `min 500₺` · `30 gün vade` (tanımlı olanlar; yoksa —). |
| DURUM | `Aktif` / `Pasif`. |
| Düzenle → | Satırın tıklanabilir olduğunu belirtir. |

Filtre ve sayfalama yoktur; aktif-pasif tüm gruplar listelenir.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Grup | Başlık sağı | "Yeni Üye Grubu" penceresi açılır. | Panele giriş |
| Satır tıklama | Tablo | "Grup: kod" düzenleme penceresi açılır. | — |
| Kaydet | Pencere | Grup oluşturulur/güncellenir, liste yenilenir. | Kod ve Ad dolu |
| Vazgeç | Pencere | Kaydetmeden kapatır. | — |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kod | Evet | Örn. `bayi`. Kaydedilirken küçük harfe çevrilir. Benzersiz: "Bu kod ile bir üye grubu zaten mevcut." Düzenlemede değiştirilemez (kilitli). |
| Ad | Evet | Grubun Türkçe adı; üye detayındaki Üye Grubu listesinde görünür. |
| En Az Sipariş Tutarı (₺) | Hayır | Boş = sınır yok. Listede `min X₺` olarak özetlenir. |
| Vade (gün) | Hayır | Boş = vade yok. Listede `N gün vade`. |
| Toptan (B2B) | Hayır | Grubun toptan/bayi grubu olduğunu işaretler; listede `B2B`. |
| Sipariş onay gerektirir | Hayır | Listede `onaylı sipariş`. |
| Girişsiz fiyat görünür | Hayır (varsayılan açık) | Kapalıysa bu grup için fiyatlar girişsiz gösterilmez. |
| Aktif | Hayır (varsayılan açık) | Pasif grup listede kalır; üye detayındaki seçim listesinde de görünür. |
| Sıra | Hayır (varsayılan 0) | Tam sayı; sıralama için. |

## Durumlar ve iş kuralları
- **Varsayılan** rozetli grup, yeni kayıt olan üyelerin otomatik bağlandığı gruptur; varsayılan grubu bu ekrandan değiştirme seçeneği yoktur.
- Grup silinmez; kullanılmayan grubu **Aktif** işaretini kaldırarak pasife alın.
- **Kod** küçük harfe çevrilir ve sonradan değiştirilemez.
- Özellik kutuları (B2B, onaylı sipariş, en az tutar, vade, girişsiz fiyat) grup kartında saklanır ve ÖZELLİKLER sütununda özetlenir; hangi kanal/akışta uygulandığı ilgili kanal ayarlarına bağlıdır.
- **ÜYE** sayısı otomatik hesaplanır.

## Adım adım
**Bayi (B2B) grubu açma**
1. Müşteriler > Gruplar'da **+ Yeni Grup**'a tıklayın.
2. **Kod** `bayi`, **Ad** "Bayi" yazın.
3. **Toptan (B2B)** kutusunu işaretleyin; gerekiyorsa **En Az Sipariş Tutarı** ve **Vade (gün)** girin.
4. **Kaydet**'e tıklayın.
5. Üyeler'de ilgili üyeyi açıp **Üye Grubu**'nu "Bayi" yapın ve kaydedin.

**Grubu pasife alma**
1. Satıra tıklayın, **Aktif** işaretini kaldırın, **Kaydet**'e tıklayın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Kod alanına büyük harf yazsanız da `bayi` gibi küçük harfle kaydedilir; arama/raporlarda küçük harfle arayın.

> **Dikkat:** "Bu kod ile bir üye grubu zaten mevcut." — pasif gruplar da sayılır; listede aynı kodlu pasif grup olup olmadığına bakın.

## İlgili sayfalar
- [Üyeler](/rehber/musteriler/uyeler/)
- [Cari Grupları](/rehber/cari/cari-gruplari/)
