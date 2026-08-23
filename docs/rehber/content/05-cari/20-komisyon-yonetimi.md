---
title: Komisyon Yönetimi
route: /commission
group: Cari
order: 20
summary: Pazaryeri satıcılarınızdan kesilecek komisyonun katmanlı oran tanımları (varsayılan, sözleşme, kampanya, ciro basamağı, ürün-özel), kampanya katılım şartları ve satıcı hakedişlerinin mutabakatı ile ödeme çıkışı.
---

## Ne işe yarar
Sitenizde kendi malını satan satıcıların (tedarikçi cari hesapları) her teslim edilen satıştan ne oranda komisyon ödeyeceğini, indirim yükünü ne ölçüde paylaşacağını ve hakedişlerinin ne zaman ödenebilir hale geleceğini bu ekrandan yönetirsiniz. Dört sekmesi sırasıyla oran tabanını, satıcıya özel sözleşmeyi, kampanya şartlarını ve satır satır hakediş mutabakatını kapsar. Muhasebe/finans sorumlusu kullanır; hakediş satırları sistem tarafından teslimatta otomatik üretilir, burada izlenir ve ödenir.

## Ekran yerleşimi
![Komisyon Yönetimi — sekmeler ve Varsayılan Oranlar](img/commission.webp)
1. **Başlık** — "Komisyon Yönetimi" ve kısa açıklama.
2. **Sekmeler** — `Varsayılan Oranlar` · `Satıcı Sözleşmeleri` · `Kampanyalar` · `Hakedişler`.
3. **İçerik kartları** — seçilen sekmenin formu ya da tablosu.

*(1) Başlık · (2) Sekmeler · (3) İçerik*

## Sekmeler

### Varsayılan Oranlar
Ürün grubu bazlı platform varsayılanları (katman 5). Satıcıya özel oran yoksa bu uygulanır; boş bırakılan grupta oran tanımsız kalır ve o gruptaki satışlar **kesintisiz** (komisyon %0, katman "Tanımsız") yazılır.

| Alan / öğe | Zorunlu | Açıklama |
|---|---|---|
| Grup ara… | Hayır | Grup listesini ada/koda göre daraltır. |
| Grup satırı → oran kutusu `%` | Hayır | Her ürün grubu için 0-100 arası yüzde (ondalık kabul eder, örn. `12.5`). Boş = tanımsız. |
| Kaydet | — | Dolu oranları topluca kaydeder; yanında "Kaydedildi" / "Kaydedilemedi" çıkar. %0-100 dışı değer "Oran %0-100 aralığında olmalıdır." hatası verir. |

### Satıcı Sözleşmeleri
Satıcı bazında hakediş şartları ve özel oranlar.

| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Satıcı (cari hesap) | Evet | Tedarikçi tipli cari hesaplar; "kod — unvan" biçiminde. Seçilince sözleşme yüklenir (yoksa varsayılanlarla boş form gelir). |
| Hakediş gecikmesi (teslim + X gün) | Evet | Teslimden kaç gün sonra hakedişin ödenebilir (bakiyeye geçer) olacağı; 0-365. Varsayılan 14. |
| Ödeme periyodu | Evet | `Haftalık` · `Aylık` · `Uygunlaşınca`. |
| Kargo modu (K3) | Evet | `Bizim sözleşmemizle biz göndeririz` · `Satıcı kendi sözleşmesiyle gönderir` · `Satıcı sözleşmesiyle biz göndeririz`. "Satıcı gönderir" seçilirse satıcının API hesabına kargo bildirimi yetkisi açılır. |
| Ciro basamağı dönemi | Evet | Ciro basamaklarının hesaplanacağı dönem: `Aylık` · `Yıllık` · `Kayan 12 ay`. |
| Notlar | Hayır | Serbest not. |
| Gruba özel oranlar (katman 3) | Hayır | Grup ara… + her ürün grubu için `%` kutusu. Boş bırakılan grup varsayılanı kullanır. |
| Ürüne özel oranlar (katman 1 — her şeyi ezer) | Hayır | Mevcut satırlar "ürün · %oran · Kaldır". Eklemek için **Ürün kodu (PRD-…)** + `%` girip **Ekle**; kod bulunamazsa "Ürün bulunamadı.", alan boşsa "Ürün kodu ve oran gerekli." |
| Ciro basamakları (katman 4) | Hayır | Satır: `Ciro ≥ [tutar] TL → oran [puan] puan` + **Kaldır**; **Basamak Ekle** yeni satır açar. Puan ayarı grup oranına eklenir; negatif = indirim (örn. `-2` puan). |
| Sözleşmeyi Kaydet | — | Tüm sözleşmeyi kaydeder; "Kaydedildi" ya da hata metni (örn. "Geçersiz kargo modu.", "Hakediş gecikmesi 0-365 gün aralığında olmalıdır."). |

