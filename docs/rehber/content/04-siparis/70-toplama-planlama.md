---
title: Toplama Planlama
route: /fulfillment/picking-plans
group: Sipariş Yönetimi
order: 70
summary: Onaylı siparişlerden toplama görevi oluşturma, görevleri listeleme, satırları depo personeline dağıtma ve toplanma ilerlemesini izleme.
---

## Ne işe yarar
Toplama Planlama, depo operasyonunun başlangıç noktasıdır. Onaylanmış ve stoğu rezerve edilmiş siparişler
filtrelenir, bunlardan **toplama görevleri** oluşturulur; görevin satırları (hangi raftan, hangi ürün, kaç adet)
depo personeline dağıtılır ve toplanma ilerlemesi buradan izlenir. Operasyon sorumlusu (idari personel) günde
bir ya da birkaç kez görev açar; depo personeli kendi satırlarını [Ürün Toplama](/rehber/siparis/urun-toplama/)
ekranından okutarak toplar.

Sistem görev oluştururken siparişleri otomatik olarak ikiye ayırır:
- **Tek ürünlü** siparişler → tek ürünlü görev; ürünler doğrudan [Hızlı Hat](/rehber/siparis/hizli-hat/) ekranında paketlenir.
- **Çok ürünlü** siparişler → çok ürünlü görev; ürünler [Ara Ayrıştırma](/rehber/siparis/ara-ayristirma/) ile kolilere,
  sonra [Masa](/rehber/siparis/masa-ve-paketleme/) ekranında slotlara ayrılır ve paketlenir.

## Ekran yerleşimi
![Toplama Görevleri listesi](img/fulfillment-picking-plans.webp)
1. **Başlık ve kayıt sayısı** — "Toplama Görevleri" başlığı, toplam kayıt sayısı ve sağda **+ Yeni Görev** butonu.
2. **Durum sekmeleri** — Tümü / Bekleyen / Toplanan / Tamamlanan.
3. **Görev tablosu** — satıra tıklayınca görev detayı açılır; satır sonunda hızlı aksiyon bağlantıları (Başlat / Tamamla).
4. **Sayfalama** — sayfa başına 20 görev.

Bu başlık altında üç ekran anlatılır: **Görev listesi** (`/fulfillment/picking-plans`), **Görev oluşturma**
(`/fulfillment/tasks/new`) ve **Görev detayı / dağıtım** (`/fulfillment/tasks/:id`).

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| PLAN NO | Görev numarası (örn. `PICK-20260809-A1B2C3`). |
| TİP | `Tek ürünlü` (mavi rozet) veya `Çok ürünlü`. Eski akışla açılmış görevlerde `Tekli` / `Toplu` / `Dalga` görülebilir. |
| SİPARİŞ | Görevdeki sipariş sayısı. |
| DAĞITIM | Personel dağıtım rozeti: `Dağıtım yapılmadı` (kırmızı), `Dağıtım eksik (atanan/toplam)` (sarı), `Dağıtım tamam` (yeşil), `Satır yok`. |
| TOPLANMA | Toplanan satır / toplam satır ve yüzde çubuğu; %100 olunca çubuk yeşile döner. |
| PLANLAMA | Görevin oluşturulduğu tarih-saat. |
| DURUM | `Bekliyor` / `Toplanıyor` / `Tamamlandı` / `İptal`. |
| (son sütun) | Durum `Bekliyor` ise **Başlat**, `Toplanıyor` ise **Tamamla** bağlantısı. |

| Filtre | Ne yapar |
|---|---|
| Tümü | Tüm görevler. |
| Bekleyen | Durumu `pending` (Bekliyor) olanlar. |
| Toplanan | Durumu `picking` (Toplanıyor) olanlar. |
| Tamamlanan | Durumu `completed` olanlar. |

