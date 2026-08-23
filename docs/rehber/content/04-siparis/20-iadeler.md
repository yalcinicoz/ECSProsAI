---
title: İadeler
route: /orders/returns
group: Sipariş Yönetimi
order: 20
summary: Müşteri iade taleplerinin listelendiği, onaylanıp reddedildiği, teslim alınıp stoğa geri yazıldığı ve geri ödemesinin tamamlandığı ekranlar; iade nedenleri yönetimi.
---

## Ne işe yarar
Müşteriler siteden (Hesabım → İadelerim) kargoya verilmiş ya da teslim edilmiş siparişleri için iade talebi açar. Bu sayfa o
taleplerin operasyon tarafıdır: talep onaylanır veya reddedilir, ürün depoya ulaşınca **teslim alınır** (stok geri yazılır) ve
son adımda **geri ödeme** yapılır. Ayrıca sitedeki iade formunda görünen **iade nedenleri** listesi buradan yönetilir.
Liste `/orders/returns`, tek iadenin detayı `/orders/returns/{id}` adresindedir.

## Ekran yerleşimi
![İadeler listesi — durum sekmeleri ve iade tablosu, sağ üstte İade Nedenleri butonu](img/orders-returns.webp)
1. **Başlık** — "İadeler" + kayıt sayısı; sağda **İade Nedenleri** butonu.
2. **Durum sekmeleri** — Talep Edilen / Onaylı / Teslim Alınan / Geri Ödenen / Reddedilen / Tümü.
3. **İade tablosu** — satıra tıklayınca detay açılır.
4. **Sayfalama** — 20 kayıt/sayfa.

![İade detayı — başlık ve aksiyon butonları, Talep Bilgisi, Kalemler, görseller, Muayene ve Geri Ödemeler kartları](img/orders-returns-detay.webp)
1. **Başlık** — "←", iade numarası, durum rozeti; sağda duruma göre Onayla / Reddet / Teslim Al / Geri Ödeme Yap.
2. **Alt satır** — "Sipariş: {sipariş no}" bağlantısı (sipariş detayına gider) · talep tarihi.
3. **Kartlar** — Talep Bilgisi, Kalemler, Talep Görselleri (varsa), Muayene (varsa), Geri Ödemeler (varsa).

## Liste ve filtreler
| Sekme | Durum |
|---|---|
| Talep Edilen (varsayılan) | `requested` |
| Onaylı | `approved` |
| Teslim Alınan | `received` |
| Geri Ödenen | `refunded` |
| Reddedilen | `rejected` |
| Tümü | hepsi |

| Sütun | Anlamı |
|---|---|
| İADE NO | İade talep numarası. |
| TİP | İade türü ("İade"). |
| TUTAR | Talep edilen geri ödeme tutarı (₺). |
| GERİ ÖDEME | Geri ödeme yöntemi · geri ödeme durumu (ör. `wallet · pending`). |
| DURUM | Durum rozeti (aşağıda). |
| TARİH | Talep tarihi. |
| (son sütun) | "Detay →" — satır tıklanabilir. |

Arama kutusu yoktur; belirli bir siparişin iadelerine sipariş detayındaki **İadeler** kartından ulaşılır.

## Detay sayfası bölümleri
| Kart | Alanlar |
|---|---|
| Talep Bilgisi | Geri Ödeme Tutarı (kalın), Geri Ödeme Yöntemi, Geri Ödeme Durumu, Kargo İade Kodu, İade Kargo Takip, Kargoya Verildi, Depoya Ulaştı, Müşteri Notu (boş alanlar gizlenir). |
| Kalemler (N) | ÜRÜN (ad; stok kodu · varyant) · ADET · NEDEN (iade nedeni; altında müşterinin kalem notu) · TUTAR (kalem geri ödeme tutarı). |
| Talep Görselleri | Müşterinin yüklediği fotoğraflar (tıklayınca yeni sekmede açılır). |
| Muayene | Not ve tamamlanma zamanı — teslim alma sırasında girilen muayene notu. |
| Geri Ödemeler | Yöntem · tutar · durum · işlem zamanı — yapılmış geri ödeme kayıtları. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| İade Nedenleri | Liste başlığı | "İade Nedenleri" penceresi: nedenler listesi (ad · N alt neden · `Pasif` rozeti), **+ Yeni Neden**, satıra tıklayınca düzenleme. | — |
| + Yeni Neden / Düzenle → | İade Nedenleri penceresi | Neden formu (aşağıda). Pasif neden sitedeki formda görünmez; geçmiş talepler etkilenmez. | — |
| Satır tıklama | Liste | İade detayı açılır. | — |
| ← | Detay başlığı | Listeye döner. | — |
| Sipariş bağlantısı | Detay alt satırı | İlgili siparişin detayını açar. | — |
| Onayla | Detay başlığı | Talep `approved` olur (onay penceresi yok, anında). | Durum `requested` |
| Reddet ⚠️ | Detay başlığı (kırmızı) | "İadeyi Reddet" penceresi: Red Nedeni (zorunlu, müşteriye gösterilir). Durum `rejected`; akış biter. | Durum `requested` |
| Teslim Al | Detay başlığı | "İadeyi Teslim Al" penceresi: **Depo** (zorunlu) + Muayene Notu. Durum `received`; **seçilen depoda stok miktarı geri yüklenir**. | Durum `approved` |
| Geri Ödeme Yap | Detay başlığı | "Geri Ödeme Yap" penceresi: Yöntem + Tutar (talep tutarı dolu gelir). Durum `refunded`, geri ödeme durumu tamamlandı; yöntem Cüzdan ise tutar müşterinin cüzdanına yazılır. | Durum `received`; tutar > 0 |