### Kampanyalar
Aktif kampanyaların satıcı şartları: kampanya penceresinde geçerli komisyon oranı (katman 2), indirim yükünün satıcı payı ve katılım (opt-in) şartı.

| Sütun | Anlamı |
|---|---|
| Kampanya | Kampanya adı ve kodu. |
| Komisyon % | Kampanyalı kalemlerde uygulanacak oran. Boş = kampanya oranı yok, sıradaki katman uygulanır. |
| İndirim payı % | Kalemdeki kampanya indiriminin yüzde kaçının satıcıdan kesileceği (0 = indirim tamamen pazaryerinde). |
| Opt-in | İşaretliyse kampanya şartları yalnız **katılan** satıcıların (ve katılımda ürün seçtilerse yalnız o ürünlerin) satışlarına uygulanır. |
| Katılım | "N satıcı" — kampanyaya katılmış satıcı sayısı. |
| Kaydet | Satırdaki üç alanı kaydeder. Oranlar %0-100 dışıysa "Oranlar %0-100 aralığında olmalıdır." |

Boşsa "Aktif kampanya yok."

### Hakedişler
Satıcı bazında hakediş satırları, bakiye ve ödeme çıkışı.

| Filtre / öğe | Ne yapar |
|---|---|
| Satıcı | Zorunlu; seçilmeden tablo gelmez. |
| Durum | `Tümü` · `Beklemede` · `Bakiyede` · `Ödendi` · `Ters kayıt`. |
| Hakediş bakiyesi | Satıcının hakediş defterindeki güncel bakiye ve para birimi. |
| Ödeme Çıkışı | `Bakiyede` durumundaki tüm satırların net toplamını ödeme olarak kaydeder ve satırları `Ödendi` yapar: "Ödeme kaydedildi: N satır, net X TL". ⚠️ Geri alınamaz. Bakiyede satır yoksa "Ödenecek 'available' hakediş satırı yok."; net toplam pozitif değilse "Ödenecek net tutar pozitif değil". |

Tablo sütunları (en çok 100 satır):
| Sütun | Anlamı |
|---|---|
| Sipariş / SKU | Sipariş numarası ("(iade)" eki ters kayıtlarda) ve altında SKU × adet. |
| Brüt | Kalemin ödenen tutarı (indirim düşülmüş). |
| Oran (katman) | Uygulanan yüzde ve hangi katmandan geldiği: `Ürün-özel` · `Kampanya` · `Sözleşme (grup)` · `Varsayılan (grup)` · `Tanımsız`; ciro ayarı uygulandıysa "+ ciro ayarı". |
| Komisyon | Brüt × oran. |
| İndirim payı | Kampanya indiriminin satıcıya yansıtılan kısmı. |
| Net | Brüt − Komisyon − İndirim payı (satıcıya ödenecek). |
| Durum | `Beklemede` (sarı) · `Bakiyede` (yeşil) · `Ödendi` (mavi) · `Ters kayıt` (kırmızı). |

Boşsa "Hakediş satırı yok."

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul |
|---|---|---|---|
| Kaydet | Varsayılan Oranlar | Grup varsayılanlarını kaydeder. | — |
| Ekle | Satıcı Sözleşmeleri → Ürüne özel oranlar | Ürün kodunu doğrulayıp listeye ekler (aynı ürün varsa oranı günceller). Kaydetmek için Sözleşmeyi Kaydet gerekir. | Satıcı seçili |
| Kaldır | Ürüne özel oranlar / Ciro basamakları | Satırı listeden çıkarır (Sözleşmeyi Kaydet ile kalıcı). | — |
| Basamak Ekle | Ciro basamakları | Boş basamak satırı ekler. | — |
| Sözleşmeyi Kaydet | Satıcı Sözleşmeleri | Sözleşmeyi kaydeder. | Satıcı seçili, cari hesap mevcut |
| Kaydet (satır) | Kampanyalar | Kampanyanın satıcı şartlarını kaydeder. | — |
| Ödeme Çıkışı | Hakedişler | Bakiyedeki satırları öder. ⚠️ Geri alınamaz. | Satıcı seçili, bakiyede pozitif net |

