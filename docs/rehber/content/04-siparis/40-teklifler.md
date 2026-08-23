---
title: Teklifler
route: /orders/quotes
group: Sipariş Yönetimi
order: 40
summary: Müşterilere hazırlanan fiyat tekliflerinin listelendiği ve taslak → gönderildi → kabul/red → siparişe dönüştü akışının takip edildiği ekran.
---

## Ne işe yarar
Teklifler sayfası, müşteriye verilen fiyat tekliflerinin (özellikle toptan/kurumsal görüşmeler) durumunu izlemek içindir.
Taslak teklif müşteriye gönderilir, müşterinin yanıtı kabul ya da red olarak işaretlenir; kabul edilen teklif sipariş oluşturma
akışıyla siparişe dönüştürülür. Satış/müşteri ilişkileri personeli kullanır.

## Ekran yerleşimi
![Teklifler listesi — durum sekmeleri ve teklif tablosu, satır sonunda aksiyon bağlantıları](img/orders-quotes.webp)
1. **Başlık** — "Teklifler" + kayıt sayısı.
2. **Durum sekmeleri** — Tümü / Taslak / Gönderildi / Kabul / Dönüştü.
3. **Hata satırı** — bir aksiyon başarısız olursa kırmızı mesaj burada görünür.
4. **Teklif tablosu** — son sütunda duruma göre aksiyon bağlantıları.
5. **Sayfalama** — 20 kayıt/sayfa.

## Liste ve filtreler
| Sekme | Durum |
|---|---|
| Tümü (varsayılan) | hepsi |
| Taslak | `draft` |
| Gönderildi | `sent` |
| Kabul | `accepted` |
| Dönüştü | `converted` |

| Sütun | Anlamı |
|---|---|
| TEKLİF NO | Teklif numarası. |
| TUTAR | Teklif genel toplamı ve para birimi (TRY için ₺). |
| GEÇERLİLİK | Teklifin son geçerlilik tarihi. |
| GÖNDERİM | Müşteriye gönderildiği tarih/saat (gönderilmediyse boş). |
| OLUŞTURMA | Oluşturulma tarihi. |
| DURUM | Durum rozeti (aşağıda). |
| (son sütun) | Duruma göre: **Gönder** · **Kabul** / **Red** · "Dönüştürülmeye hazır" yazısı. |

Arama kutusu yoktur; satır tıklaması detay açmaz (detay sayfası bulunmaz). Liste boşsa "Teklif yok." görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Gönder | Satır sonu | Tarayıcı onayı ("… müşteriye gönderilsin mi?") sonrası teklif `sent` olur, gönderim zamanı yazılır. | Durum `draft` |
| Kabul | Satır sonu (yeşil) | Onay sonrası teklif `accepted` olarak işaretlenir. | Durum `sent`; geçerlilik süresi dolmamış |
| Red | Satır sonu (kırmızı) | Onay sonrası teklif `rejected` olur. | Durum `sent`; geçerlilik süresi dolmamış |
| "Dönüştürülmeye hazır" | Satır sonu | Bilgi yazısı — siparişe dönüştürme teslimat bilgisi gerektirir ve sipariş oluşturma akışından yapılır; bu ekranda buton yoktur. | Durum `accepted` |

> **Not:** Bu ekranda yeni teklif oluşturma formu yoktur; teklifler sistemde başka akışlardan (ör. müşteri temsilcisi araçları/API) oluşturulur
> ve burada yönetilir.

## Durumlar ve iş kuralları
| Rozet | Kod | Anlamı |
|---|---|---|
| Taslak (gri) | `draft` | Hazırlandı, henüz gönderilmedi. |
| Gönderildi (mavi) | `sent` | Müşteriye iletildi; yanıt bekleniyor. |
| Kabul Edildi (yeşil) | `accepted` | Müşteri kabul etti; siparişe dönüştürülebilir. |
| Reddedildi (kırmızı) | `rejected` | Müşteri reddetti; akış biter. |
| Siparişe Dönüştü | `converted` | Tekliften sipariş oluşturuldu (yeni sipariş `pending` durumunda açılır). |
| Süresi Doldu (sarı) | `expired` | Geçerlilik tarihi geçmiş teklif. |

Akış: `draft` → `sent` → `accepted` / `rejected`; `accepted` → `converted`.
- Yalnız taslak gönderilebilir ("'sent' durumundaki teklif gönderilemez.").
- Yalnız gönderilmiş teklife yanıt verilir; geçerlilik tarihi geçtiyse "Teklifin geçerlilik süresi dolmuş." hatası alınır.
- Yalnız kabul edilmiş teklif siparişe dönüştürülür; aynı teklif ikinci kez dönüştürülemez.

## Adım adım
**Teklifi müşteriye gönderip yanıtı işlemek**
1. **Taslak** sekmesinde teklifi bulun → satır sonunda **Gönder** → tarayıcı onayını kabul edin.
2. Müşteri yanıt verince **Gönderildi** sekmesinde **Kabul** ya da **Red**'e tıklayın.
3. Kabul edilen teklif **Kabul** sekmesinde "Dönüştürülmeye hazır" olarak görünür; sipariş oluşturma akışından teslimat bilgisiyle siparişe dönüştürün.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Gönder / Kabul / Red işlemleri tek tarayıcı onayıyla uygulanır ve geri alınamaz.

> **İpucu:** Geçerlilik tarihi yaklaşan tekliflerde müşteriyi önceden arayın; süre dolduktan sonra yanıt işlenemez.

## İlgili sayfalar
- [Siparişler](/rehber/siparis/siparisler/)
- [Sipariş Detayı](/rehber/siparis/siparis-detay/)
