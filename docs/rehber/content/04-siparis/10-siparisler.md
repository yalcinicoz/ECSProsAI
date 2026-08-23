---
title: Siparişler
route: /orders
group: Sipariş Yönetimi
order: 10
summary: Tüm kanallardan gelen siparişlerin durum sekmeleriyle listelendiği, arandığı ve detayına geçildiği ana ekran.
---

## Ne işe yarar
Siparişler sayfası, mağazanızın (web sitesi, mobil, pazaryeri vb.) tüm satış kanallarından gelen siparişlerin tek listesidir.
Sipariş operasyonunu yürüten herkes günlük işe buradan başlar: bekleyen siparişleri görür, sipariş numarası ya da alıcı adıyla
arar, ödemesi alınmamış siparişleri ayıklar ve bir satıra tıklayarak [Sipariş Detayı](/rehber/siparis/siparis-detay/) sayfasına geçer.
Onaylama, iptal, kargoya verme gibi tüm işlemler detay sayfasındadır; bu ekran yalnız listeleme ve bulma içindir.

## Ekran yerleşimi
![Siparişler listesi — durum sekmeleri, arama/filtre şeridi ve sipariş tablosu](img/orders.webp)
1. **Başlık** — "Siparişler" ve toplam kayıt sayısı (tarih aralığı uygulanmışsa "… kayıt (seçili tarih aralığında)").
2. **Durum sekmeleri** — Aktif / Bekleyen / Onaylı / İşlemde / Kargoda / Teslim / İptal / Tümü; aktif durumların yanında canlı sayaç.
3. **Arama ve filtre şeridi** — sipariş no / alıcı adı arama kutusu + **Ara** butonu, ödeme tahsilat filtresi, başlangıç–bitiş tarihi ve **Temizle**.
4. **Sipariş tablosu** — satıra tıklayınca detay açılır.
5. **Sayfalama** — tablonun altında "← Önceki  1 / N  Sonraki →" (20 kayıt/sayfa).

## Liste ve filtreler

### Durum sekmeleri
| Sekme | Kapsadığı durumlar | Not |
|---|---|---|
| Aktif | Bekleyen + Onaylı + İşlemde + Kargoda | Varsayılan açılış sekmesi. Yanındaki sayı dört durumun toplamıdır. |
| Bekleyen | `pending` | Sayaçlı. Müşteri onayı ya da operatör onayı bekleyen siparişler. |
| Onaylı | `confirmed` | Sayaçlı. Stok rezervasyonu yapılmış, işleme alınmayı bekleyen siparişler. |
| İşlemde | `processing` | Sayaçlı. Toplama/paketleme aşamasındaki siparişler. |
| Kargoda | `shipped` | Sayaçlı. Kargoya verilmiş, teslim edilmemiş siparişler. |
| Teslim | `delivered` | Sayaçsız; açıldığında tarih boşsa **son 30 gün** otomatik uygulanır. |
| İptal | `cancelled` | Sayaçsız; son 30 gün kuralı geçerli. |
| Tümü | tüm durumlar | Sayaçsız; son 30 gün kuralı geçerli. |

Sayaçlar dakikada bir kendiliğinden yenilenir. Sekme değiştirince sayfa 1'e dönülür.

### Arama ve filtreler
| Filtre | Ne yapar |
|---|---|
| Arama kutusu ("Sipariş no veya alıcı adı ara…") | Sipariş numarası ya da teslimat alıcısının adı üzerinde arar. Yazdıkça değil, **Ara** butonuna basınca ya da Enter ile uygulanır. |
| Ödeme: Tümü / Ödemesi Alınan / Ödemesi Alınmayan | Tahsilat durumuna göre süzer. "Alınan" = ödeme durumu **Ödendi**; "Alınmayan" = Ödendi dışındaki her durum (Bekliyor, Ödenmedi, Başarısız, Kısmi). Ödeme yöntemine göre değil, tahsilata göre filtreler. |
| Tarih aralığı (iki tarih kutusu) | Sipariş oluşturulma tarihine göre başlangıç–bitiş süzgeci; bitiş günü dahildir. |
| Temizle | Tarih kutuları doluysa görünür; her iki tarihi siler. |

