# E-Ticaret Altyapı Projesi - Gereksinim ve Kapsam Dokümanı

**Doküman Versiyonu:** 1.0  
**Son Güncelleme:** Ocak 2025  
**Durum:** Beyin fırtınası tamamlandı, tasarım aşamasına geçiliyor

---

## 1. Proje Vizyonu

Mevcut e-ticaret yazılımı yıllar içinde yapılan eklemelerle kontrolden çıkmış durumda. Kullanılan araçlar yetersiz kalmakta. Bu nedenle sıfırdan, modern teknolojilerle, genişletilebilir bir ticaret altyapısı inşa edilecek.

**Temel Hedefler:**
- Çok firmalı, çok kanallı satış yapısı
- Sektörden bağımsız, esnek ürün yönetimi
- Ölçeklenebilir ve bakımı kolay mimari
- Mevcut operasyonel süreçlerin korunması ve iyileştirilmesi
- Çok dilli yapı (hem yönetim paneli hem müşteri siteleri için)

---

## 2. Firma ve Organizasyon Yapısı

### 2.1 Çok Firmalı Yapı
- Aynı çatı altında birden fazla firma bulunmakta (holding benzeri yapı)
- Firmalar ortak ürün havuzunu ve depoyu paylaşıyor
- Her firma kendi ticari kimliğiyle bağımsız hareket ediyor
- Firmalar arası rekabet ortamı mevcut

### 2.2 Firma Bağımsızlığı
- Her firmanın kendi e-ticaret sitesi olabilir
- Her firmanın kendi pazaryeri mağazaları olabilir
- Firmalar kendi faturalarını kesiyor
- Firmalar kendi satış fiyatlarını belirliyor
- Ürün meta bilgileri firma bazında özelleştirilebiliyor

### 2.3 Firmalar Arası İlişkiler
- Ortak depodaki ürünler farklı firmalar adına satılabiliyor
- Firmalar arası hesaplaşmalar takip edilmeli
- Ürün transferleri yönetilmeli

---

## 3. Satış Kanalları

### 3.1 Desteklenmesi Gereken Kanallar
- Online satış (e-ticaret siteleri)
- Pazaryerleri (Trendyol, Hepsiburada, vb.)
- Telefon satışı
- Fiziksel mağaza satışı
- Doğrudan depodan satış
- Dropshipping

### 3.2 Kanal Yönetimi
- Platform sayısı ve türleri firmalara özel
- Her firma kendi pazarlama stratejisini belirliyor
- Aynı ürün farklı kanallarda farklı fiyat ve açıklamayla satılabilir

---

## 4. Ürün Yönetimi

### 4.1 Ürün Veri Yapısı
- Ana ürün kaydı (master) merkezi olarak tutulacak
- Firma ve platform kombinasyonlarına göre özelleştirmeler yapılacak
- Her kombinasyon için ayrı fiyat ve meta bilgisi

### 4.2 Fiyatlandırma
- Ana firma baz satış fiyatını belirliyor
- Diğer firmalar ve kanallar için baz fiyat referans alınıyor
- Katsayılı fiyatlandırma yapılabilir
- Serbest fiyat belirlemesi de mümkün

### 4.3 Ürün Dağıtımı
- Filtre bazlı otomatik ürün dağıtımı
- Filtreler genellikle cinsiyet ve ürün grubu üzerinden
- Daha karmaşık filtreler de oluşturulabilir
- Elle ürün ekleme/çıkarma da mümkün olmalı

**Mevcut Sorun:** Periyodik filtre çalıştırma gecikme yaratıyor. Yeni ürünler gecikmeli satışa giriyor. Anlık veya çok kısa aralıklı güncelleme hedeflenmeli.

---

## 5. Stok Yönetimi

### 5.1 Stok Yapısı
- Tek havuz, çoklu erişim noktası prensibi
- Ana depo ve mağaza depoları var
- Mağaza stokları fiziksel olarak ayrı ama mantıksal olarak aynı sistemin parçası
- Mağaza stokları dönemsel olarak internet satışına açılabiliyor (manuel karar)

### 5.2 Stok Tipleri
- Fiziksel stok (depoda mevcut)
- Sanal/tedarik edilebilir stok (henüz teslim alınmamış, üretilebilir, temin edilebilir)

### 5.3 Stok Senkronizasyonu
- Kendi sistemleri anlık stok görüyor
- Pazaryerleri kuyruk üzerinden senkronize ediliyor
- Stok hareketi olduğunda tüm kanallar için güncelleme kuyruğa ekleniyor