## Durumlar ve iş kuralları
- **Hakediş satırı oluşumu:** Satıcıya ait sipariş kalemleri sipariş **teslim edildiğinde** otomatik hakediş satırı olur (`Beklemede`). Brüt = kalemin indirim düşülmüş ödenen tutarı; aynı kalem için ikinci satır üretilmez.
- **Etkin oran (beş katman, ilk tutan uygulanır):** 1) satıcı sözleşmesindeki **ürün-özel** oran → 2) kalem kampanyalıysa ve (opt-in gerekiyorsa) satıcı katılmışsa **kampanya** oranı → 3) sözleşmedeki **grup** oranı → 5) **platform varsayılanı** (grup). 4) **Ciro basamağı** puan ayarı yalnız 3 ve 5'i değiştirir (oran sıfırın altına inmez). Hiçbiri yoksa oran %0, katman `Tanımsız`. Her satıra uygulanan katman yazılır — "bu satışta neden %X" tek bakışta görünür.
- **Ciro basamağı:** dönem cirosu, seçilen dönem tipine göre (ay başı / yıl başı / 12 ay geriye) satıcının ters kayıt dışı hakediş satırlarının brüt toplamıdır; tutan en yüksek basamağın puanı uygulanır.
- **İndirim payı:** kampanya geçerliyse kalem kampanya indiriminin "İndirim payı %" kadarı satıcıdan kesilir.
- **Durum akışı:** `Beklemede` → (teslim + X gün dolunca, 30 dakikada bir çalışan kontrolle) `Bakiyede` — net tutar satıcının hakediş defterine işlenir → **Ödeme Çıkışı** ile `Ödendi`. İade teslim alındığında kalemin satırı için **ters kayıt** üretilir: orijinal henüz beklemedeyse ve iade tamsa ikisi de deftere girmeden `Ters kayıt` olur; aksi halde ters satır `Beklemede` doğar ve uygunlaşınca negatif olarak bakiyeden düşer.
- Hakediş bakiyesi yalnız bu defter hareketlerinden oluşur; ekrandan elle satır eklenmez/silinmez.
- **Kampanya opt-in** şu an yalnız hakediş hesabında etkilidir; satıcının kampanyaya katılımı satıcı panelinden yapılır.
- **Yetki:** Sayfa giriş yapmış panel kullanıcılarına açıktır; ayrı izin aranmaz.

## Adım adım

**1. Oran tabanını kurma**
1. **Varsayılan Oranlar** → her ürün grubuna yüzde girin → **Kaydet**.
2. Satıcıya özel şart varsa **Satıcı Sözleşmeleri** → satıcıyı seçin → hakediş gecikmesi, ödeme periyodu, kargo modu, ciro dönemi → gerekiyorsa gruba/ürüne özel oranlar ve ciro basamakları → **Sözleşmeyi Kaydet**.

**2. Kampanya şartı tanımlama**
1. **Kampanyalar** → satırda Komisyon %, İndirim payı %, Opt-in → **Kaydet**.
2. Opt-in işaretliyse satıcıların katılımını Katılım sütunundan izleyin.

**3. Hakediş ödemesi**
1. **Hakedişler** → satıcıyı seçin → Durum `Bakiyede` ile satırları ve **Hakediş bakiyesi**ni kontrol edin.
2. Ters kayıt (iade) satırlarının düşüldüğünden emin olun.
3. **Ödeme Çıkışı** → "Ödeme kaydedildi: N satır, net X TL"; satırlar `Ödendi` olur.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Oran (katman) sütununda `Tanımsız` görüyorsanız o ürünün grubunda ne varsayılan ne sözleşme oranı var — satış %0 komisyonla yazılmıştır. Varsayılan Oranlar'ı tamamlayın; geçmiş satırlar değişmez.

> **Dikkat:** Ödeme Çıkışı bakiyedeki **tüm** satırları tek seferde öder; kısmi ödeme seçimi yoktur.

> **İpucu:** Ürüne özel oranda ürün kodu tam yazılmalıdır (örn. `PRD-000123`); Ekle butonu kodu katalogdan doğrular.

> **Not:** Satır henüz `Beklemede` iken bakiyede görünmez; teslim tarihinden sözleşmedeki X gün sonra otomatik bakiyeye geçer.

## İlgili sayfalar
- [Cari Kartlar](/rehber/cari/cari-kartlar/)
- [Kampanyalar](/rehber/pazarlama/kampanyalar/)
- [Pazaryerleri](/rehber/siparis/pazaryerleri/)
- [Siparişler](/rehber/siparis/siparisler/)
