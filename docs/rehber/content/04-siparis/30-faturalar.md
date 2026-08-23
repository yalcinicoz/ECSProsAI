---
title: Faturalar
route: /orders/invoices
group: Sipariş Yönetimi
order: 30
summary: Kesilen faturaların listelendiği, entegratör PDF adresinin kaydedildiği, faturanın iptal edildiği ve fatura serilerinin tanımlandığı ekran.
---

## Ne işe yarar
Faturalar sayfası, siparişlerden kesilen faturaların kaydıdır. Fatura **sipariş detayındaki "+ Fatura Oluştur"** ile kesilir; bu
sayfada ise faturalar listelenir, bir faturaya tıklanarak entegratörden (e-Arşiv/e-Fatura sağlayıcısı) alınan PDF adresi kaydedilir
ya da fatura iptal edilir. Fatura numarasının türetildiği **fatura serileri** de buradaki "Fatura Serileri" penceresinden tanımlanır.
Muhasebe/operasyon personeli kullanır.

## Ekran yerleşimi
![Faturalar listesi — durum sekmeleri, fatura tablosu, sağ üstte Fatura Serileri butonu](img/orders-invoices.webp)
1. **Başlık** — "Faturalar" + kayıt sayısı; sağda **Fatura Serileri** butonu.
2. **Durum sekmeleri** — Oluşturulan / İptal Edilen / Tümü.
3. **Fatura tablosu** — satıra tıklayınca fatura penceresi açılır.
4. **Sayfalama** — 20 kayıt/sayfa.
5. **Pencereler** — "Fatura {no}" detay penceresi ve "Fatura Serileri" penceresi.

## Liste ve filtreler
| Sekme | Durum |
|---|---|
| Oluşturulan (varsayılan) | `created` |
| İptal Edilen | `cancelled` |
| Tümü | hepsi |

| Sütun | Anlamı |
|---|---|
| FATURA NO | Seriden türetilen numara: seri kodu + yıl + 9 haneli sıra (ör. `MSH2026000000001`). |
| TİP | e-Arşiv / e-Fatura / İhracat. |
| ALICI | Faturadaki alıcı adı. |
| TUTAR | Fatura toplamı (₺). |
| PDF | Entegratör PDF adresi kayıtlıysa "✓", değilse "—". |
| DURUM | `Oluşturuldu` (yeşil) / `İptal` (kırmızı). |
| TARİH | Fatura tarihi. |
| (son sütun) | "Detay →" — satır tıklanabilir. |

Arama kutusu yoktur; belirli bir siparişin faturaları sipariş detayındaki **Faturalar** kartında listelenir. Liste boşken
"Fatura bulunamadı. Fatura, sipariş detayındaki "Fatura Oluştur" ile kesilir." mesajı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Satır tıklama | Liste | "Fatura {no}" penceresi: durum rozeti, tip · tarih, Alıcı, Tutar, "Sipariş: görüntüle" bağlantısı, Entegratör PDF durumu, PDF adresi alanı. | — |
| Sipariş: görüntüle | Fatura penceresi | Siparişin detay sayfasına gider. | — |
| PDF Adresini Kaydet | Fatura penceresi | Girilen https adresini faturaya kaydeder; müşteri sitede "Faturayı Görüntüle" butonunu görür. **Boş kaydetmek mevcut adresi siler.** | — |
| Faturayı İptal Et ⚠️ | Fatura penceresi (sol alt, kırmızı) | Fatura `İptal` olur. Geri alınamaz; numara yeniden kullanılmaz. | Durum `Oluşturuldu` |
| Kapat | Fatura penceresi | Pencereyi kapatır. | — |
| Fatura Serileri | Liste başlığı | "Fatura Serileri" penceresi: mevcut seriler (ad · e-Arşiv/e-Fatura/İhracat kodları · `Pasif` rozeti) + YENİ SERİ formu. | — |
| + Seri Ekle | Fatura Serileri penceresi | Yeni seri oluşturur; form temizlenir, liste yenilenir. | Firma ve e-Arşiv Seri dolu |

## Form alanları

### Entegratör PDF Adresi (fatura penceresi)
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Entegratör PDF Adresi (https) | Hayır | Ör. `https://.../earchive/....pdf`. Adres müşteriye doğrudan verilmez; site sunucusu üzerinden görüntülenir. |

### Yeni seri (Fatura Serileri penceresi)
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Firma | Evet | Serinin ait olduğu firma. |
| Ad | Hayır | Ör. "Ana Seri"; boşsa listede e-Arşiv kodu görünür. |
| e-Arşiv Seri | Evet | Büyük harfe çevrilir (ör. `MSH`). |
| e-Fatura Seri | Hayır | Boşsa e-Arşiv ile aynı kabul edilir. |
| İhracat Seri | Hayır | Boşsa e-Arşiv ile aynı kabul edilir. |

## Durumlar ve iş kuralları
| Rozet | Kod | Anlamı |
|---|---|---|
| Oluşturuldu | `created` | Geçerli fatura. |
| İptal | `cancelled` | İptal edilmiş; yeniden iptal edilemez ("Fatura zaten iptal edilmiş."). |

- Fatura oluşturabilmek için **en az bir aktif seri** gerekir ("Aktif fatura serisi bulunamadı.").
- Numara, seri + fatura tipi + yıl bazında bir artar; tip başına ayrı sayaç (e-Arşiv/e-Fatura/İhracat).
- **Paket başına fatura normaldir**: sipariş tedarikçiye göre paketlere bölündüğünde her pakete ayrı fatura düzenlenir; tek fatura
  bilinçli bir istisnadır (paket birleştirme — bkz. Sipariş Detayı).
- İptal fatura kaydını silmez; listede İptal Edilen sekmesinde kalır. İptal edilen numara havuza dönmez.
- Entegratör PDF'i kayıtlı olmayan faturada müşteri sitede fatura görüntüleyemez.

## Adım adım
**Fatura kesme (sipariş detayından)**
1. Siparişi açın → Faturalar kartında **+ Fatura Oluştur**.
2. Seri, tip, tarih ve alıcı bilgilerini kontrol edin → **Fatura Oluştur**.
3. Entegratörden PDF adresi alınınca **Faturalar** sayfasında faturaya tıklayın, adresi yapıştırın → **PDF Adresini Kaydet**.

**Yeni fatura serisi tanımlama**
1. **Faturalar → Fatura Serileri**.
2. Firma seçin, Ad ve e-Arşiv Seri kodunu yazın (gerekirse e-Fatura/İhracat) → **+ Seri Ekle**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Fatura iptali geri alınamaz; yanlış kesildiyse iptal edip yeniden kesin (yeni numara alır).

> **İpucu:** Sipariş detayında seri listesi boş geliyorsa bu sayfadan seri tanımlamanız yeterlidir; pencereyi kapatıp açmanız gerekmez.

> **Not:** PDF kutusunu boş bırakıp kaydetmek mevcut adresi siler; müşteri tarafındaki "Faturayı Görüntüle" butonu kaybolur.

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparis-detay/)
- [Numara Serileri](/rehber/siparis/numara-serileri/)