**Mevcut Sorun:** Pazaryeri API limitleri nedeniyle istenen hızda güncellenemiyor. Güncelleme sıklığı şu an pazaryeri bazlı elle ayarlanıyor (2 dakika - 30 dakika arası). Hızlı satan ürünlerde sorun yaşanabiliyor.

**Hedef:** API limitlerinin maksimum sınırında, dinamik ve öncelikli güncelleme. Düşük stoklu ve hızlı satan ürünler öncelikli olmalı.

---

## 6. Müşteri Yönetimi

### 6.1 Üyelik Yapısı
- Üyelikli ve üyeliksiz alışveriş desteklenmeli
- Üyelik grupları tanımlanabilir (personel dahil)
- Gruplara özel indirim kampanyaları yapılabilir

### 6.2 Veri Gizliliği (KVKK/GDPR)
- Müşteri veri silme talepleri karşılanmalı
- Açık siparişi olan müşteri silme talebi bekletilmeli
- İade sürecindeki siparişler tamamlanana kadar silme bekletilmeli
- Tamamlanmış siparişlerde anonimleştirme yaklaşımı (sipariş kaydı kalır, müşteri bağlantısı kopar)

### 6.3 Müşteri Adresleri
- Adresler: ülke, şehir, ilçe, mahalle, detay alanları
- Mahalle bilgisine göre kargo şirketi belirleniyor
- Adres geçerliliği kontrolü gerekli (şu an manuel)

**Hedef:** Yapılandırılmış adres seçimi. Türkiye için mahalle/sokak/bina veritabanı entegrasyonu. Yurt dışı adresleri için farklı yaklaşım gerekebilir.

**Önemli Kural:** Sipariş verildiğinde adres siparişe kopyalanmalı. Müşteri sonradan adresini değiştirse bile sipariş adresi değişmemeli.

### 6.4 Müşteri İstekleri
- Sipariş notları yapılandırılmış seçeneklerle alınacak
- Paketleme tercihleri ayrı
- Teslimat notları ayrı (kargo firmasına iletilecek şekilde)
- Serbest metin alanı "diğer" seçeneği altında kalabilir

---

## 7. Sipariş Yönetimi

### 7.1 Sipariş Veri Yapısı
Mevcut yapı (değerlendirilecek):
- orders: Ana sipariş kaydı
- orderlines: Sipariş kalemleri
- orderdiscounts: İndirimler
- orderexpenses: Masraflar (sipariş veya ürün bazlı olabilir)
- ordertaxes: Vergiler (orderlines ve orderexpenses kayıtları için)

**Not:** orderexpenses tablosunda orderLineId=NULL ise sipariş genelini ilgilendiren masraf, değilse ürüne özel masraf (montaj gibi).

### 7.2 Ödeme Yöntemleri
- Kredi kartı ile ödeme
- Kapıda nakit ödeme
- Kapıda kredi kartı ile ödeme
- Havale ile ödeme
- Bakiye ile ödeme (kısmi veya tam)
- Hediye çeki ile ödeme
- Personel için maaştan kesim
- Sadakat puanları ile ödeme
- Firma çalışan çekleri ile ödeme

**Kısmi Ödeme:** Birden fazla ödeme yöntemi kombine edilebilir (bakiye + kart + puan gibi).

### 7.3 Ödeme Onayı
- Üyelikli + kredi kartı: İlk siparişte SMS/e-mail onayı
- Üyeliksiz: Her zaman onay
- Kapıda ödeme: Her zaman onay

### 7.4 Sipariş Akışı
1. Sipariş oluşturma
2. Stok rezervasyonu (anlık)
3. Tüm platformlarda stok güncelleme tetikleme
4. Adres geçerlilik kontrolü (isValid=false ise bekletme)
5. Fatura tipi belirleme (GİB sorgusu, invoiceTypeChecked=false ise bekletme)
6. Müşteriye bilgilendirme (SMS + varsa e-mail)
7. Toplama planlamasına dahil edilme
8. Toplama
9. Ayrıştırma
10. Paketleme
11. Fatura/etiket basımı
12. Kargo firmasına bildirim
13. Kargo teslimi

### 7.5 Fatura Yönetimi
- e-Arşiv, e-Fatura, ihracat faturası ayrımı otomatik
- Fatura tipine göre farklı seri numaraları
- GİB API ile mükellefiyet kontrolü
- Kurumsal bilgi girilmemişse e-arşiv, yurt dışı ise ihracat olarak işaretleniyor

