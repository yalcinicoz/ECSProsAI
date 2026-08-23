---
title: Numara Serileri
route: /orders/number-series
group: Sipariş Yönetimi
order: 50
summary: Satış kanalı başına sipariş ve paket numarası serilerinin (önek, dolgu, aktiflik) ve taşıyıcılara tahsis edilen kargo barkod aralıklarının yönetildiği ekran.
---

## Ne işe yarar
Sipariş ve paket numaraları rastgele değil, **satış kanalına özel serilerden** üretilir: numara = önek + soldan sıfır dolgulu sayaç
(ör. `MIS000001`). Bu sayfada her kanal için sipariş serisi ve paket serisi tanımlanır/düzenlenir; ayrıca barkodu taşıyıcının tahsis
ettiği aralıklardan alan kargolar (ör. PTT) için **kargo barkod aralıkları** girilir ve doluluğu izlenir. Kurulum ve operasyon
yöneticileri kullanır; gündelik operasyonda dokunulmaz.

## Ekran yerleşimi
![Numara Serileri — Sipariş Numarası Serileri, Paket Numarası Serileri tabloları ve Kargo Barkod Aralıkları kartı](img/orders-number-series.webp)
1. **Başlık** — "Numara Serileri" ve açıklama (pazaryeri siparişlerinde seri kullanılmaz; pazaryerinin numarası aynen saklanır).
2. **Sipariş Numarası Serileri** kartı — kanal başına bir satır, satır içi düzenleme.
3. **Paket Numarası Serileri** kartı — aynı yapı; paket numarası siparişten bağımsız üretilir (~6 hane önerilir).
4. **Kargo Barkod Aralıkları** kartı — Firma → Kargo Entegrasyonu seçimi, aralık ekleme formu ve aralık tablosu.

## Liste ve filtreler

### Sipariş / Paket Numarası Serileri tabloları
| Sütun | Anlamı |
|---|---|
| KANAL | Satış kanalının adı ve kodu. Firmanın tüm kanalları listelenir; serisi henüz açılmamış kanal da satır olarak görünür. |
| ÖNEK | Numaranın başındaki harf/rakam öneki (en fazla 10 karakter; otomatik büyük harf). |
| DOLGU | Sayacın hane sayısı (4–12; varsayılan 6). |
| ÖRNEK | Girilen önek/dolguya göre canlı örnek (ör. `MIS000001`). |
| SIRADAKİ | Serinin vereceği bir sonraki sayaç değeri; seri açılmamışsa "—". **Elle değiştirilemez.** |
| AKTİF | Onay kutusu; pasif seri numara üretmez. |
| (son sütun) | **Seri Aç** (seri yoksa) / **Kaydet** (seri varsa; yalnız değişiklik yapıldığında etkin) + "Kaydedildi" onayı. |

Tablonun altında hatırlatma: "Sıradaki değer elle değiştirilemez; kullanılan numaralar iptalde bile havuza geri dönmez."

### Kargo Barkod Aralıkları tablosu
| Sütun | Anlamı |
|---|---|
| ARALIK | Başlangıç – bitiş barkod numarası. |
| KULLANIM | Kullanılan / toplam adet. |
| DOLULUK | Yüzde çubuğu: %70'ten sonra sarı, %90'dan sonra kırmızı. |
| DURUM | `Aktif` (yeşil) / `Pasif` (gri) / `Tükendi` (kırmızı — aralık bitti). |
| (son sütun) | **Pasifleştir** / **Aktifleştir** düğmesi (tükenmiş aralıkta yoktur). |

