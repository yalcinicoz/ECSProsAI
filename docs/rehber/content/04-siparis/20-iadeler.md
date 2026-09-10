---
title: İadeler
route: /orders/returns
group: Sipariş Yönetimi
order: 20
summary: İki iade tipinin (Müşteri İadesi, Teslimatsız İade) listelendiği, onaylanıp reddedildiği, teslim alınıp stoğa geri yazıldığı ve geri ödemesinin tahsilat kuralına göre tamamlandığı ekranlar; iade nedenleri yönetimi.
---

## Ne işe yarar
İki iade tipi vardır:
- **Müşteri İadesi** — müşteri, **teslim edilmiş** siparişi için siteden (Hesabım → İadelerim) ya da personel panelden talep açar.
  Sipariş durumu değişmez (sipariş "Teslim" kalır).
- **Teslimatsız İade** — paket müşteriye ulaştırılamadı / müşteri kabul etmedi **ya da** faturası kesildi ama hiç kargoya verilmedi.
  Personel sipariş detayından **Teslimatsız İade** ile başlatır; sipariş durumu "Teslimatsız İade" olur, fatura iptal edilir ve
  iade kaydı doğrudan **onaylı** açılır (talep/onay adımı yoktur).

Bu sayfa her iki tipin operasyon tarafıdır: talep onaylanır veya reddedilir, ürün depoya ulaşınca **teslim alınır** (stok geri yazılır)
ve son adımda **geri ödeme** yapılır — yalnız müşteriden **tahsilat yapıldıysa**. Ayrıca sitedeki iade formunda görünen **iade nedenleri**
listesi buradan yönetilir. Liste `/orders/returns`, tek iadenin detayı `/orders/returns/{id}` adresindedir.

## Ekran yerleşimi
![İadeler listesi — durum sekmeleri ve iade tablosu, sağ üstte İade Nedenleri butonu](img/orders-returns.webp)
1. **Başlık** — "İadeler" + kayıt sayısı; sağda **İade Nedenleri** butonu.
2. **Durum sekmeleri** — Talep Edilen / Onaylı / Teslim Alınan / Geri Ödenen / Reddedilen / Tümü.
3. **İade tablosu** — satıra tıklayınca detay açılır.
4. **Sayfalama** — 20 kayıt/sayfa.

![İade detayı — başlık ve aksiyon butonları, Talep Bilgisi, Kalemler, görseller, Muayene ve Geri Ödemeler kartları](img/orders-returns-detay.webp)
1. **Başlık** — "←", iade numarası, durum rozeti, **tip rozeti** (Müşteri İadesi / Teslimatsız İade); sağda duruma göre Onayla / Reddet / Teslim Al / Geri Ödeme Yap.
2. **Alt satır** — "Sipariş: {sipariş no}" bağlantısı (sipariş detayına gider) · talep tarihi.
3. **Kartlar** — Geri Ödeme (uygunluk/üst sınır ya da "Geri ödeme yok" + nedeni), Talep Bilgisi, Kalemler, Talep Görselleri (varsa), Muayene (varsa), Geri Ödemeler (varsa).

## Liste ve filtreler
| Sekme | Durum |
|---|---|
| Talep Edilen (varsayılan) | `requested` |
| Onaylı | `approved` |
| Teslim Alınan | `received` |
| Ödenecek | `received` + geri ödeme `pending` — teslim alınmış, parası henüz ödenmemiş iadeler (eski "Müşteriye Ödemeler" ödenmemiş listesi). |
| Geri Ödenen | `refunded` |
| Kapanan | `closed` (geri ödeme yok, teslim alma ile kapandı) |
| Reddedilen | `rejected` |
| Tümü | hepsi |

| Sütun | Anlamı |
|---|---|
| İADE NO | İade talep numarası. |
| TİP | Müşteri İadesi / Teslimatsız İade (sütun filtresi tipten). |
| TUTAR | Geri ödeme tutarı (₺); geri ödeme yoksa 0. |
| GERİ ÖDEME | Geri ödeme yöntemi · geri ödeme durumu (Bekliyor / Tamamlandı / **Geri ödeme yok**). |
| DURUM | Durum rozeti (aşağıda). |
| TARİH | Talep tarihi. |
| (son sütun) | "Detay →" — satır tıklanabilir. |

