---
title: Stok Hareketleri (Transferler)
route: /inventory/transfers
group: Stok
order: 30
summary: Depolar arası transfer taleplerinin oluşturulduğu, kalemlerinin eklendiği ve durumunun (taslak → tamamlandı) takip edildiği ekran.
---

## Ne işe yarar
Sol menüde **Stok > Stok Hareketleri** olarak görünen bu ekran, bir depodan diğerine yapılan **transfer taleplerini** yönetir: talep açılır, taslakken ürün kalemleri eklenir, ardından onay → toplama → yolda → teslim → tamamlandı adımlarıyla izlenir. Depo ve mağaza ekipleri ikmal, iade ve iç transferleri buradan takip eder.

## Ekran yerleşimi
![Transferler listesi ve Yeni Transfer Talebi penceresi](img/inventory-transfers.webp)
1. **Başlık satırı** — "Transferler", kayıt sayısı; sağda **Durum** listesi ve **+ Yeni Transfer**.
2. **Tablo** — satıra tıklayınca transfer detayı açılır.
3. **Sayfalama** — sayfa başına 20; ← Önceki / Sonraki →.
4. **Yeni Transfer Talebi penceresi**.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Transfer kodu, `TR-YYYYAAGG-0001` biçiminde otomatik. |
| KAYNAK | Kaynak depo kodu. |
| HEDEF | Hedef depo kodu. |
| TİP | `İç Transfer` / `İkmal` / `İade` / `Düzeltme`. |
| KALEM | Kalem sayısı. |
| DURUM | Durum rozeti (aşağıda). |
| TARİH | Oluşturulma tarihi. |
| Detay → | Satırın tıklanabilir olduğunu belirtir. |

| Filtre | Ne yapar |
|---|---|
| Durum | Tüm Durumlar ya da tek durum (Taslak, Bekliyor, Toplama, Toplandı, Yolda, Teslim, Tamamlandı, İptal). |

Kayıt yoksa "Transfer bulunamadı." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Transfer | Liste başlığı | "Yeni Transfer Talebi" penceresi açılır. | `inventory.manage` |
| Oluştur | Pencere | Talep `Taslak` olarak açılır ve detay sayfasına geçilir. | Kaynak ve hedef depo seçili ve farklı |
| İptal | Pencere | Formu temizleyip kapatır. | — |
| Satır tıklama | Tablo | `/inventory/transfers/:id` detay sayfası. | — |
| ‹ Transferler | Detay başlığı | Listeye döner. | — |
| İŞLEMLER butonları | Detay → bilgi kartı altı | Durumu bir sonraki adıma taşır (tablo aşağıda). ⚠️ **İptal Et** geri alınamaz. | `inventory.manage`; durum uygun |
| Ekle | Detay → Yeni Kalem Ekle | Kaleme ekler, liste yenilenir. | Yalnız `Taslak`; Varyant ID dolu, Miktar ≥ 1 |

## Form alanları
### Yeni Transfer Talebi
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kaynak Depo | Evet | Stoğun çıkacağı depo. |
| Hedef Depo | Evet | Kaynaktan farklı olmalı (kaynak seçilince listeden düşer). |
| Transfer Tipi | Hayır (varsayılan İç Transfer) | İç Transfer / İkmal / İade / Düzeltme. |
| Not | Hayır | Açıklama; detayda bilgi kartında görünür. |

Pencerede hatırlatma: "Transfer oluşturulduktan sonra detay sayfasından ürün kalemleri ekleyebilirsiniz."

### Yeni Kalem Ekle (detay, yalnız Taslak)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Varyant ID | Evet | Varyantın sistem kimliği (UUID biçiminde). Boşsa "Varyant ID gereklidir." |
| Miktar | Evet (varsayılan 1) | En az 1: "Miktar en az 1 olmalıdır." |
| Kaynak Lokasyon | Hayır | Kaynak konum kimliği (UUID). |
| Hedef Lokasyon | Hayır | Hedef konum kimliği (UUID). |