Tablo yalnız bir kargo entegrasyonu seçilince görünür; aralık yoksa "Bu entegrasyona tanımlı aralık yok." yazar.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Seri Aç | Seri satırı | Kanal için seri oluşturur (girilen önek/dolgu/aktiflik ile). | Seri yokken |
| Kaydet | Seri satırı | Önek, dolgu ve aktiflik değişikliklerini kaydeder; 2 sn "Kaydedildi" gösterir. | Satırda değişiklik yapılmış olmalı |
| Firma / Kargo Entegrasyonu seçimi | Kargo Barkod Aralıkları | Firma seçilince o firmanın kargo entegrasyonları listelenir; entegrasyon seçilince aralık tablosu açılır. | — |
| Aralık Ekle | Kargo Barkod Aralıkları | Başlangıç–bitiş değerleriyle yeni aralık tanımlar. | Entegrasyon seçili, iki değer dolu |
| Pasifleştir / Aktifleştir | Aralık satırı | Aralığın kullanımını durdurur/yeniden açar. | Aralık tükenmemiş |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar |
|---|---|---|
| Önek | Hayır | En fazla 10 karakter; yalnız harf ve rakam ("Önek yalnız harf ve rakam içerebilir."). Boş bırakılabilir. |
| Dolgu | Evet | 4–12 arası ("Dolgu uzunluğu 4-12 arasında olmalıdır."). |
| Aktif | — | Varsayılan açık. |
| Başlangıç / Bitiş (aralık) | Evet | Pozitif sayı; bitiş başlangıçtan küçük olamaz; aynı entegrasyonda çakışan aralık tanımlanamaz ("…çakışan bir aralık zaten tanımlı."). |

## Durumlar ve iş kuralları
- **Hiçbir numara havuza geri dönmez:** sipariş no, paket no ve kargo barkodu serilerinde iptal/yeniden numaralandırma eski değeri yakar; sayaç geri alınmaz.
- Seri tanımlanmamış kanalda ilk sipariş gelince güvenli bir varsayılan seri kendiliğinden açılır (önek = kanal kodu); sonradan burada düzenlenebilir.
- **Pazaryeri** kanallarında seri kullanılmaz; pazaryerinin sipariş numarası aynen saklanır.
- Paket numarası sipariş numarasından bağımsızdır; kimlik "kanal + sipariş no + paket no" üçlüsüdür.
- Kargo barkod aralığı yalnız **tahsisli aralık** kuralıyla çalışan taşıyıcılar içindir (ör. PTT); serbest/kurallı kod üreten taşıyıcılarda gerekmez.
  Aralık bitince sıradaki aktif aralığa geçilir; hiç aralık kalmadıysa kargo kodu üretimi anlaşılır bir hatayla durur — yeni aralık tanımlayın.
- Aralık sınırları ve sayacı sonradan değiştirilemez; yalnız pasife alınabilir.

## Adım adım
**Bir kanala sipariş serisi açma**
1. **Sipariş Numarası Serileri** tablosunda kanalın satırını bulun.
2. Önek (ör. `MIS`) ve dolguyu (ör. 7) girin; ÖRNEK sütunundan sonucu kontrol edin.
3. **Seri Aç**'a tıklayın. Aynı işlemi **Paket Numarası Serileri** için de yapın.

**PTT barkod aralığı tanımlama**
1. **Kargo Barkod Aralıkları** kartında Firma'yı, ardından PTT kargo entegrasyonunu seçin.
2. Taşıyıcının verdiği başlangıç ve bitiş numaralarını yazın → **Aralık Ekle**.
3. Doluluk çubuğunu izleyin; %90'a yaklaşınca yeni aralık isteyip ekleyin.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Öneki sonradan değiştirmek geçmiş numaraları etkilemez ama yeni numaralar farklı görünür; kanal açılırken karar verin.

> **Dikkat:** Sıradaki değer elle geri alınamaz; "numara atlandı" görünmesi normaldir (iptal edilen siparişlerin numarası yakılır).

> **İpucu:** Kargo entegrasyon listesi boşsa o firma için kargo entegrasyonu tanımlı değildir (Ayarlar → Firmalar → Entegrasyonlar).

## İlgili sayfalar
- [Sipariş Detayı](/rehber/siparis/siparis-detay/) (paket numarası ve kargo kodu işlemleri)
- [Faturalar](/rehber/siparis/faturalar/) (fatura serileri)
- [Kargo Bölgeleri](/rehber/siparis/kargo-bolgeleri/)