**Fatura Zamanlaması (Açık Konu):** Şu an paket hazırlandığında kesiliyor. İptal durumunda gereksiz evrak işi çıkıyor. Kargoya teslim anında kesmek değerlendirilebilir.

**Fiziksel Çıktı (Açık Konu):** Şu an fatura çıktısı pakete konuyor. Sadece etiket koymak operasyonel yükü azaltır. e-Fatura sisteminde elektronik iletimin yeterliliği araştırılmalı.

---

## 8. Depo Operasyonları

### 8.1 Temel Prensipler
- Personel sipariş detaylarını bilmeden çalışabilmeli
- Sesli yönlendirme sistemi
- Minimum ekran bağımlılığı
- Hata oranını minimize eden akış

### 8.2 Toplama Planlaması
Kıdemli personel tarafından yapılıyor. Kullanılabilir filtreler:
- Tek ürün içeren siparişler
- Belirli ile gönderilecekler
- Dropshipping bayilerin siparişleri (satıcı bazlı)
- Kargo şirketi bazlı
- Belirli depolardan ürün içerenler

### 8.3 Toplama Türleri

**Tek Ürün Siparişleri:** Okutulan ürün direkt paketleniyor. Ara süreç yok.

**Toplu Toplama:** Binlerce sipariş birleştiriliyor. Depo konumlarına göre ürünler sıralanıyor. Kat/kısım bazlı personel dağılımı.

### 8.4 Ayrıştırma Süreci

**Ara Ayrıştırma:**
- 50'şer siparişlik koliler kullanılıyor
- Koliler tamamen sanal - ihtiyaca göre otomatik tanımlanıyor
- Tüm ürünlerin gelmesi beklenmiyor
- Hazır olan arabalardan başlanıyor
- Ürünleri tamam olan siparişler küçük numaralı kolilere
- Eksik ürünlü siparişler büyük numaralı kolilere
- Dar alanda binlerce ürün yönetilebiliyor

**Son Ayrıştırma:**
- Personel kendi masasında çalışıyor
- 20 gözlü raf sistemi
- Ürün tamam olan gözler hemen paketleniyor
- Boşalan göz yeniden kullanıma giriyor

### 8.5 Sesli Yönlendirme
- Koli ve raf göz numaraları sesli bildiriliyor
- Son ürün okutulduğunda "fatura" komutu
- Müşteri notu varsa personel uyarılıyor

### 8.6 Paketleme ve Kargo
- Son okutmada otomatik etiket/fatura basımı
- Kargo şirketi çıktı üzerinde belirtili
- Kargo konteynerlerine paket atılıyor
- Arka plan process kargo API'sine bildirim gönderiyor
- Yanlış konteynere atılan paket sisteme takılıyor (barkod eşleşmez)

### 8.7 OBM (Ortak Birleştirme Masası)
- Bulunamayan ürünlü siparişler buraya aktarılıyor
- Ana akış aksatılmıyor
- OBM sorumlusu çözüm üretiyor
- Oran: %1-2 (tamamen personel hatası kaynaklı)
- Genelde aynı gün çözülüyor

### 8.8 Toptancı Hibrit Modeli
- Tek siparişte binlerce ürün olabilir
- 20-30 kolilik kargolar
- Kendi depodan tek tek toplama
- Komşu tedarikçilerden gelen karışık ürünler ayrıştırılıyor
- İki akış senkronize ilerliyor

---

## 9. İade ve Cüzdan Yönetimi

### 9.1 İade Türleri
- Teslim edilemeyen paketler
- Teslim alınmayan paketler
- Teslim sonrası tam iade
- Teslim sonrası kısmi iade
- Zamana yayılmış iade (aynı siparişten farklı zamanlarda)

### 9.2 Cüzdan Yapısı
- İade tutarları geçici olarak cüzdanda saklanıyor
- Yasal olarak önceden bakiye yüklenemez
- Müşteri iade isterse en kısa sürede ödeniyor
- Değişim isterse yeni siparişte cüzdandan ödeme yapabilir
- Yeni sipariş tamamen farklı ürünler de olabilir

**Sorun:** Unutulan bakiyeler birikebiliyor. Hatırlatma mekanizması gerekli. Muhasebe tarafı netleştirilmeli.

---

## 10. Kurum İçi Transfer Sistemi

### 10.1 Genel Prensip
Her türlü ürün hareketi tek bir yapıyla yönetilmeli:
- Stüdyoya gönderim
- Stüdyodan dönüş
- Depolar arası transfer
- Terziye/ütüye gönderim
- Kargosu yapılamayan ürünlerin depoya dönüşü
- Tedarikçiye iade için toplama
- Pazarlama birimine gönderim
- Defo/bağış çıkışı

