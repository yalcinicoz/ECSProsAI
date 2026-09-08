Yeni panelde kullanılan tablo yapılarını daha kullanışlı, responsive ve operasyonel kullanıma uygun hale getirmek istiyorum.

Buradaki amaç sadece Bootstrap tablosuna birkaç özellik eklemek değil; panel genelinde kullanılabilecek ortak, güçlü ve kullanıcı dostu bir **DataGrid / gelişmiş tablo standardı** oluşturmaktır.

Öncelikle mevcut tablo yapılarını ve kullanılan ortak component/helper yapılarını incele. Mevcut sistemi mümkün olduğunca koruyarak nasıl iyileştirilebileceğini analiz et.

İlk aşamada doğrudan kod yazmaya başlama. Önce mevcut yapıyı incele, aşağıdaki ihtiyaçlara göre önerdiğin mimariyi ve UX davranışlarını netleştir. Eğer mevcut sistemde bu özelliklerden bazıları zaten varsa tekrar geliştirmek yerine onları kullan.

## 1. Responsive ve yatay kaydırma

Çok sütunlu tablolar masaüstü, tablet ve mobil ekranlarda kullanılabilir olmalıdır.

Ekran genişliği tabloyu göstermeye yetmediğinde tablo yatay olarak kaydırılabilmelidir.

Kullanıcı hiçbir sütuna erişimini kaybetmemelidir.

Mobil görünümde sabit/frozen sütunlar kalan verilerin görüntülenmesini engellememelidir.

Frozen kolonların toplam genişliği ekranın önemli bölümünü kaplıyorsa sistem responsive olarak:

- sabit kolon sayısını azaltmalı,
- yalnız en kritik kolonu sabit bırakmalı,
- veya gerektiğinde sticky/frozen davranışını tamamen kapatmalıdır.

Amaç şudur:

**Sabit sütun kullanıcıya yardımcı olmalı, diğer sütunlara ulaşmasını engellememelidir.**

Mobil görünümde özellikle tüm görünür kolonlara erişim korunmalıdır.

## 2. Uzun tablolarda sürekli erişilebilir yatay scroll

Bu konu özellikle önemlidir.

Masaüstünde çok sütunlu ve çok satırlı tablolarda yatay scrollbar yalnız tablonun en altında bulunmamalıdır.

Örneğin tabloda 50, 100 veya daha fazla satır varsa kullanıcı sağ taraftaki kolonlara ulaşabilmek için önce tablonun en altına kadar inmek zorunda kalmamalıdır.

Bu kötü kullanıcı deneyimi oluşturur.

Tablo kullanıcı ekranında görünür olduğu sürece yatay kaydırma kontrolüne kolayca ulaşılabilmelidir.

Tercihen:

- viewport altında sticky/floating bir yatay scrollbar kullanılabilir,
- kullanıcı tablonun ortasındayken de sağa/sola kaydırabilmelidir,
- bu scrollbar tablonun gerçek yatay scroll pozisyonu ile tamamen senkron çalışmalıdır,
- kullanıcı gerçek alt scrollbar'ı kullandığında sticky scrollbar da aynı pozisyona gelmelidir,
- kullanıcı sticky scrollbar'ı kullandığında tablo aynı şekilde kaymalıdır,
- tablo viewport dışına çıktığında veya tablo sona erdiğinde gereksiz sticky scrollbar görünmemelidir.

Amaç:

**Kullanıcı sağdaki kolonları görmek için hiçbir zaman tablonun son satırına gitmek zorunda kalmamalıdır.**

Normal tablonun kendi alt scrollbar'ı korunabilir; sticky scrollbar bunun yerine değil, erişilebilirliği artırmak için ek bir kontrol olarak düşünülebilir.

Alternatif olarak üst tarafta ikinci bir senkron yatay scrollbar düşünülebilir ancak bunun görsel kalabalık yaratıp yaratmayacağını değerlendir.

Varsayılan tercih, mümkünse:

**Sticky bottom horizontal scrollbar**

olmalıdır.

Touchpad ile doğal yatay kaydırma davranışları da düzgün çalışmalıdır.

Shift + mouse wheel gibi masaüstü yatay scroll davranışları desteklenebiliyorsa mevcut tarayıcı davranışları bozulmamalıdır.

Ancak kullanıcıların bunu bilmesi beklenmemeli; ana çözüm görünür bir yatay scroll kontrolü olmalıdır.