### Tablo sütunları
| Sütun | Anlamı |
|---|---|
| SİPARİŞ NO | Kanala özel seriden üretilen sipariş numarası (pazaryeri siparişlerinde pazaryerinin numarası). |
| MÜŞTERİ | Teslimat alıcısının adı; yoksa "—". |
| TUTAR | Genel toplam ve para birimi (TRY için ₺). |
| ÖDEME | Üst satır ödeme yöntemi (Kart (Online) / Kapıda Nakit / Kapıda Kart; eski kayıtlarda "—"), alt satır ödeme durumu (Bekliyor, Ödenmedi, Ödendi, Kısmi, İade Edildi, Başarısız). |
| DURUM | Sipariş durumu rozeti (aşağıdaki tablo). |
| TARİH | Oluşturulma tarihi ve saati. |
| (son sütun) | "Detay →" — satırın tamamı tıklanabilir. |

Sıralama sabittir (en yeni üstte). Liste boşsa "Sipariş bulunamadı." yazısı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Ara | Arama kutusunun sağı | Yazılan metni uygular, sayfa 1'e döner. | — |
| Temizle | Tarih kutularının sağı | Tarih filtrelerini kaldırır. | En az bir tarih girilmiş olmalı |
| Satır tıklama | Tablo | `/orders/{id}` detay sayfası açılır. | — |
| ← Önceki / Sonraki → | Tablo altı | Sayfalar arasında gezer. | Birden fazla sayfa varsa |

> **Not:** Bu ekranda yeni sipariş oluşturma, toplu işlem veya kanal filtresi yoktur. Siparişler siteden/mobilden/pazaryerinden
> gelir; kanal bilgisi detay sayfasının üst satırında "Platform:" olarak görünür.

## Durumlar ve iş kuralları
| Rozet | Kod | Anlamı |
|---|---|---|
| Bekleyen (sarı) | `pending` | Sipariş alındı; onay bekliyor. Stok henüz rezerve değil. |
| Onaylı (sarı) | `confirmed` | Onaylandı; seçilen depoda stok rezervasyonu oluştu. |
| İşlemde (sarı) | `processing` | Toplama/paketleme başladı. |
| Kargoda (yeşil) | `shipped` | Gönderi oluştu; rezerve stok gerçekten düştü. |
| Teslim (yeşil) | `delivered` | Müşteriye ulaştı. |
| İptal (kırmızı) | `cancelled` | İptal edildi; rezervasyonlar serbest bırakıldı. |
| İade (kırmızı) | `returned` | İade edilmiş sipariş. |

Geçişler: `pending` → `confirmed` → `processing` → `shipped` → `delivered`; yalnız `pending` ve `confirmed` iptal edilebilir.
Ayrıntı ve her geçişin stok etkisi için [Sipariş Detayı](/rehber/siparis/siparis-detay/) sayfasına bakın.

**Onay politikası:** Siparişin "Bekleyen"de kalıp kalmayacağı kanal bazlı politikayla belirlenir (Ayarlar → Bildirim Şablonları):
kapıda ödemeli siparişlerde "Her zaman onay iste / Onay isteme", kartla ödenenlerde "Yalnız ilk sipariş/misafir / Her zaman / Onay isteme".
Onay istenen siparişte müşteriye SMS/e-posta ile onay bağlantısı gider (link ömrü saat olarak ayarlanır); müşteri bağlantıya tıklayınca
sipariş onaylanır. Politika onay istemiyorsa kartla ödenen sipariş ödeme başarılı olunca kendiliğinden onaylanır.

## Adım adım
**Bir siparişi bulup açma**
1. Sol menüden **Sipariş Yönetimi → Siparişler**'e girin.
2. Arama kutusuna sipariş numarasını ya da alıcı adını yazın, **Ara**'ya basın (veya Enter).
3. Gerekirse durum sekmesini değiştirin (eski siparişler için **Tümü** + tarih aralığı).
4. Satıra tıklayın; detay sayfası açılır.

**Ödemesi alınmamış bekleyen siparişleri görme**
1. **Bekleyen** sekmesini seçin.
2. Ödeme filtresinden **Ödemesi Alınmayan**'ı seçin.
3. Listeyi inceleyin; gerekirse detaydan iptal edin.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Üst çubuktaki komut paletinden (arama) sipariş numarası yazarak da doğrudan detaya gidebilirsiniz; "Bekleyen Siparişler"
> kısayolu favoriler panelinde sayaçla görünür.

> **Dikkat:** Teslim / İptal / Tümü sekmeleri çok büyük listeler olduğundan tarih boşsa otomatik olarak son 30 güne çekilir.
> Daha eskisini görmek için başlangıç tarihini geriye alın.

> **Not:** Sayaçlar görünmüyorsa sunucu sayaç servisini henüz sunmuyordur; liste yine çalışır.

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparis-detay/)
- [İadeler](/rehber/siparis/iadeler/)
- [Faturalar](/rehber/siparis/faturalar/)
- [Numara Serileri](/rehber/siparis/numara-serileri/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