### 10.2 Temel Akış
Kaynak konum → Toplama talebi → Toplama → Çıkış → Transfer → Hedef konum → Teslim alma → Yerleştirme

### 10.3 Detay Seviyesi Esnekliği
- Bazı kurumlar sadece çıkış-giriş kaydı ister
- Bazıları her el değiştirmeyi takip etmek ister
- Sistem her ikisini de desteklemeli

### 10.4 Stüdyo Süreci
- Ürün kabul aşamasında veya sonrasında stüdyo için ayrılabilir
- Manken cinsiyeti ve beden ölçüsüne göre ürün seçimi
- Yeniden çekim için elle ürün ekleme
- Barkod okutarak çekim başlıyor
- Fotoğraf dosyalarına barkod isim olarak veriliyor
- Otomatik fotoğraf-ürün eşleşmesi
- Fotoğrafı çekilmeden ürün satışa açılmamalı (yetkili istisnası var)
- Bazı ürünler aksesuar olarak kalıcı stüdyoda kalabilir

### 10.5 Transfer Takibi
- Tüm aşamalar raporlanıyor
- Amaç: Depo harici hiçbir birimde ürün kalmaması
- Açık transferler net görülebilmeli

---

## 11. Tedarik ve Cari Yönetimi

### 11.1 Kapsam
Mevcut ERP'den devralınacak, temel düzeyde tutulacak.

### 11.2 Tedarikçi Kayıtları
- Tedarikçi bilgileri tutulacak
- Cari hesap takibi (borç, alacak, bakiye)

### 11.3 Satın Alma
- Personel inisiyatifinde
- Raporlar ve filtreler satın almacıya bilgi sağlıyor
- Otomatik satın alma önerisi şimdilik kapsam dışı

### 11.4 Fatura Girişi
- Basit yapı: ürün, miktar, fiyat, KDV, iskonto
- Fatura ile fiili teslimat ayrı takip edilecek
- Aralarında gevşek bağlantı kurulabilecek

**Önemli:** Tedarikçi "10.000 adet t-shirt" faturası keserken depoya 200 farklı stok kartı girebilir. İkisi farklı granülerlikte.

### 11.5 Ödeme Takibi
- Nakit, kredi kartı, çek, senet türleri
- Enstrüman detayları şimdilik kapsam dışı
- Temel borç/alacak/ödeme takibi yeterli

### 11.6 Tedarikçiye İade
- Genelde satılamayan ürünler için
- Kalite kontrolde tespit edilen defolar sisteme girmeden iade
- Sonradan tespit edilen defolar defo deposuna, oradan çıkış

### 11.7 Kalite Kontrol
- Ürün kabulünde yapılıyor
- Defolu ürünler sisteme sokulmadan iade
- Sistemdeki defolar defo deposuna transfer

---

## 12. Defo ve Bağış Çıkışları

- Defolu ürünler defo deposuna transfer ediliyor
- İmha veya bağış kararı yetkililere ait
- Transfer sistemiyle yönetilebilir (hedef: defo deposu veya bağış çıkışı)
- Muhasebe tarafı: maliyet kaydı, zarar yazma (netleştirilmeli)

---

## 13. Kampanya ve Promosyon Yönetimi

### 13.1 Yapı
- Kampanya tipleri ve kuralları yazılım ekibi tarafından tanımlanıyor
- Kampanya sorumlusu mevcut tiplerden seçip konfigüre ediyor
- Kampanya adı, geçerlilik süreleri belirleniyor
- Ürünler filtre ile veya elle ekleniyor

### 13.2 Uygulama Mantığı
- Öncelik sırasına göre indirim hesaplama
- Bir ürüne birden fazla farklı tip kampanya uygulanabilir
- Aynı tip kampanyadan bir ürün sadece birine dahil olabilir

### 13.3 Genişletilebilirlik
**Hedef:** Plugin mantığıyla yeni kampanya tipleri eklenebilmeli. Ana koda müdahale gerekmemeli.

---

## 14. Raporlama

### 14.1 Mevcut Durum
- Dinamik raporlama mevcut
- Kullanıcılar sık kullandıkları raporları saklayabiliyor
- Filtre bazlı rapor tanımlama
- Satış raporları ağırlıklı kullanılıyor

### 14.2 Hedef
Yapay zeka destekli raporlama. Kullanıcı konuşma diliyle rapor talep edebilmeli.