Ayrıca kullanıcıya tablonun sağında veya solunda başka kolonlar olduğunu göstermek için gerektiğinde hafif görsel ipuçları değerlendirilebilir.

Örneğin:

- sağ tarafta daha fazla kolon varsa hafif bir sağ kenar gölgesi,
- kullanıcı en sağa geldiğinde bu gölgenin kaybolması,
- sola kaydırılmış durumda solda benzer bir görsel ipucu

kullanılabilir.

Bunun amacı kullanıcıya:

**“Bu tablo yatay olarak devam ediyor.”**

bilgisini sezgisel şekilde vermektir.

## 3. Kritik / Frozen sütunlar

Bazı tablolarda kullanıcı sağa kaydırırken hangi kayıt üzerinde olduğunu kaybetmemelidir.

Örneğin Sipariş Listesi gibi bir ekranda:

- Sipariş No
- Müşteri
- Durum

gibi kritik kolonlardan uygun olanlar masaüstünde sabit tutulabilir.

Ancak bu davranış responsive olmalıdır.

Genel yaklaşım:

- Masaüstü: birden fazla kritik kolon sabitlenebilir.
- Tablet: sabit kolon sayısı azaltılabilir.
- Mobil: yalnız çok dar bir kritik kolon sabit kalabilir veya frozen kolon tamamen kaldırılabilir.

Bu davranış tablo bazında configurable olmalıdır.

## 4. Server-side filtreleme

Filtre sistemi en önemli konudur.

Filtreleme yalnız tarayıcıya yüklenmiş mevcut sayfa üzerinde yapılmamalıdır.

Filtreler doğrudan backend / database sorgusuna uygulanmalıdır.

Örneğin ekranda yalnız 50 kayıt görünse bile kullanıcı müşteri adı, sipariş numarası, durum veya tarih filtresi verdiğinde sorgu gerçek veri kaynağında çalışmalıdır.

Büyük veri setlerinde de performanslı çalışabilecek server-side filtering mantığı kullanılmalıdır.

## 5. Filtre tipleri

Kolon tipine göre uygun filtre tipi kullanılmalıdır.

Metin alanlarında:

- içerir
- eşittir
- ile başlar

gibi ihtiyaçlara uygun arama yapılabilmelidir.

Sipariş No, ID, barkod gibi alanlarda gerektiğinde tam eşleşme hızlı şekilde kullanılabilmelidir.

Enum / durum alanlarında:

- dropdown
- gerekirse çoklu seçim

desteklenmelidir.

Tarih alanlarında:

- Bugün
- Dün
- Son 7 gün
- Son 30 gün
- Bu ay
- Geçen ay
- Özel tarih aralığı

gibi hızlı seçenekler bulunmalıdır.

Sayısal alanlarda:

- eşittir
- büyük
- küçük
- belirli aralık

gibi filtreleme seçenekleri düşünülmelidir.

Boolean alanlarda:

- Tümü
- Evet
- Hayır

mantığı kullanılmalıdır.

## 6. Global arama + kolon filtreleri

Tablonun üzerinde hızlı bir global arama bulunmalıdır.

Global arama birden fazla uygun alanda arama yapabilmelidir.

Örneğin kullanıcı tek kutuya:

- Sipariş No
- Müşteri
- Telefon
- Barkod
- Kargo takip numarası

gibi bir değer yazabilir.

Bunun yanında kolon bazlı filtreler de ayrıca çalışmalıdır.

Global arama ve kolon filtreleri aynı anda kullanılabilmelidir.

## 7. Hızlı filtreler ve gelişmiş filtreler

Tablo ekranını onlarca filtre alanıyla doldurmak istemiyorum.

En sık kullanılan filtreler doğrudan görünür olmalıdır.

Örneğin:

- Durum
- Tarih
- Arama
- Ödeme durumu
- Kargo durumu

gibi alanlar hızlı filtre olarak kullanılabilir.

Daha az kullanılan filtreler ise ayrı bir **Gelişmiş Filtreler** alanı/paneli üzerinden erişilebilir olmalıdır.

Bu ayrım tablo bazında değişebilmelidir.

## 8. Aktif filtrelerin görünürlüğü

Kullanıcı hangi filtrelerin uygulandığını kolayca görebilmelidir.

Örneğin:

Durum: Hazırlanıyor  
Tarih: Son 7 gün  
Tutar: > 1000 TL

gibi aktif filtreler badge/chip şeklinde gösterilebilir.

Her filtre ayrı ayrı kaldırılabilmelidir.

Ayrıca tek işlemle:

**Tüm filtreleri temizle**

seçeneği bulunmalıdır.

Kullanıcı görünmeyen veya unutulmuş bir filtre nedeniyle eksik veri gördüğünü düşünmemelidir.

## 9. Filtre davranışı ve performans

Metin aramalarında gereksiz yere her tuş vuruşunda database sorgusu çalıştırılmamalıdır.

Kullanıcı deneyimini bozmayacak şekilde debounce veya uygun bir arama tetikleme mantığı kullanılmalıdır.

Filtreleme sırasında tablo tamamen kullanılamaz hale gelmemeli ve kullanıcıya yüklenme durumu net gösterilmelidir.

## 10. Sorting

Kolon bazlı sıralama server-side çalışmalıdır.

Özellikle:

- tarih
- sipariş no
- tutar
- müşteri
- durum

gibi alanlarda sorting desteklenmelidir.

Aktif sıralama kullanıcı tarafından açık şekilde görülebilmelidir.

## 11. Pagination

Pagination server-side olmalıdır.

Kullanıcı uygun kayıt sayısı seçeneklerinden seçim yapabilmelidir.

Örneğin:

- 25
- 50
- 100
- 250

Tablonun altında/üstünde:

- toplam kayıt sayısı
- filtrelenmiş kayıt sayısı
- mevcut sayfa

anlaşılır şekilde gösterilmelidir.

## 12. Kolon göster / gizle

Kullanıcı ihtiyacı olmayan kolonları tablodan gizleyebilmelidir.

Bir **Kolonlar** menüsünden hangi kolonların görüntüleneceğini seçebilmelidir.

Örneğin:

☑ Sipariş No  
☑ Durum  
☑ Müşteri  
☑ Tutar  
☐ Telefon  
☐ Adres  
☐ Fatura No

gibi.

Kritik kolonların tamamen gizlenip gizlenemeyeceği tablo bazında tanımlanabilmelidir.

## 13. Kolon sırası

Mümkünse kullanıcı kolonların sırasını değiştirebilmelidir.

Örneğin kullanıcı sürekli kullandığı Kargo Durumu kolonunu daha öne alabilmelidir.

Bu özellik UX veya mevcut teknoloji açısından gereksiz karmaşıklık oluşturuyorsa önce değerlendir ve önerini belirt.

## 14. Kullanıcı tercihlerinin korunması

Kullanıcının tablo tercihleri mümkünse korunmalıdır.

Örneğin:

- hangi kolonları gizlediği
- kolon sırası
- sayfa başına kayıt sayısı
- gerekiyorsa frozen kolon tercihleri

tekrar aynı tabloya geldiğinde korunabilir.

Ancak kalıcı filtrelerin kullanıcıyı şaşırtma ihtimalini ayrıca değerlendir.

Eğer filtreler korunacaksa aktif filtrelerin çok net şekilde görünmesi zorunludur.

## 15. Kaydedilmiş görünümler

İleri seviye olarak kullanıcı bir tablo görünümünü kaydedebilmelidir.

Örneğin:

- Depo
- Muhasebe
- Müşteri Hizmetleri
- Bekleyen Siparişler
- Kargoya Hazır Siparişler

gibi.

Kaydedilen görünüm şunları içerebilir:

- kolon görünürlüğü
- kolon sırası
- filtreler
- sorting
- page size

Bu özelliğin mevcut sisteme uygunluğunu değerlendir.

## 16. Mobil kullanım

Mobilde masaüstündeki tabloyu birebir küçültmeye çalışma.

Mobil kullanım ayrıca düşünülmelidir.

Öncelik:

1. Kritik verilere hızlı erişim
2. Diğer verilere erişimin hiçbir zaman kaybolmaması
3. Frozen kolonların ekranı kaplamaması
4. Filtrelerin kolay kullanılabilmesi
5. Gereksiz yatay alan tüketiminin önlenmesi

Gerekirse mobilde alternatif bir **Kompakt Görünüm** değerlendirilebilir.

Örneğin ana satırda yalnız:

- Sipariş No
- Durum
- Tutar
- Tarih

görünüp, satıra dokunulduğunda diğer bilgiler açılabilir.

Ancak klasik tam tablo görünümü de gerekiyorsa kullanıcı yatay kaydırarak tüm kolonlara erişebilmelidir.

Kompakt görünüm ile Tam Tablo görünümü arasında geçiş yapılmasının faydalı olup olmayacağını değerlendir.

