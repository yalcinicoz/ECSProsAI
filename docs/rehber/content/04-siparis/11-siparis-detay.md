---
title: Sipariş Detayı
route: /orders/:id
group: Sipariş Yönetimi
order: 11
summary: Tek siparişin kalemleri, ödemeleri, adresleri, paketleri, kargo, fatura, iade ve geçmişinin görüldüğü; onaylama, iptal, işleme alma, kargoya verme, teslim ve fatura oluşturma işlemlerinin yapıldığı sayfa.
---

## Ne işe yarar
Sipariş Detayı, bir siparişle ilgili her şeyin tek yerde toplandığı sayfadır. Operatör siparişi burada onaylar (stok rezerve eder),
işleme alır, paketler, kargoya verir, teslim edildi işaretler ya da iptal eder; fatura keser, paketlerin numara/kargo kodlarını yönetir
ve müşterinin notları, kabul ettiği sözleşmeler, ödeme kayıtları ile operasyon geçmişini görür. Siparişin iadeleri ve faturaları da
ilgili sayfalara bağlantılarla burada listelenir.

## Ekran yerleşimi
![Sipariş detayı — başlık/aksiyon butonları, sol sütunda kalemler ve bölümler, sağda özet](img/orders-detay.webp)
1. **Başlık satırı** — "←" geri oku, sipariş numarası, durum rozeti; sağda duruma göre aksiyon butonları (Onayla, İşleme Al, Kargoya Ver, Teslim Edildi, İptal Et).
2. **Üst bilgi satırı** — alıcı adı · oluşturulma tarihi · Ödeme: yöntem · durum · Platform: kanal adı · (perakende dışıysa) Tip.
3. **Sol sütun (geniş)** — sırasıyla Kalemler, Ödemeler, Adresler, Paketler, Kargo, Faturalar, İadeler (varsa), Notlar ve Sözleşmeler, Durum Geçmişi, Operasyon Geçmişi kartları.
4. **Sağ sütun** — Özet (tutarlar) ve Müşteri kartları.
5. **Aksiyon pencereleri** — butonlara basınca açılan onay diyalogları (aşağıda).

![Sipariş detayı — Paketler bölümü: paket satırları, Kargo Kodu / Yeni No / Geçmiş düğmeleri ve birleştirme seçimi](img/orders-detay--paketler.webp)

## Bölümler ve alanlar

### Başlık ve üst bilgi
| Alan | Anlamı |
|---|---|
| Sipariş numarası | Kanal serisinden üretilen numara (pazaryerinde pazaryerinin numarası). |
| Durum rozeti | Bekleyen / Onaylı / İşlemde / Kargoda / Teslim / İptal / İade. |
| Alıcı · tarih | Teslimat alıcısı ve siparişin oluşturulma zamanı. |
| Ödeme | Yöntem (Kart (Online) / Kapıda Nakit / Kapıda Kart) ve durum (Bekliyor, Ödenmedi, Ödendi, Kısmi, İade Edildi, Başarısız). |
| Platform | Siparişin geldiği satış kanalı. |
| Tip | Yalnız perakende dışı siparişlerde görünür (ör. toptan). |

### Kalemler (N)
| Sütun | Anlamı |
|---|---|
| ÜRÜN | Ürün adı; altında stok kodu · varyant bilgisi (renk/beden). |
| ADET | Sipariş miktarı. |
| BİRİM | Birim fiyat. |
| İND. | Kaleme düşen indirim (kampanya/kupon indirimi kalemlere dağıtılır); yoksa "—". |
| TUTAR | Kalem toplamı (indirim düşülmüş). |

### Ödemeler
Her satır: ödeme yöntemi adı · tutar · ödeme durumu. Kayıt yoksa "Ödeme kaydı yok." yazar.

### Adresler
| Bölüm | Alanlar |
|---|---|
| TESLİMAT | Alıcı, Telefon, Adres, Bölge (mahalle / ilçe / il), Posta Kodu, Teslimat Notu. |
| FATURA | Teslimatla aynıysa "Teslimat adresiyle aynı."; değilse Ad, Firma, Vergi D., Vergi No, Adres, Bölge. |

### Paketler (N)
Sipariş tedarikçiye göre paketlere bölünür; **normal akış paket başına ayrı fatura ve ayrı kargodur.** Paket yoksa bu kural açıklaması görünür.

