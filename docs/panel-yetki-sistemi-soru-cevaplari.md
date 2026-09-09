K1-K8 kararlarını netleştiriyorum. Bunları nihai iş kuralları olarak kabul et ve artık brief'teki A-O tasarım çıktısını üret.

## K1 — Kapsam birimi

**Kanal olacak.**

Yetkilendirme v1'de firma + kanal şeklinde çift kapsamlı olmayacak.

Kullanıcının yetkileri doğrudan satış kanalı bazında değerlendirilecek:

- Gülseli
- Julude
- OlurButik
- Mishar Italia
- Tozlu
- vb.

Bir kanalın hangi firmaya ait olduğu zaten sistemden türetilebilir.

Firma seviyesini ayrı bir permission scope hâline getirerek modeli karmaşıklaştırma.

İleride firma seviyesinde toplu yetkilendirme ihtiyacı çıkarsa UX tarafında firmaya bağlı tüm kanalların toplu seçimi olarak değerlendirilebilir.

---

## K2 — Kanal kapsamı listeleri filtreleyecek mi?

**EVET, kesinlikle.**

Kullanıcının erişemediği satış kanalına ait veri hiçbir yüzeyden sızmamalıdır.

Bu kapsam:

- liste,
- arama,
- detay,
- dashboard,
- durum sayaçları,
- rapor,
- Excel/CSV export,
- toplu işlem,
- API cevabı

için geçerlidir.

Örneğin kullanıcı Julude kanalına erişemiyorsa Julude siparişini yalnızca açamaması değil, sipariş listesinde dahi görmemesi gerekir.

---

## K3 — Yetki değişikliği ne zaman etkili?

**ANINDA etkili olmalı.**

İş kuralımız:

Yetki verdim → hemen kullanabilir.

Yetkiyi kaldırdım → hemen kullanamaz.

60 dakika veya token ömrü boyunca eski yetkinin devam etmesi kabul edilmez.

Bunun teknik çözümünü tasarımda öner ancak şu aşamada kod yazma.

---

## K4 — Permission kataloğu

**Kod sahipli katalog yaklaşımını kabul ediyorum.**

Claude'un "panelden oluşturulan ancak hiçbir endpoint'e bağlı olmayan hayalet permission" uyarısı doğru.

Teknik permission'ın sistemde gerçek karşılığı developer/kod tarafından tanımlanmalıdır.

Ancak eski panelimizin kullanım rahatlığını korumak istiyorum.

Panelden şunlar yönetilebilmeli:

- görünen permission adı,
- açıklama,
- bağlı sayfa/modül,
- sıralama,
- aktif/pasif,
- permission türü,
- kanal kapsamlı olup olmadığı.

Teknik key immutable olmalı.

İstersen ayrıca "permission taslağı" kavramını değerlendir:

Panelden yeni bir permission ihtiyacı taslak olarak oluşturulabilir ancak kod tarafında karşılığı oluşana kadar kullanıcılara/gruplara atanamaz.

Bu gerçekten faydalıysa öner, gereksizse ekleme.

---

## K5 — Super Admin

**Super admin kullanıcı üzerinde ayrı ve belirgin bir sistem özelliği olacak.**

Normal permission listesinde `*` gibi bir permission olarak modellenmemeli.

Super admin:

- tüm permission kontrollerini bypass eder,
- tüm kanalları görür,
- tüm field'ları görür,
- tüm aksiyonları kullanır.

Ancak bütün işlemleri audit edilir.

Ayrıca:

- sistemde en az bir super_admin kalmalı,
- super_admin verme/kaldırma özel audit olayı olmalı,
- super_admin sayısı görülebilmeli.

Kullanıcının kendi super_admin durumunu kaldırıp kaldıramaması konusunu güvenlik açısından değerlendir ve en sade güvenli kuralı öner.

---

## K6 — Field yetkileri

**V1'e girecek ancak sınırlı başlayacak.**

İlk etapta gerçekten hassas alanlarla sınırlı olsun:

- maliyet,
- kâr,
- müşteri telefonu,
- müşteri adresi,
- personel/özel notlar.

Her kolon ve her input için permission üretme.

Field permission'ı olmayan kullanıcıya veri backend tarafından mümkünse hiç gönderilmemeli.

Export tarafı da aynı kurala tabi olmalı.

---

## K7 — Geçici yetki

**V1'de olmayacak.**

Gelecekte eklenebilir ancak şu an UI'ya veya kullanıcı akışına ekleme.

Mimariyi ileride eklenmesine engel olmayacak şekilde düşünmek yeterlidir.

---

## K8 — Mevcut kullanıcı geçişi

Mevcut kullanıcıları bir anda default deny ile sıfır yetkiye düşürüp canlı paneli durdurmayacağız.

Geçiş yaklaşımı:

1. Mevcut erişime yakın bir geçiş/legacy yetki grubu oluştur.
2. Mevcut kullanıcıları kontrollü biçimde bu kapsama al.
3. Gerçek departman/yetki gruplarını oluştur.
4. Kullanıcıları doğru gruplara geçir.
5. Kullanıcı bazlı gerekli istisnaları tanımla.
6. En son legacy/geçiş grubunu kaldır.

Bu geçiş rolü kalıcı tasarımın bir parçası olmamalıdır.

---

# Kullanıcı özel istisnası hakkında karar

Buradaki tespitini kabul ediyorum:

Grup tarafından kazanılmış bir permission'ın kullanıcı özelinde kaldırılması teknik olarak bir deny/override davranışıdır.

Ancak bunu sistem genelinde ALLOW/DENY/INHERIT gibi karmaşık bir modele çevirmek istemiyorum.

Kural:

- Gruplar yalnız yetki VERİR.
- Bir grubun permission vermemesi yasak anlamına gelmez.
- Gruplar union edilir.
- Kullanıcıya özel yetki eklenebilir.
- Kullanıcıya özel olarak gruptan gelen bir yetki kaldırılabilir.
- Kullanıcı özel kararı grup sonucundan daha güçlüdür.
- İstisna satış kanalı kapsamlı olabilir.

Örnek:

Sipariş İptal Et:

Gruptan:
Gülseli + Julude + OlurButik

Kullanıcı özel:
Julude kaldırılmış

Efektif:
Gülseli + OlurButik

Panel kullanıcısına ALLOW / DENY / INHERIT gibi teknik seçenekler gösterme.

Onun gördüğü zihinsel model hâlâ:

"Bu kullanıcının bu yetkisi var mı, yok mu?"

olmalı.

---

# Ek karar

"Tüm kanallar" seçimi gelecekte oluşturulacak satış kanallarını otomatik kapsamamalıdır.

Tüm kanallar seçildiği anda mevcut kanalların tamamının seçilmesi anlamına gelsin.

Yarın yeni kanal açılırsa default deny nedeniyle mevcut kullanıcı otomatik olarak erişmemelidir.

---

Artık K1-K8 kararı beklemiyorsun.

Bu kararları kaynak kabul ederek brief'te istenen **A-O nihai tasarım çıktısını üret.**

Kod yazma.

SQL yazma.

Entity/class/API yazma.

Bu aşamada yalnız:

- nihai mimari,
- efektif yetki mantığı,
- edge-case kararları,
- panel UX,
- audit,
- güvenlik,
- performans prensipleri,
- geçiş yaklaşımı

üzerinden ilerle.

Özellikle sistemi gereksiz karmaşıklaştıracak fikirleri ele ve günlük panel kullanımını basit tut.