Sıralama sunucu tarafındadır (en yeni üstte). Satıra tıklayınca görev detayı açılır.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| + Yeni Görev | Liste üst sağ | Görev oluşturma ekranı açılır. | — |
| Başlat | Liste satırı sonu / detay başlığı | Onay sorusu ("… toplama başlatılsın mı?") sonrası görev `Toplanıyor` durumuna geçer. | Görev `Bekliyor` olmalı; aksi halde "'…' durumundaki plan başlatılamaz." hatası. |
| Tamamla | Liste satırı sonu / detay başlığı | Onay sorusu sonrası görev `Tamamlandı` olur; tamamlanan görevde dağıtım yapılamaz. | Görev `Toplanıyor` olmalı. |
| Görev(ler)i Oluştur | Görev oluşturma ekranı, özet şeridi | Filtreye uyan siparişlerden görev(ler) açılır; başarı penceresi görev numaralarını listeler. | Eşleşen sipariş sayısı 0 ise buton pasiftir. |
| Göreve Git / Görev Listesine Git | Başarı penceresi | Tek görev oluştuysa doğrudan detayına, aksi halde listeye gider. | — |
| Hızlı Hat Ekranı | Görev detayı başlığı | Tek ürünlü görev için Hızlı Hat okutma ekranı açılır. | Tip `Tek ürünlü`, durum Bekliyor/Toplanıyor. |
| Ara Ayrıştırma / Koli Duvarı | Görev detayı başlığı | Çok ürünlü görev için ilgili operasyon ekranı açılır. | Tip `Çok ürünlü`, durum Bekliyor/Toplanıyor. |
| Seçilenleri Ata | Görev detayı dağıtım çubuğu | İşaretli satırlar seçilen personele atanır; satır durumu `Atandı` olur. | En az 1 satır seçili + personel seçilmiş; görev Bekliyor/Toplanıyor. Toplanmış satır yeniden atanamaz ("… satır toplanmış/kapanmış — yeniden atanamaz."). |
| Atanmamışları Eşit Paylaştır (N) | Görev detayı dağıtım çubuğu | Pencere açılır; seçilen personellere atanmamış satırlar rota sırasıyla dönüşümlü dağıtılır. | Atanmamış satır olmalı (N > 0). |
| Paylaştır / Vazgeç | Eşit paylaştırma penceresi | Dağıtımı uygular / pencereyi kapatır. | En az bir personel işaretli. |

## Görev oluşturma ekranı
![Görev Oluşturma — filtreler, özet şeridi ve önizleme](img/fulfillment-tasks-new.webp)
*(1) Filtre kartı · (2) Özet şeridi + Görev(ler)i Oluştur · (3) Önizleme tablosu*

### Form alanları (filtreler)
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Kanallar | Hayır | Onay kutulu çoklu seçim. Boş bırakılırsa tüm kanallar dahildir. |
| Depo | Hayır | Tek depo seçimi. Seçilince yalnız **tüm** rezervasyonları o depoda olan siparişler listelenir; karma depolu siparişler hariç tutulur (özet şeridinde sayısı görünür). |
| Kargo | Hayır | Siparişte istenen kargo şirketi. Seçenekler önizlemede görülen kargolardan oluşur. |
| Teslimat Şehri | Hayır | Teslimat adresinin ili (il listesi yüklenemezse alan gizlenir). |
| Ürün Sayısı | Hayır | `Tümü` / `Tek ürünlü` / `Çok ürünlü`. Çok ürünlüde **Min** (en az 2) ve **Maks** (boş = sınırsız) adet kutuları açılır. Bu seçim aynı zamanda hangi görev tiplerinin açılacağını belirler. |
| Sipariş Tarihi | Hayır | Başlangıç — bitiş tarihi (bitiş günü sonuna kadar dahil). |

Filtre değiştikçe özet şeridi yeniden hesaplanır ("Hesaplanıyor…"):
`Eşleşen: N sipariş (T tek / Ç çok) | Karma depolu (hariç): K` ve sağda oluşturulacak görev tipi bilgisi
("Tek + çok ürünlü görevler ayrı ayrı oluşturulacak" / "Tek ürünlü görev oluşturulacak" / "Çok ürünlü görev oluşturulacak").