| Alan / rozet | Anlamı |
|---|---|
| Paket numarası + #sıra | Kanala özel paket serisinden üretilen numara ve sipariş içi sıra. |
| Durum rozeti | `Paketlendi` (mavi) · `Birleştirildi` (gri — kapatılmış paket, listede sayılmaz) · `Kargoda` (yeşil). |
| `Etiket basıldı` (sarı) | Kargo etiketi basılmış; paket kilitlidir (kod değiştirilemez). |
| Kargo kodu: … (üretildi / dış kod) | Taşıyıcı entegrasyon kodu ve kaynağı. |
| N kalem · kg · desi | Paket içeriği özeti. |
| Onay kutusu | Yalnız `order.packages.merge` yetkisi olan kullanıcıda, kilitsiz Paketlendi durumundaki paketlerde görünür; birleştirme için seçim. |

### Kargo
Gönderi satırları: gönderi numarası · kargo anlaşması adı · takip numarası (takip adresi varsa tıklanabilir) · paket sayısı · tarih; altında
taşıyıcı olayları (tarih — açıklama (yer)). Gönderi yoksa "Henüz gönderi yok."

### Faturalar (N)
Fatura numarası · tip (e-Arşiv / e-Fatura / İhracat) · tutar · durum rozeti (Oluşturuldu / İptal) · "PDF ✓" (entegratör PDF'i kayıtlıysa) · "Faturalarda aç →" bağlantısı.
Altında **+ Fatura Oluştur** butonu. Fatura yoksa "Bu sipariş için fatura kesilmedi."

### İadeler (N)
Yalnız iade varsa görünür: iade numarası · tutar · durum rozeti · "Detay →" (satır [İade Detayı](/rehber/siparis/iadeler/) sayfasına gider).

### Notlar ve Sözleşmeler
| Alan | Anlamı |
|---|---|
| Müşteri Notu | Müşterinin ödeme adımında yazdığı not. |
| İç Not | Personel notu; iptal nedeni de "[İptal] …" biçiminde buraya eklenir. |
| KABUL EDİLEN SÖZLEŞMELER | Sözleşme adı — kabul zamanı (metin sürümü tarihi). Sözleşme metni kabulden sonra güncellendiyse "⚠ metin bu kabulden sonra güncellendi" rozeti çıkar. |

### Durum Geçmişi
Zaman damgalarından türetilen satırlar: Sipariş oluşturuldu · Onaylandı · Kargoya verildi (gönderi no) · Teslim edildi.

### Operasyon Geçmişi
Depo operasyonunun adımları (tarih · cümle · personel): Toplama görevine eklendi (plan no), Kalem toplama için atandı → kişi, Kalem toplandı — stok kodu (raf …),
Kalem eksik işaretlendi, Kalem rafa iade edildi, Toplama görevi başlatıldı/tamamlandı. Kayıt yoksa "Operasyon kaydı yok."

### Özet (sağ sütun)
Ara Toplam · İndirim (varsa, eksi) · Masraf (varsa, ör. kapıda ödeme bedeli) · Vergi (varsa) · **TOPLAM**.

### Müşteri (sağ sütun)
Alıcı · Üye Id (üye değilse `Misafir` rozeti) · Kargo Tercihi (müşterinin teslimat adımında seçtiği kargo).

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| ← | Başlık | Siparişler listesine döner. | — |
| Onayla | Başlık (sağ) | "Siparişi Onayla" penceresi: **Depo** seçilir (zorunlu). Onay, seçilen depoda stok **rezervasyonu** oluşturur; durum `confirmed`. | Durum `pending` |
| İşleme Al | Başlık | "İşleme Al" penceresi; sipariş `processing` olur, toplama/paketleme başlatılabilir. | Durum `confirmed` |
| Kargoya Ver | Başlık | "Kargoya Ver" penceresi: Kargo Anlaşması (müşterinin tercihi varsayılan gelir, bağlayıcı değil), Takip Numarası, Paket Sayısı (en az 1). Gönderi kaydı oluşur; durum `shipped`; **rezerve stok gerçekten düşer**. | Durum `processing` |
| Teslim Edildi | Başlık | Onay penceresi; durum `delivered`, açık gönderiler teslim edildi işaretlenir. | Durum `shipped` |
| İptal Et ⚠️ | Başlık (kırmızı) | "Siparişi İptal Et" penceresi: İptal Nedeni (isteğe bağlı). Rezervasyonlar serbest bırakılır, neden İç Not'a yazılır. **Geri alınamaz.** | Durum `pending` veya `confirmed` |
| Tedarikçiye Göre Paketle | Paketler kartı | Kalemleri tedarikçi başına bir pakete böler (tedarikçisiz kalemler tek pakette). Her pakete seriden numara verilir. | Paket yokken; durum `confirmed` veya `processing` |
| Geçmiş | Paket satırı | O paketin kod değişiklik geçmişini açar/kapatır (tarih — Birleştirme / Yeniden numaralandırma / Kargo kodu değişimi · eski no · eski kargo kodu · gerekçe). | — |
| Kargo Kodu | Paket satırı | "Kargo Kodu" penceresi: Kargo Anlaşması seçilir → kod taşıyıcı kuralına göre üretilir (serbest / kurallı / tahsisli aralık) **veya** Dış Kod yazılır (pazaryeri/taşıyıcının verdiği kod aynen). Mevcut kod değişecekse Gerekçe alanı çıkar. | Paket `Paketlendi`, kilitsiz (gönderiye bağlı değil, etiket basılmamış) |
| Yeni No | Paket satırı | "Yeni Numara" penceresi: Gerekçe (zorunlu). Pakete seriden yeni numara verilir; eski numara geçmişe yazılır ve bir daha kullanılmaz; bağlı kargo kodu temizlenir. | Paket `Paketlendi`, kilitsiz |
| Seçilenleri Birleştir (N) ⚠️ | Paketler kartı başlığı | "Paketleri Birleştir — İstisna İşlemi": seçilen paketler kapatılır (`Birleştirildi`), kalemler yeni tek pakete taşınır, eski numaralar geçmişe yazılır ve geri kullanılmaz. Gerekçe zorunlu; kırmızı "Birleştirmeyi Onayla" ile tamamlanır. | ≥2 paket seçili, aynı sipariş; yetki `order.packages.merge` |
| + Fatura Oluştur | Faturalar kartı | "Fatura Oluştur" penceresi (aşağıdaki form). Fatura numarası seriden otomatik üretilir; tutarlar siparişten alınır. | En az bir aktif fatura serisi |
| Faturalarda aç → | Fatura satırı | Faturalar sayfasına gider (PDF adresi girme / iptal orada). | — |
| Detay → | İade satırı | İade detayına gider. | — |
| Takip numarası bağlantısı | Kargo kartı | Taşıyıcının takip sayfasını yeni sekmede açar. | Takip adresi varsa |

> **Not:** İade talebi bu sayfadan açılmaz; iadeler müşteri tarafından siteden (Hesabım → İadelerim) başlatılır ve
> [İadeler](/rehber/siparis/iadeler/) sayfasında yönetilir. İade yalnız kargoya verilmiş veya teslim edilmiş siparişler için açılabilir.

## Form alanları

### Siparişi Onayla
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Depo | Evet | Rezervasyonun yapılacağı depo. Seçilmeden Onayla butonu pasiftir. |

### Siparişi İptal Et
| Alan | Zorunlu | Açıklama |
|---|---|---|
| İptal Nedeni | Hayır | İç Not'a "[İptal] …" olarak yazılır. |

### Kargoya Ver
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Kargo Anlaşması | Hayır | Firmanın aktif kargo entegrasyonları; yoksa "Ayarlar → Firmalar → Entegrasyonlar'dan eklenebilir" uyarısı. Müşteri tercihi varsayılan gelir. |
| Takip Numarası | Hayır | Kargo firmasının takip numarası. |
| Paket Sayısı | Evet | En az 1 (varsayılan 1). |

### Fatura Oluştur
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Fatura Serisi | Evet | Aktif seriler; yoksa "Faturalar sayfasındaki Fatura Serileri'nden tanımlanır" uyarısı. |
| Tip | Evet | e-Arşiv (varsayılan) / e-Fatura / İhracat. |
| Fatura Tarihi | Evet | Varsayılan bugün. |
| Alıcı Adı | Evet | Ayrı fatura adresi varsa oradan, yoksa teslimat alıcısından dolu gelir. |
| Firma Adı | Hayır | Fatura adresindeki firma. |
| Adres | Evet | Fatura/teslimat adresinden dolu gelir. |
| Vergi Dairesi | Hayır | — |
| Vergi/TC No | Hayır | — |

Fatura numarası: `seri kodu + yıl + 9 haneli sıra` (ör. `MSH2026000000001`). Tutar siparişin genel toplamıdır.

### Paket pencereleri
| Pencere | Alan | Zorunlu | Açıklama |
|---|---|---|---|
| Yeni Numara | Gerekçe | Evet | Ör. "Paket içeriği değişti". |
| Paketleri Birleştir | Gerekçe | Evet | Ör. "Müşteri tek kargo talep etti". |
| Kargo Kodu | Kargo Anlaşması **veya** Dış Kod | Biri | Dış kod yazılınca anlaşma seçimi kapanır. |
| Kargo Kodu | Gerekçe | Mevcut kod varsa | Kod değişimi geçmişe gerekçesiyle yazılır. |

## Durumlar ve iş kuralları
| Geçiş | Buton | Otomatik etki |
|---|---|---|
| `pending` → `confirmed` | Onayla (depo seçimiyle) ya da müşterinin onay bağlantısı / kart ödemesinde otomatik onay | Seçilen depoda **stok rezervasyonu** oluşur. |
| `confirmed` → `processing` | İşleme Al (toplama görevi oluşturulunca da otomatik) | Toplama/paketleme başlar. |
| `processing` → `shipped` | Kargoya Ver | Gönderi kaydı; rezervasyonlar "toplandı" olur, **stok miktarı gerçekten düşer**. |
| `shipped` → `delivered` | Teslim Edildi | Gönderiler teslim; (pazaryeri satıcı hakedişleri bu adımdan üretilir). |
| `pending` / `confirmed` → `cancelled` | İptal Et | Rezervasyonlar serbest bırakılır. Sonraki durumlarda iptal edilemez ("'…' durumundaki sipariş iptal edilemez."). |

Diğer kurallar:
- Yanlış durumda buton görünmez; yine de istek giderse sunucu "'İşlemde' durumundaki sipariş onaylanamaz." gibi bir hata döner.
- Paketleme yalnız `confirmed`/`processing` siparişte ve paket yokken yapılır ("Bu sipariş zaten paketlenmiş…").
- Sipariş no, paket no ve kargo kodu **havuza geri dönmez**: iptal/yenileme eski değeri yakar.
- Etiket basılmış ya da gönderiye bağlı paketin numarası/kargo kodu değiştirilemez, birleştirilemez.
- Kampanya/kupon indirimi kalemlere ağırlıklı dağıtılır; iade tutarı kalemin gerçek ödenen fiyatından hesaplanır.
- Onay politikası (kapıda/kart, link ömrü) kanal bazlıdır ve Ayarlar → Bildirim Şablonları'ndan yönetilir; politika onay istiyorsa
  kartla ödenmiş sipariş bile `pending`de kalır, müşteriye onay bağlantısı gider.

## Adım adım
**Standart sipariş akışı**
1. Bekleyen siparişi açın, **Onayla**'ya basın, depoyu seçin, **Onayla**. (Durum: Onaylı; stok rezerve.)
2. **İşleme Al** → **İşleme Al**. (Durum: İşlemde.)
3. Paketler kartında **Tedarikçiye Göre Paketle**; gerekirse her pakete **Kargo Kodu** atayın.
4. Faturalar kartında **+ Fatura Oluştur**; seri/tip/alıcı bilgilerini kontrol edip **Fatura Oluştur**.
5. **Kargoya Ver** → kargo anlaşması, takip no, paket sayısı → **Kargoya Ver**. (Durum: Kargoda; stok düşer.)
6. Teslimat gerçekleşince **Teslim Edildi**.

**Sipariş iptali**
1. Durumun Bekleyen ya da Onaylı olduğunu doğrulayın.
2. **İptal Et** → nedeni yazın (isteğe bağlı) → kırmızı **İptal Et**.
3. Durum İptal olur; neden İç Not'ta görünür.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** İptal ve paket birleştirme geri alınamaz. Birleştirme bilinçli bir istisnadır; normal akış paket başına fatura ve kargodur.

> **İpucu:** Kargoya Ver penceresinde kargo listesi boşsa firmanın kargo entegrasyonu tanımlı değildir (Ayarlar → Firmalar → Entegrasyonlar).
> Fatura penceresinde seri listesi boşsa Faturalar sayfasından **Fatura Serileri** tanımlayın.

> **Not:** "Kargo Tercihi" müşterinin teslimat adımında seçtiği kargodur; Kargoya Ver penceresinde varsayılan gelir ama değiştirilebilir.

> **Not:** Bölge adları, operasyon geçmişi ve sözleşme sürüm uyarısı ilgili servisler sunucuda yoksa sessizce gizlenir; sayfa yine açılır.

## İlgili sayfalar
- [Siparişler](/rehber/siparis/siparisler/)
- [İadeler](/rehber/siparis/iadeler/)
- [Faturalar](/rehber/siparis/faturalar/)
- [Numara Serileri](/rehber/siparis/numara-serileri/)
- [Kargo Bölgeleri](/rehber/siparis/kargo-bolgeleri/)