Arama kutusu yoktur; belirli bir siparişin iadelerine sipariş detayındaki **İadeler** kartından ulaşılır.

## Detay sayfası bölümleri
| Kart | Alanlar |
|---|---|
| Geri Ödeme | Geri ödeme varsa: tutar, yöntem, durum ve **Ödenebilir Üst Sınır** (tahsil edilenden daha önce iade edilenler düşülmüş kalan). Yoksa: "Geri ödeme yok" rozeti + neden (kapıda ödeme — tahsilat yapılmadı / pazaryeri siparişi / tahsilat yok / tamamı zaten iade edildi). |
| Talep Bilgisi | Kargo İade Kodu, İade Kargo Takip, Kargoya Verildi, Depoya Ulaştı, Müşteri Notu (boş alanlar gizlenir). |
| Kalemler (N) | ÜRÜN (ad; stok kodu · varyant) · ADET · NEDEN (iade nedeni; altında müşterinin kalem notu) · TUTAR (kalem geri ödeme tutarı). |
| Talep Görselleri | Müşterinin yüklediği fotoğraflar (tıklayınca yeni sekmede açılır). |
| Muayene | Not ve tamamlanma zamanı — teslim alma sırasında girilen muayene notu. |
| Geri Ödemeler | Yöntem · tutar · durum · işlem zamanı — yapılmış geri ödeme kayıtları. |
| Geri Ödeme › IBAN / Hesap Sahibi | Havale ile iadede müşterinin banka bilgisi; havale seçiliyken boşsa kırmızı "girilmedi" uyarısı. |

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
| Teslim Al | Detay başlığı | "İadeyi Teslim Al" penceresi: **Depo** (zorunlu) + Muayene Notu. Durum `received`; **seçilen depoda stok miktarı geri yüklenir**. Geri ödeme yoksa iade doğrudan `closed` olur. Kargoya verilmemiş teslimatsız iadede stok hiç çıkmamıştı → stok girişi yapılmaz (rezervasyon iade anında serbest bırakılmıştı). | Durum `approved` |
| Geri Ödeme Yap | Detay başlığı | "Geri Ödeme Yap" penceresi: Yöntem + Tutar (üst sınırla kırpılmış talep tutarı dolu gelir; **üst sınır aşılamaz**). Durum `refunded`, geri ödeme durumu tamamlandı; yöntem Cüzdan ise tutar müşterinin cüzdanına yazılır. | Durum `received` ve geri ödeme uygun; tutar > 0 ve ≤ üst sınır |

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
| Yöntem | Evet | Talepteki yöntem varsayılan (siparişin ödeme yöntemine göre: kart → Karta iade, kapıda → Havale/EFT); seçenekler Karta iade, Cüzdan, Havale/EFT, Nakit. |
| IBAN / Hesap Sahibi | Havale/EFT'de IBAN evet | Yalnız Havale/EFT seçiliyken görünür; IBAN en az 15 karakter. **Banka bilgisini kaydet (ödemesiz)** ile ödeme yapmadan iade kaydına yazılır (müşteri IBAN'ı sonradan bildirdiğinde). |
| Tutar | Evet | Varsayılan talep tutarı; 0'dan büyük ve **üst sınırı** (tahsil edilen − daha önce iade edilen) aşmamalı. Sunucu da aynı sınırı uygular. |

## Durumlar ve iş kuralları
| Rozet | Kod | Anlamı |
|---|---|---|
| Talep Edildi (sarı) | `requested` | Müşteri talebi açtı; karar bekliyor. |
| Onaylandı (sarı) | `approved` | Kabul edildi; ürünün depoya gelmesi bekleniyor. |
| Teslim Alındı (yeşil) | `received` | Ürün depoda; stok geri yazıldı; geri ödeme bekliyor. |
| Geri Ödendi (yeşil) | `refunded` | Geri ödeme tamamlandı; akış kapandı. |
| Tamamlandı (geri ödeme yok) (gri) | `closed` | Ürün teslim alındı; tahsilat olmadığı / pazaryeri olduğu için para iadesi yapılmadı; akış kapandı. |
| Reddedildi (kırmızı) | `rejected` | Talep reddedildi; akış kapandı. |

