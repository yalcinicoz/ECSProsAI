---
title: Migration
route: /settings/migration
group: Sistem
order: 80
summary: Eski veritabanındaki katalog verisinin (görsel setleri, özellikler, ürün grupları, ürünler, varyantlar, görseller) yeni sisteme faz faz aktarıldığı, durumunun ve işlem logunun izlendiği veri aktarım ekranı.
---

## Ne işe yarar
Migration ekranı, eski sistemin katalog verisini yeni sisteme aktaran aracı panelden çalıştırmak içindir. Yalnız kurulum/geçiş döneminde, sistem yönetimi tarafından kullanılır. Ekranda mevcut tablo doluluğu, son çalıştırmanın durumu ve canlı işlem logu görülür; bir faz ya da tüm fazlar seçilip başlatılır.

> **Dikkat:** Bu ekran canlı veriyi **siler ve yeniden yazar**. Operasyon kullanıcıları tarafından kullanılmamalıdır; bir aktarım yalnız sistem yönetiminin bilgisi ve yedek alınmış halde başlatılmalıdır.

## Ekran yerleşimi
![Migration ekranı — durum kartı, tablo istatistikleri, faz seçimi ve işlem logu](img/settings-migration.webp)
1. **Başlık** — "Eski Veritabanı Migration" ve kaynak → hedef açıklaması.
2. **Durum kartı** — son çalıştırmanın durumu (Bekleniyor / Çalışıyor / Tamamlandı / Hata), başlangıç saati ve geçen süre ya da hata metni.
3. **Mevcut Tablo Durumu** — hedef tabloların kayıt sayıları ve toplam.
4. **Migration Çalıştır** — faz seçenekleri (radyo), **Migration Başlat** butonu ve onay şeridi.
5. **İşlem Logu** — açılır/kapanır kara kutu; çalışırken otomatik en alta kayar.

## Liste ve filtreler
| Tablo Durumu satırı | Anlamı |
|---|---|
| Image Sets, Attribute Types, Attribute Values, Product Groups, Products, Product Attributes, Product Variants, Variant Attributes, Product Images | Hedef tablodaki mevcut kayıt sayısı; dolu tablolar vurgulu renkte. Üstte toplam. |

Çalışma sürerken ekran 2 saniyede bir kendini yeniler.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Faz seçimi (radyo) | Migration Çalıştır kartı | Çalıştırılacak fazı seçer. | Çalışma sürerken kilitli. |
| Migration Başlat | Kartın altı | Sarı **onay şeridi** açar: seçilen faza göre uyarı metni ("Tüm migration fazları çalıştırılacak. Mevcut veriler silinecek. Emin misiniz?" / "Faz N çalıştırılacak. İlgili tablolar silinip yeniden yazılacak…" / Faz 8-9 için yalnız güncelleme/birleştirme uyarısı). | Çalışma sürerken pasif ("Çalışıyor…"). |
| Evet, Başlat | Onay şeridi | ⚠️ Aktarımı arka planda başlatır; durum kartı "Çalışıyor"a döner. Geri alınamaz. | — |
| İptal | Onay şeridi | Şeridi kapatır, hiçbir şey çalışmaz. | — |
| İşlem Logu (başlık) | Log kartı | Logu açar/kapatır. | Log yalnız bir çıktı oluştuğunda görünür. |

Aynı anda ikinci bir çalıştırma başlatılamaz; deneme "Bir hata oluştu" ya da sunucu hatası olarak döner.

## Form alanları (faz seçenekleri)
| Faz | Açıklama (ekrandaki metin) | Veri etkisi |
|---|---|---|
| Tüm Fazlar | Sıfırdan tam migration (1-7 arası) | ⚠️ Tüm hedef tablolar silinip yeniden yazılır. |
| Faz 1 — Image Sets | Görsel setleri (2 kayıt) | Tablo silinip yazılır. |
| Faz 2 — Attribute Types | Özellik tipleri (43 kayıt) | Tablo silinip yazılır. |
| Faz 3 — Attribute Values | Özellik değerleri + markalar (~4K kayıt) | Tablo silinip yazılır. |
| Faz 4 — Product Groups | Ürün grupları (217 kayıt) | Tablo silinip yazılır. |
| Faz 5 — Products | Ürünler + ürün özellikleri (~117K kayıt) | Tablo silinip yazılır. |
| Faz 6 — Variants | Varyantlar + varyant özellikleri (~1.2M kayıt, ~22 dk) | Tablo silinip yazılır; uzun sürer. |
| Faz 7 — Images | Ürün görselleri (~1.4M kayıt, ~11 dk) | Tablo silinip yazılır; uzun sürer. |
| Faz 8 — Grup Adı Düzelt | Ürün grubu adlarından cinsiyet ön ekini kaldırır (silme yok, yalnız güncelleme, ~1 sn) | Yalnız güncelleme. |
| Faz 9 — Grup Birleştir | Aynı adlı ürün gruplarını tek gruba indirir; ürünleri ana gruba yönlendirir, kopyaları siler (~1 sn) | Kopya gruplar silinir. |

## Durumlar ve iş kuralları
| Durum | Anlamı |
|---|---|
| Bekleniyor | Henüz çalıştırılmadı. |
| Çalışıyor — Faz N/Tümü | Aktarım sürüyor; başlangıç saati ve geçen süre gösterilir. |
| Tamamlandı | Bitti; faz ve toplam süre gösterilir. |
| Hata | Başarısız; hata metni kartta. |

- Aktarım arka planda, panelden bağımsız çalışır; sayfayı kapatsanız da sürer, tekrar açınca durum görünür.
- Tam aktarım (Tüm Fazlar) ve Faz 1-7 mevcut katalog verisini ezer; Faz 8-9 yalnız düzeltme/birleştirme yapar.
- Ekran herhangi bir ek yetki istemez; bu yüzden erişimi kısıtlamak kullanıcı/rol yönetimine kalır.

## Adım adım
### Tek bir fazı yeniden çalıştırma
1. Yedek alındığından ve kimsenin katalogda çalışmadığından emin olun.
2. Sistem › **Migration**'da fazı seçin, **Migration Başlat**'a tıklayın.
3. Onay şeridindeki uyarıyı okuyun; **Evet, Başlat**.
4. Durum kartını ve **İşlem Logu**'nu izleyin; "Tamamlandı" görünce tablo sayılarını kontrol edin.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** "Tüm Fazlar" ve Faz 1-7, hedef tablolardaki mevcut verileri SİLER. Canlı katalogda elle yapılmış değişiklikler kaybolur. Yedek olmadan başlatmayın.

> **Dikkat:** Faz 6 ve 7 dakikalarca sürer; bu sırada ikinci bir çalıştırma başlatılamaz. Durum kartı "Çalışıyor" iken bekleyin.

> **Not:** "Hata" durumunda hata metni kartta, ayrıntı İşlem Logu'nda ("[STDERR]" satırları) görünür; sistem yönetimine iletin.

## İlgili sayfalar
- [Ürün Kartları](/rehber/katalog/urun-kartlari/)
- [Ürün Grupları](/rehber/katalog/urun-gruplari/)