**Dikkat Edilecekler:**
- Veri güvenliği (kullanıcı yetkisi dışına çıkılmamalı)
- Performans (kötü sorguların sistemi kilitleme riski)
- Doğruluk (yanlış yorumlama riski)

---

## 15. Entegrasyonlar

### 15.1 Pazaryeri Entegrasyonları
**Mevcut Sorun:** Pazaryerleri sık değişiklik yapıyor. Takibi ve uygulaması zor.

**Hedef:** Ana sistemden tamamen izole entegrasyon katmanı. Pazaryeri değişikliği sadece ilgili adaptörü etkilesin.

### 15.2 Kargo Entegrasyonları
- Birden fazla kargo firması
- Mahalle bazlı kargo firması ataması (ana ölçüt)
- Ödeme tipine bağlı kargo firması seçimi:
  - Kapıda ödeme gerektirmeyen siparişler → Kargo A
  - Kapıda kredi kartı ödemeli siparişler → Kargo B
  - Kapıda nakit ödemeli siparişler → Kargo C
- Kargo şirketi sözleşmeleri ve hizmet kapasiteleri dikkate alınıyor
- Bu tercihlerin kolay ve otomatik yönetilmesi gerekli
- Performans ve maliyet değerlendirmesi

### 15.3 Ödeme Sistemleri
- Kredi kartı entegrasyonları
- Sanal POS

### 15.4 e-Fatura Entegratör Entegrasyonları
- Mükellefiyet kontrolü ve fatura gönderimi entegratör firmalar üzerinden yapılıyor
- Doğrudan GİB entegrasyonu yok
- Farklı firmalar için farklı entegratör firmalarla çalışılabiliyor
- Entegratör firma API'lerine entegrasyon gerekli
- Desteklenmesi gereken işlemler:
  - Mükellefiyet sorgulama
  - e-Fatura gönderimi
  - e-Arşiv fatura gönderimi
  - İhracat faturası gönderimi

### 15.5 Mimari Hedef
Plugin/adapter bazlı yapı. Yeni entegrasyon eklemek mevcut kodda değişiklik gerektirmemeli.

---

## 16. Kullanıcı ve Yetki Yönetimi

### 16.1 Kullanıcı Bilgileri
- Ad, birim, görev
- Sistem yetkileri
- Maaş/özlük bilgileri kapsam dışı

### 16.2 Yetkilendirme Yapısı
Çok katmanlı yetkilendirme gerekli:
- Sayfa bazlı erişim
- İşlem bazlı erişim (butonlar, aksiyonlar)
- Veri bazlı erişim (fiyatları görme, firma verileri)
- Kapsam bazlı erişim (bölge, firma sınırlaması)

### 16.3 Mevcut Yaklaşım
- Grup bazlı yetki dağıtımı
- Kişiye özel yetki çıkarma mümkün

### 16.4 Değerlendirilecek Yaklaşım
- Rol bazlı temel yetkiler
- Birden fazla rol atanabilir (yetkiler birleşir)
- Çıkarma yerine ekleme mantığı
- Çok spesifik durumlar için kişiye özel ek yetki

---

## 17. Açık Konular ve Kararlar

### 17.1 Netleştirilecek Konular
- [ ] Fatura kesim zamanlaması (paket hazır mı, kargoya teslim mi?)
- [ ] Paketle fiziksel belge gönderme zorunluluğu
- [ ] Cüzdan bakiyelerinin muhasebe kaydı
- [ ] Yurt dışı adres doğrulama yaklaşımı
- [ ] Defo/bağış çıkışlarının muhasebe kaydı

### 17.2 Teknoloji Kararları (Sonraki Aşama)
- [ ] Backend teknolojisi
- [ ] Frontend teknolojisi
- [ ] Veritabanı seçimi
- [ ] Kuyruk sistemi
- [ ] Önbellek sistemi
- [ ] Entegrasyon mimarisi detayları

---

## 18. Sonraki Adımlar

1. Teknoloji seçimleri
2. Veritabanı tasarımı
3. Modüler mimari tasarımı
4. API tasarımı
5. Entegrasyon katmanı tasarımı
6. Yetkilendirme sistemi tasarımı
7. Geliştirme fazları planlaması

---

## Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 1.0 | Ocak 2025 | İlk versiyon - Beyin fırtınası sonuçları |
| 1.1 | Ocak 2025 | Çok dilli yapı, kargo seçim kuralları, sanal ayrıştırma kolileri, entegratör firma bilgisi eklendi |