Akış: `requested` → `approved` → `received` → `refunded` (ya da geri ödeme yoksa `received` yerine doğrudan `closed`);
`requested` → `rejected`. Teslimatsız iade `approved` ile başlar. Her adım yalnız bir önceki durumdan yapılabilir
("'approved' durumundaki iade onaylanamaz." gibi hatalar yanlış sıradan gelir).
- **Müşteri iadesi yalnız teslim edilmiş** siparişte açılır ("Müşteri iadesi yalnızca teslim edilmiş siparişler için açılabilir; kargodaki
  sipariş için Teslimatsız İade kullanın."). Kargoda olup müşterinin reddettiği paket → sipariş detayında **Teslimatsız İade**.
- Fatura kesilmeden önceki hiçbir aşamada iade yoktur, yalnız **İptal** vardır (sipariş detayı).
- Stok etkisi **yalnız Teslim Al** adımındadır (onay stok değiştirmez).
- **Geri ödeme kuralı (tek kural):** para iadesi yalnız müşteriden **tahsilat yapıldıysa** hesaplanır. Kart/havale/cüzdan ödemesinde
  tahsilat checkout anında vardır. **Kapıda ödeme** teslim edilmeden iade alınırsa tahsilat yoktur → geri ödeme yok; teslim edilmiş kapıda
  ödemede tahsilat teslimde kaydedilir → müşteri iadesinde geri ödeme vardır. **Pazaryeri** siparişinde hiçbir durumda para iadesi yapılmaz
  (iadeyi pazaryeri yapar). Uygun olmayan iadede "Geri Ödeme Yap" butonu görünmez; yine de istek giderse sunucu reddeder.
- Teslimatsız iadede geri ödeme tutarı tahsil edilenin **tamamıdır** (kargo dahil); müşteri iadesinde yalnız ürün kalemleri (kargo ücreti
  iade edilmez). Tutarlar kampanya/kupon indirimi düşülmüş gerçek ödenen fiyattan gelir; vade farkı payı eklenir.
- Geri ödeme yöntemi Cüzdan ise müşterinin cüzdan bakiyesine hareket yazılır; yazılamazsa işlem başarısız olur ve hata görünür.

## Adım adım
**Bir iade talebini sonuçlandırma**
1. **İadeler → Talep Edilen** sekmesinde talebi açın; kalemleri, nedenleri ve görselleri inceleyin.
2. **Onayla** (ya da **Reddet** + neden).
3. Ürün depoya ulaşınca **Teslim Al** → depo seçin, muayene notu yazın → **Teslim Al**. (Stok geri yazılır.)
4. **Geri Ödeme Yap** → yöntem ve tutarı kontrol edin → **Geri Ödeme Yap**. (Durum: Geri Ödendi.)

**Teslimatsız iade (paket geri döndü)**
1. Sipariş detayında **Teslimatsız İade** → nedeni yazın → **Teslimatsız İade Uygula**. (Sipariş: Teslimatsız İade; fatura iptal; iade `approved`.)
2. Paket depoya ulaşınca **İadeler → Onaylı** sekmesinden iadeyi açın → **Teslim Al** → depo seçin.
3. Tahsilat varsa (kart) → **Geri Ödeme Yap**; kapıda ödemeyse iade kendiliğinden **Kapanan** olur.

**İade nedeni ekleme**
1. Liste başlığındaki **İade Nedenleri**'ne tıklayın → **+ Yeni Neden**.
2. Ana başlığı ve her satıra bir alt nedeni yazın, Aktif bırakın → **Kaydet**.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Reddetme ve geri ödeme geri alınamaz; red nedeni müşteriye gösterilir.

> **İpucu:** Müşteri notu ve fotoğraflar karar vermeyi kolaylaştırır; kalem nedeninin altındaki küçük yazı müşterinin o kaleme yazdığı açıklamadır.

> **Not:** Reddedilen bir talep için müşteri yeniden talep açabilir; pasife alınan nedenler eski taleplerde görünmeye devam eder.

> **Not:** "Teslim Edilemedi" sistem nedeni pasiftir: müşteri formunda görünmez, teslimatsız iade kalemlerine otomatik yazılır.

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparis-detay/)
- [Siparişler](/rehber/siparis/siparisler/)