## Detay sayfası
![Transfer detayı — bilgi kartı, işlem butonları ve kalemler](img/inventory-transfers-detay.webp)
*(1) Başlık: Transferler / KOD + durum rozeti · (2) Bilgi kartı: KAYNAK DEPO, HEDEF DEPO, TİP, TARİH, not · (3) İŞLEMLER butonları · (4) Kalemler tablosu · (5) Yeni Kalem Ekle (yalnız Taslak)*

**Kalemler** tablosu:
| Sütun | Anlamı |
|---|---|
| VARYANT ID | Kalemin varyant kimliği. |
| İSTENEN | Talep edilen miktar. |
| TOPLANAN | Toplanan miktar. |
| TESLİM | Teslim edilen miktar. |
| DURUM | Kalem durumu (iç kodla, örn. `pending`). |

Kalem yoksa: Taslak'ta "Henüz kalem eklenmemiş. Aşağıdan ekleyin.", diğer durumlarda "Kalem bulunmuyor."

## Durumlar ve iş kuralları
| Durum | Rozet | Sonraki adımlar (İŞLEMLER) |
|---|---|---|
| `draft` | Taslak | **Onayla** → Bekliyor · **İptal Et** |
| `pending` | Bekliyor | **Toplamaya Başla** → Toplama · **İptal Et** |
| `picking` | Toplama | **Toplama Tamamlandı** → Toplandı · **İptal Et** |
| `picked` | Toplandı | **Kargoya Ver** → Yolda |
| `in_transit` | Yolda | **Teslim Alındı** → Teslim |
| `delivered` | Teslim | **Tamamla** → Tamamlandı |
| `completed` | Tamamlandı | — (son durum) |
| `cancelled` | İptal | — (son durum) |

- Sıra atlanamaz: izin verilmeyen geçişte "'pending' durumundan 'completed' durumuna geçiş yapılamaz." benzeri hata döner.
- Kalem yalnız **Taslak** durumunda eklenir: "Sadece taslak transferlere kalem eklenebilir." Kalem silme/düzenleme yoktur.
- Toplanan/Teslim miktarları bu ekrandan girilmez; yalnız izlenir.
- **Durum geçişleri stok miktarlarını kendiliğinden değiştirmez.** Gerçek stok düşüşü ve girişi için Stok Takibi > **+ Stok Hareketi** ile kaynak depoda **Transfer Çıkış** (eksi miktar), hedef depoda **Transfer Giriş** (artı miktar) kaydedin.
- Kod otomatik verilir (`TR-20260823-0001`), değiştirilemez.
- Yazma işlemleri `inventory.manage` ister; yetkisiz kullanıcıda İŞLEMLER ve kalem ekleme görünmez.

## Adım adım
**Mağazaya ikmal transferi açma**
1. Stok > Stok Hareketleri'nde **+ Yeni Transfer**'e tıklayın.
2. **Kaynak Depo** (merkez) ve **Hedef Depo** (mağaza) seçin; **Transfer Tipi** İkmal; isteğe bağlı not; **Oluştur**.
3. Açılan detayda **Yeni Kalem Ekle** bölümüne varyant kimliğini ve miktarı yazıp **Ekle**'ye tıklayın; tüm kalemler için tekrarlayın.
4. **Onayla**'ya tıklayın (Bekliyor); depo toplamaya başlayınca **Toplamaya Başla**, bitince **Toplama Tamamlandı**, sevkte **Kargoya Ver**, mağaza teslim alınca **Teslim Alındı**, kontrol bitince **Tamamla**.
5. Stok Takibi'nde kaynak depo için Transfer Çıkış, hedef depo için Transfer Giriş hareketlerini kaydedin.

**Transferi iptal etme**
1. Detayda **İptal Et**'e tıklayın (yalnız Taslak, Bekliyor, Toplama durumlarında görünür).

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Kalem eklemek için varyantın sistem kimliği (UUID) gerekir; barkod ya da ürün kodu kabul edilmez. Kimliği ürün kartındaki varyant bilgisinden alın.

> **Dikkat:** Onayladıktan sonra kalem eklenemez; eksik kalem için yeni talep açın.

> **İpucu:** Durum filtresini **Bekliyor** yaparak toplamaya alınmamış talepleri hızlıca görün.

## İlgili sayfalar
- [Depolar](/rehber/stok/depolar/)
- [Stok Takibi](/rehber/stok/stok-takibi/)