### Önizleme tablosu
| Sütun | Anlamı |
|---|---|
| SİPARİŞ NO | Sipariş numarası; satıra tıklayınca sipariş detayı açılır. |
| KANAL | Siparişin satış kanalı. |
| TARİH | Sipariş tarihi. |
| ADET | Siparişteki toplam ürün adedi. |
| KARGO | İstenen kargo şirketi. |
| (rozet) | `Karma depolu` — rezervasyonları birden fazla depoda olan sipariş. |

Eşleşen sipariş sayısı önizlemeden fazlaysa başlıkta "(ilk N / toplam)" yazar.

## Görev detayı ve dağıtım ekranı
![Görev detayı — başlık rozetleri, dağıtım çubuğu ve rota sıralı satır tablosu](img/fulfillment-tasks-detay.webp)
*(1) Başlık: plan no + tip/durum/dağıtım rozetleri + aksiyon butonları · (2) Özet satırı (planlama, başlama, bitiş, depo, toplanma) · (3) Dağıtım çubuğu · (4) Satır tablosu*

| Sütun | Anlamı |
|---|---|
| (onay kutusu) | Satır seçimi; yalnız `Bekliyor` / `Atandı` satırlar seçilebilir. Başlıktaki kutu tümünü seçer. |
| # | Rota sırası — personelin depoda yürüyüş sırası (raf koduna göre; rafı olmayan satırlar sona). |
| RAF | Önerilen kaynak raf kodu (siparişin rezervasyon rafı). |
| SKU | Ürün/varyant kodu. |
| ÜRÜN | Ürün adı + varyant bilgisi. |
| SİPARİŞ NO | Satırın ait olduğu sipariş. |
| ADET | İstenen adet; toplama başladıysa `toplanan/istenen`. |
| DURUM | `Bekliyor` / `Atandı` / `Toplandı` / `Eksik` (bulunamadı) / `İade`. |
| ATANAN | Satırın atandığı personel. |

Dağıtım çubuğu yalnız görev `Bekliyor` veya `Toplanıyor` iken görünür: seçili satır sayısı, **Personel seçin**
kutusu (aktif kullanıcılar), **Seçilenleri Ata** ve **Atanmamışları Eşit Paylaştır (N)**.

## Durumlar ve iş kuralları
| Durum | Anlamı / geçiş |
|---|---|
| `pending` Bekliyor | Görev oluşturuldu. **Başlat** ile `picking`'e geçer. Dağıtım yapılabilir. |
| `picking` Toplanıyor | Personel okutuyor. **Tamamla** ile `completed` olur. Dağıtım yapılabilir. |
| `completed` Tamamlandı | Kapandı; dağıtım/atama yapılamaz. |
| `cancelled` İptal | İptal edilmiş görev. |

- **Aday sipariş kuralı:** Yalnız durumu **onaylı** (`confirmed`) olan, henüz bir göreve bağlanmamış ve
  stoğu **rezerve edilmiş** siparişler görev adayıdır. Rezervasyonu olmayan sipariş (örneğin eski sistemden
  aktarılmış geçiş siparişleri) listede çıkmaz.
- **Tek/çok otomatik ayrım:** Toplam adedi 1 olan sipariş tek ürünlü, diğerleri çok ürünlü kabul edilir; her
  tip için ayrı görev açılır. Ürün Sayısı filtresi `Tek ürünlü` ise yalnız tek ürünlü, `Çok ürünlü` ise yalnız
  çok ürünlü görev oluşur.
- **Görev oluşunca sipariş `İşleniyor` (processing) durumuna geçer.**
- **Rota sırası:** Satırlar önerilen raf koduna göre sıralanır; aynı raf için sipariş numarası ikincil anahtardır.
  Raf, siparişin rezervasyonundan gelir (kısım ve raf toplama sırası dikkate alınır).
