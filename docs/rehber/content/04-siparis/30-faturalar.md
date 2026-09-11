---
title: Faturalar
route: /orders/invoices
group: Sipariş Yönetimi
order: 30
summary: Kesilen faturaların listelendiği, entegratör PDF adresinin kaydedildiği, faturanın iptal edildiği ve fatura serilerinin tanımlandığı ekran.
---

## Ne işe yarar
Faturalar sayfası, siparişlerden kesilen faturaların kaydıdır. Fatura **sipariş detayındaki "+ Fatura Oluştur"** ile kesilir; bu
sayfada ise faturalar sipariş, alıcı, tutar kırılımı ve gönderim (entegratör / ERP / pazaryeri) bilgileriyle listelenir; her satırın
sağındaki sabit **Görüntüle** düğmesi faturayı PDF olarak açar, **URL** düğmesi fatura adreslerini kopyalanabilir küçük bir pencerede
gösterir. Fatura iptali sipariş detayındaki Faturalar kartından yapılır. Fatura numarasının türetildiği **fatura serileri** de buradaki "Fatura Serileri" penceresinden tanımlanır.
Muhasebe/operasyon personeli kullanır.

## Ekran yerleşimi
![Faturalar listesi — durum sekmeleri, fatura tablosu, sağ üstte Fatura Serileri butonu](img/orders-invoices.webp)
1. **Başlık** — "Faturalar" + kayıt sayısı; sağda **Fatura Serileri** butonu.
2. **Durum sekmeleri** — Oluşturulan / İptal Edilen / Tümü.
3. **Fatura tablosu** — satır tıklanmaz; sağdaki sabit **Görüntüle / URL** düğmeleri kullanılır.
4. **Sayfalama** — 20 kayıt/sayfa.
5. **Pencereler** — "Fatura {no} — adresler" (URL) penceresi ve "Fatura Serileri" penceresi.

## Liste ve filtreler
| Sekme | Durum |
|---|---|
| Oluşturulan (varsayılan) | `created` |
| İptal Edilen | `cancelled` |
| Tümü | hepsi |

| Sütun | Anlamı |
|---|---|
| FATURA NO | Seriden türetilen numara: seri kodu + yıl + 9 haneli sıra (ör. `MSH2026000000001`); dış kaynaklı numarada kaynak rozeti (ERP / Pazaryeri / Entegratör). |
| SİPARİŞ NO | Faturanın siparişi; tıklanınca sipariş detayı açılır. |
| FATURA TARİHİ | Faturanın tarihi. |
| OLUŞTURMA | Kaydın panelde oluşturulduğu tarih-saat. |
| ETTN | Entegratörün verdiği evrensel tekil tanımlama numarası (henüz gönderilmediyse "—"). |
| VKN / TCKN | Alıcının vergi kimlik ya da TC kimlik numarası. |
| FATURA TİPİ | e-Arşiv / e-Fatura / İhracat. |
| PARA BİRİMİ | Siparişin fatura para birimi (varsayılan TRY). |
| TOPLAM TUTAR | Mal/hizmet toplamı (indirim ve vergi öncesi). |
| ÖDENECEK | Vergiler dahil genel toplam. |
| VERGİ MATRAHI | Toplam tutar − indirim. |
| VERGİ TOPLAMI | Hesaplanan KDV toplamı. |
| ENTEGRATÖR | Entegratör gönderim durumu + gönderim tarihi; PDF kayıtlıysa "PDF ✓". |
| ERP | ERP gönderim durumu (Gönderim yok / Bekliyor / Gönderildi / ERP kesti) + tarih + ERP referansı. |
| PAZARYERİ | Faturayı pazaryeri kestiyse pazaryeri adı ve belge numarası; değilse "—". |
| DURUM | `Oluşturuldu` (yeşil) / `İptal` (kırmızı). |
| ALICI | Alıcı adı — varsayılan gizli, Kolonlar menüsünden açılır. |
| PAZARYERİ (devam) | Sıralanabilir; başlık filtresi pazaryeri adına göre süzer. |
| (son sütun) | Belge ikonu (**Görüntüle**) ve zincir ikonu (**URL**); masaüstünde yatay kaydırmada sağda sabit kalır, mobilde sabit değildir. |

Arama kutusu fatura no, alıcı, vergi no ve dış belge numarasında arar; her sütun başlığında filtre vardır. Belirli bir siparişin
faturaları sipariş detayındaki **Faturalar** kartında da listelenir. Liste boşken
"Fatura bulunamadı. Fatura, sipariş detayındaki "Fatura Oluştur" ile kesilir." mesajı görünür.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Görüntüle (belge ikonu) | Satırın sağındaki sütun | Entegratör PDF'i kayıtlıysa faturanın PDF'ini yeni sekmede açar (site sunucusu üzerinden, adres istemciye inmez). PDF henüz yoksa fatura yazdırma sayfası açılır (tarayıcıdan PDF olarak kaydedilebilir). | `orders.invoices.view` |
| URL (zincir ikonu) | Satırın sağındaki sütun | Küçük pencere: **entegratörün ürettiği fatura adresi** + **Kopyala** (pazaryeri vb. yerlere verilen adres budur). Entegratör henüz adres üretmediyse bilgi mesajı görünür. | — |
| Sipariş no | Liste | Siparişin detay sayfasına gider. | — |
| İptal Et ⚠️ | Sipariş detayı → Faturalar kartı (kırmızı) | Onay sorulur; fatura `İptal` olur. Geri alınamaz; numara yeniden kullanılmaz. | Durum `Oluşturuldu` |
| Fatura Serileri | Liste başlığı | "Fatura Serileri" penceresi: mevcut seriler (ad · e-Arşiv/e-Fatura/İhracat kodları · `Pasif` rozeti) + YENİ SERİ formu. | — |
| + Seri Ekle | Fatura Serileri penceresi | Yeni seri oluşturur; form temizlenir, liste yenilenir. | Firma ve e-Arşiv Seri dolu |

## Form alanları

### Entegratör PDF adresi
Panelde elle adres giriş alanı kalmadı (2026-09-11); adres entegratör gönderimiyle (FE3/FE4) ya da sipariş detayındaki
"Dış fatura kaydet" akışıyla oluşur. Müşteriye doğrudan verilmez; site sunucusu üzerinden görüntülenir.

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
3. Faturayı görmek için **Faturalar** sayfasında satırın sağındaki **Görüntüle**'ye basın; adresi paylaşmak için **URL → Kopyala**.

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