## Form alanları

### İade nedeni
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Neden (ana başlık) | Evet | Ör. "Bedeni olmadı". |
| Alt Nedenler | Hayır | Her satır bir seçenek; sitedeki aramalı listede görünür (ör. "Küçük geldi", "Büyük geldi"). |
| Aktif | — | Kapalıysa formda görünmez. |
| Sıra | Hayır | Listede sıralama (sayı). |

### İadeyi Teslim Al
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Depo | Evet | Stoğun geri yazılacağı depo. |
| Muayene Notu | Hayır | Ürün kontrol notu; detayda "Muayene" kartında görünür. |

### Geri Ödeme Yap
| Alan | Zorunlu | Açıklama |
|---|---|---|
| Yöntem | Evet | Talepteki yöntem varsayılan; seçenekler Cüzdan (`wallet`), Havale/EFT (`bank_transfer`), Nakit (`cash`). |
| Tutar | Evet | Varsayılan talep tutarı; 0'dan büyük olmalı. Altında "Talep tutarı: …" hatırlatması. |

## Durumlar ve iş kuralları
| Rozet | Kod | Anlamı |
|---|---|---|
| Talep Edildi (sarı) | `requested` | Müşteri talebi açtı; karar bekliyor. |
| Onaylandı (sarı) | `approved` | Kabul edildi; ürünün depoya gelmesi bekleniyor. |
| Teslim Alındı (yeşil) | `received` | Ürün depoda; stok geri yazıldı; geri ödeme bekliyor. |
| Geri Ödendi (yeşil) | `refunded` | Geri ödeme tamamlandı; akış kapandı. |
| Reddedildi (kırmızı) | `rejected` | Talep reddedildi; akış kapandı. |

Akış: `requested` → `approved` → `received` → `refunded`; `requested` → `rejected`. Her adım yalnız bir önceki durumdan yapılabilir
("'approved' durumundaki iade onaylanamaz." gibi hatalar yanlış sıradan gelir).
- İade talebi yalnız **kargoya verilmiş veya teslim edilmiş** siparişler için açılabilir ve en az bir kalem içermelidir.
- Stok etkisi **yalnız Teslim Al** adımındadır (onay stok değiştirmez).
- Tutarlar kalem bazındadır ve kampanya/kupon indirimi düşülmüş gerçek ödenen fiyattan gelir; geri ödeme tutarı son adımda elle düzeltilebilir.
- Geri ödeme yöntemi Cüzdan ise müşterinin cüzdan bakiyesine hareket yazılır; yazılamazsa işlem başarısız olur ve hata görünür.

## Adım adım
**Bir iade talebini sonuçlandırma**
1. **İadeler → Talep Edilen** sekmesinde talebi açın; kalemleri, nedenleri ve görselleri inceleyin.
2. **Onayla** (ya da **Reddet** + neden).
3. Ürün depoya ulaşınca **Teslim Al** → depo seçin, muayene notu yazın → **Teslim Al**. (Stok geri yazılır.)
4. **Geri Ödeme Yap** → yöntem ve tutarı kontrol edin → **Geri Ödeme Yap**. (Durum: Geri Ödendi.)

**İade nedeni ekleme**
1. Liste başlığındaki **İade Nedenleri**'ne tıklayın → **+ Yeni Neden**.
2. Ana başlığı ve her satıra bir alt nedeni yazın, Aktif bırakın → **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Reddetme ve geri ödeme geri alınamaz; red nedeni müşteriye gösterilir.

> **İpucu:** Müşteri notu ve fotoğraflar karar vermeyi kolaylaştırır; kalem nedeninin altındaki küçük yazı müşterinin o kaleme yazdığı açıklamadır.

> **Not:** Reddedilen bir talep için müşteri yeniden talep açabilir; pasife alınan nedenler eski taleplerde görünmeye devam eder.

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparis-detay/)
- [Siparişler](/rehber/siparis/siparisler/)