- **Dağıtım rozeti** görevin tamamını kapsar: hiç atama yoksa kırmızı, kısmen sarı, tamamı atandıysa yeşil.
- **Eşit paylaştırma** atanmamış satırları seçilen personellere rota sırasıyla dönüşümlü (1→A, 2→B, 3→A…) verir;
  pencerede personel başına yaklaşık kaç satır düşeceği gösterilir.
- **Operasyon günlüğü:** Görev oluşturma, satır atama, toplama, ayrıştırma, paketleme gibi her adım sipariş
  bazında kaydedilir ve sipariş detayındaki **Operasyon Geçmişi** bölümünde zaman sırasıyla görünür.
- Çoklu (çok ürünlü) görevlerde sipariş iptali süreci durdurmaz; iptal paketleme aşamasında yakalanır.

## Adım adım
**Yeni görev oluşturma**
1. Sol menüden **Sipariş Yönetimi → Toplama Planlama**'ya gelin, **+ Yeni Görev**'e tıklayın.
2. Filtreleri seçin (kanal, depo, kargo, şehir, ürün sayısı, tarih). Özet şeridindeki eşleşen sipariş sayısını kontrol edin.
3. Önizlemede siparişleri gözden geçirin; gerekirse bir satıra tıklayıp sipariş detayına bakın.
4. **Görev(ler)i Oluştur**'a tıklayın. Başarı penceresinde oluşan görevleri (plan no, tip, sipariş ve satır sayısı) görün.
5. **Göreve Git** (tek görev) ya da **Görev Listesine Git** ile devam edin.

**Satırları personele dağıtma**
1. Listeden göreve tıklayın; detay açılır.
2. Satırları onay kutularıyla seçin (başlıktaki kutu tümünü seçer), **Personel seçin** kutusundan kişiyi seçip **Seçilenleri Ata**'ya tıklayın.
3. Kalan satırlar için **Atanmamışları Eşit Paylaştır (N)** → personelleri işaretleyin → **Paylaştır**.
4. Başlıktaki dağıtım rozetinin `Dağıtım tamam` olduğundan emin olun ve **Başlat**'a tıklayın.
5. Personel, [Ürün Toplama](/rehber/siparis/urun-toplama/) ekranında görevi görür ve okutmaya başlar.

**Görevi kapatma**
1. TOPLANMA sütunu %100'e ulaşınca (ya da kalan satırlar `Eksik` işaretlenince) **Tamamla**'ya tıklayın ve onaylayın.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Depo filtresi seçiliyken karma depolu siparişler listede çıkmaz; bunları unutmamak için filtreyi
> kaldırıp özet şeridindeki "Karma depolu (hariç)" sayısını kontrol edin.

> **Dikkat:** Tamamlanmış görevde atama yapılamaz; "'completed' durumundaki görevde dağıtım yapılamaz." hatası alırsınız.
> Toplanmış satır başka personele aktarılamaz.

> **Not:** "Filtreye uyan sipariş bulunamadı." / "Seçilen görev tiplerine uyan sipariş yok." mesajları, filtreye
> uyan onaylı-rezervasyonlu siparişin bulunmadığını ya da seçilen tipte (tek/çok) sipariş olmadığını söyler.

> **Not:** Görev detayında "Bu görevde satır yok (eski akışla oluşturulmuş olabilir)." görünüyorsa görev satır
> bazlı dağıtım öncesi açılmıştır; yeni görev oluşturun.

## İlgili sayfalar
- [Ürün Toplama](/rehber/siparis/urun-toplama/)
- [Hızlı Hat](/rehber/siparis/hizli-hat/)
- [Ara Ayrıştırma ve Koli Duvarı](/rehber/siparis/ara-ayristirma/)
- [Masa ve Paketleme](/rehber/siparis/masa-ve-paketleme/)
- [Siparişler](/rehber/siparis/siparisler/)
- [Sipariş Detayı — Operasyon Geçmişi](/rehber/siparis/siparis-detay/)
