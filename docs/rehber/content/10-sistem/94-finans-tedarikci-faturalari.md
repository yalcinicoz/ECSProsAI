---
title: Tedarikçi Faturaları
route: /finance/supplier-invoices
group: Sistem
order: 94
summary: Tedarikçilerden (cari hesap) gelen alış faturalarının numara, tarih, vade, kalem sayısı, tutar ve ödeme durumuyla listelendiği ekran.
---

## Ne işe yarar
Tedarikçilerinizden aldığınız mal/hizmet faturaları bu ekranda listelenir. Muhasebe ve satın alma sorumlusu hangi
faturaların açık, hangilerinin ödenmiş olduğunu, vadelerini ve tutarlarını buradan izler. Ekranın alt başlığı:
"N kayıt — faturalar tedarikçi teslimat akışından oluşur": fatura kaydı panelden elle girilmez; tedarikçi teslimat
(mal kabul) akışı ve ilgili servisler üzerinden oluşur, bu ekran sonuçları gösterir.

Sol menüde **Sistem > Finans** bağlantısı bu sayfayı açar. Ayrı izin gerekmez.

> **Not:** Tedarikçi, sistemde ayrı bir kayıt türü değil bir **cari hesaptır**. Tedarikçi kartları **Cari > Cari
> Kartlar** ekranında (hesap tipi: tedarikçi) tutulur; bu ekranda ayrıca tedarikçi kartı yönetimi yoktur.

## Ekran yerleşimi
![Tedarikçi Faturaları — durum sekmeleri ve fatura listesi](img/finance-supplier-invoices.webp)
1. **Başlık satırı** — "Tedarikçi Faturaları" ve toplam kayıt sayısı.
2. **Sekmeler** — `Tümü` / `Açık` / `Ödendi`.
3. **Tablo** — faturalar, fatura tarihine göre yeniden eskiye.
4. **Sayfalama** — `← Önceki  1 / N  Sonraki →`; sayfa boyutu 20.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| FATURA NO | Tedarikçinin fatura numarası. |
| TARİH | Fatura tarihi (gün.ay.yıl). |
| VADE | Ödeme vadesi; tanımlı değilse `—`. |
| KALEM | Faturadaki satır (kalem) sayısı. |
| TUTAR | Genel toplam (₺, iki ondalık) — ara toplam − indirim + vergi. |
| DURUM | `Taslak` (gri), `Açık` (mavi), `Kısmi Ödendi` (sarı), `Ödendi` (yeşil), `İptal` (kırmızı). |

| Filtre | Ne yapar |
|---|---|
| Tümü | Tüm faturalar. |
| Açık | Yalnız `Açık` durumundakiler. |
| Ödendi | Yalnız `Ödendi` durumundakiler. |

- Arama, tedarikçi ya da tarih filtresi yoktur; sekme değişince liste 1. sayfaya döner.
- Satıra tıklamak bir şey yapmaz; fatura kalemleri ekranda açılmaz.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Sekme (Tümü / Açık / Ödendi) | Liste üstü | Listeyi duruma göre süzer. | — |
| ← Önceki / Sonraki → | Tablo altı | Sayfalar arasında gezinir. | Birden çok sayfa varsa |

Ekranda oluştur/düzenle/iptal butonu yoktur; fatura kaydı ve durum değişiklikleri teslimat/ödeme akışından gelir.

## Durumlar ve iş kuralları
| Rozet | Anlamı |
|---|---|
| `Taslak` (`draft`) | Fatura kaydı yeni oluşturuldu; yeni faturalar bu durumla açılır. |
| `Açık` (`open`) | Ödeme bekleyen fatura. |
| `Kısmi Ödendi` (`partial`) | Tutarın bir kısmı ödendi. |
| `Ödendi` (`paid`) | Tamamı ödendi. |
| `İptal` (`cancelled`) | Fatura iptal edildi. |

- Fatura tutarı kalemlerden hesaplanır: her kalem için `miktar × birim fiyat`, üzerinden indirim yüzdesi düşülür,
  kalan tutara vergi yüzdesi eklenir; genel toplam bunların toplamıdır. En az bir kalem zorunludur.
- Tedarikçiye yapılan ödemeler ve iadeler cari hesap hareketlerine işlenir; bakiye **Cari > Cari Kartlar**'daki
  hesap detayından izlenir.
- Vade geçmiş ama `Açık` görünen faturalar için ayrı bir uyarı rozeti yoktur; VADE sütununu tarihle karşılaştırın.

## Adım adım
**Ödeme bekleyen faturaları listeleme**
1. **Sistem > Finans** sayfasını açın.
2. `Açık` sekmesine geçin; VADE sütununa göre yaklaşan/gecikmiş faturaları belirleyin.
3. Ödeme kaydı ve bakiye için tedarikçinin cari kartına gidin (**Cari > Cari Kartlar**).

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** `Kısmi Ödendi`, `Taslak` ve `İptal` durumları için ayrı sekme yoktur; bunları `Tümü` sekmesinde DURUM
> rozetinden ayırt edin.

> **Dikkat:** Bu ekran salt okunurdur. Faturanın tutarı ya da vadesi yanlışsa düzeltme teslimat/cari tarafında yapılır;
> buradan değiştirilemez.

> **Not:** Ödenen tutarlar fatura satırında gösterilmez; yalnız durum rozeti değişir.

## İlgili sayfalar
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
- [Entegrasyon Logları](/rehber/sistem/entegrasyon-loglari/)