Tabloları tamamen karta dönüştürme yaklaşımını otomatik olarak uygulama. Özellikle operasyonel ekranlarda satırlar arası karşılaştırma önemli olduğu için tablo yapısının avantajlarını koru.

## 17. Kolon öncelikleri

Her kolon için önem seviyesi tanımlanabilmesi faydalı olabilir.

Örneğin:

Priority 1:
Her zaman mümkün olduğunca görünür.

Priority 2:
Alan varsa görünür.

Priority 3:
Detay/veri yoğun kolonlar.

Responsive davranış buna göre yönetilebilir.

Ancak kullanıcı manuel olarak kolon açmışsa erişimi engellenmemelidir.

## 18. Excel export

Excel aktarımı çok önemlidir.

Excel'e aktar işlemi yalnız mevcut sayfadaki kayıtları export etmemelidir.

Örneğin:

Filtre sonucu toplam 4.268 kayıt varsa ve ekranda pagination nedeniyle yalnız 50 kayıt görünüyorsa:

**Excel çıktısında filtreye uyan 4.268 kaydın tamamı bulunmalıdır.**

Export sırasında:

- aktif filtreler
- global search
- sıralama gerekiyorsa
- veri kapsamı

korunmalıdır.

Ancak:

- mevcut sayfa numarası
- pagination limiti

Excel export sonucunu kısıtlamamalıdır.

Listeleme ve Excel export mümkün olduğunca aynı filtre modelini kullanmalıdır; böylece ekranda görülen filtre ile Excel sonucu arasında tutarsızlık oluşmamalıdır.

Ayrıca şu seçeneğin faydalı olup olmadığını değerlendir:

- Tüm kolonları Excel'e aktar
- Yalnız görünür kolonları Excel'e aktar

## 19. Ortak DataGrid standardı

Bu özellikleri her sayfada ayrı ayrı yazmak istemiyorum.

Panel genelinde kullanılabilecek ortak bir tablo/DataGrid yapısı oluşturulmalıdır.

Her tablo yalnız kendi ayarlarını tanımlamalıdır.

Örneğin tablo bazında şu davranışlar değişebilir:

- hangi kolonlar searchable
- hangi kolonlar sortable
- hangi filtre tipi kullanılacak
- hangi kolonlar varsayılan görünür
- hangi kolonlar frozen
- mobil priority
- Excel export açık/kapalı
- hızlı filtreler
- gelişmiş filtreler

Ama temel UX ve davranış panel genelinde standart olmalıdır.

## 20. Mevcut sistemi bozma

Bu çalışma sırasında mevcut panel tasarımını tamamen değiştirme.

Mevcut Bootstrap tasarım dili ve mevcut component sistemi mümkün olduğunca korunmalıdır.

Amaç yeni bir tasarım framework'ü getirmek değil, mevcut paneldeki tabloları profesyonel bir DataGrid seviyesine çıkarmaktır.

Mevcut çalışan:

- CRUD işlemleri
- action butonları
- satır seçimleri
- modal işlemleri
- linkler
- yetkilendirmeler
- mevcut API/backend işlemleri

bozulmamalıdır.

## 21. Önce analiz yap

İlk cevapta doğrudan tüm kodları üretme.

Önce:

1. Mevcut tablo altyapısını incele.
2. Ortak kullanılan table/component/helper yapılarını tespit et.
3. Yukarıdaki özelliklerden hangilerinin mevcut olduğunu belirle.
4. Eksikleri listele.
5. Önerdiğin ortak DataGrid mimarisini anlat.
6. Mobil, masaüstü ve tablet davranışlarını açıkla.
7. Filtre mimarisini özellikle detaylandır.
8. Excel export yaklaşımını açıkla.
9. Uzun tablolarda sticky yatay scroll yaklaşımını ayrıca değerlendir.
10. Mevcut sisteme minimum müdahaleyle nasıl uygulanabileceğini belirt.
11. Riskli veya mevcut davranışı bozabilecek noktaları ayrıca belirt.

Ben onay verdikten sonra uygulamaya geç.

Ana hedef:

**Çok büyük veri tablolarında bile hızlı çalışan, mobilde kullanılabilir, güçlü server-side filtreleme sunan, kullanıcı tarafından kişiselleştirilebilen, uzun tablolarda yatay kaydırma kontrolüne her zaman kolay erişim sağlayan ve filtrelenmiş verinin tamamını doğru şekilde Excel'e aktarabilen ortak bir panel DataGrid standardı oluşturmak.**