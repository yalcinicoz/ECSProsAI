# AI raporlama — uygulama hazırlığı

## Faz 7x — sözlük alanı netleştirme koruması (2026-09-11)

- Sonuç: `20260911_ai_clarification_fix` iki API'de Healthy.21hedefli/522API testi geçti,12ortama bağlı test atlandı. Gerçek tarayıcıda asıl istek ve kısa netleştirmeyle eski grup koşulu kaldırıldı;7kolon+stok>=5+Ortam=Tesettür1.549kayıt. Ek tablo>=100filtresi7sonuç verdi ve kaldırıldı. Admin/.56/.59/şema/worker/config değişmedi. Yalnız bu biçim hatası ve netleştirme senaryosu tamamlandı; genel dinamik rapor kapsamındaki diğer açık işler ayrı kalır.

- Devam tanılamasında canlı `unexpected_plan` doğrulandı. Schema decision/clarification/nullable plan alanlarını bağımsız tanımladığı için netleştirmeyle birlikte geçici plan gelebiliyor. Bilinen soru varsa provisional plan atılıp yalnız netleştirme döndürülür; rapor onayı veya çalıştırma üretilmez. Ready+none bütün eski plan doğrulamalarından geçer. Tanılama yalnız sabit hata kodlarıdır, içerik loglanmaz. Resmî structured-output sözleşmesi kontrol edildi: https://developers.openai.com/api/docs/guides/structured-outputs

- Son aktif yayın `20260911_ai_binding_replacement` (API01/API02 ve multi-test admin); iki readiness Healthy, geçici aktarım arşivleri temiz. Yeni admin build gerekmedi; kullanıcının son paketi aynı. API talimat/soru değişikliği Release derlendi ve yayımlandı.
- Yeni sürümde asıl istek yeniden denendiğinde genel model biçim hatası tekrarlandı. Kısa yanıt düzeltmesinin bu sürümde uçtan uca kabulü henüz tamamlanmadı. Sonraki iş: ParseResponse reddinin yapısal sebebini ham kullanıcı/model içeriği veya anahtar loglamadan ayırmak; yalnız tekrar deneyerek kapatılmamalı.

- `20260911_ai_binding_guard` API01/API02 ve multi-test admin yayını tamamlandı; readiness sağlıklı. Tarayıcıda ilk model çağrısı biçim hatası verdi, tekrarında sözlük koruması doğru biçimde Ortam netleştirmesi istedi. Bunun bütün model biçim hatalarını çözdüğü iddia edilmez.
- Ekran kabulünde kısa netleştirme eski productGroup contains koşulunu da korudu; bu yanlış plan çalıştırılmadı. Açık değiştirme isteğiyle yalnız Ortam=Tesettür ve stok>=5, istenen yedi kolonla 1.550 güncel sonuç geldi. Sonuç sayısı canlı stok değiştikçe değişebilir.
- Bu gözlem üzerine model talimatı, netleştirilen özelliğin belirsiz grup koşulunun YERİNE geçtiğini açıkça belirtiyor; ilgisiz kolon/eşikler korunuyor. Koruma sorusu da aynı anlamla düzeltildi. İlgili19test geçti; bu talimat deterministik semantik doğrulamanın yerine geçmez. eq/in koruması contains kapsamını henüz kapsamaz.

- Modelin productGroup eq/in seçimi mevcut grupta yokken izinli özellik seçenekleriyle eşleşirse sunucu hazır planı netleştirmeye çevirir. Özel Tesettür kuralı yok; gerçek grup öncelikli, bilinmeyen değerle boş sonuç alma geçerlidir. Plan otomatik yeniden yazılmaz. Bu koruma bütün doğal dil yorumlama hatalarını çözmez.
- Metadata seçenekleri structured MatchedValues olarak da taşınır. Mevcut global katalog+stok+rapor izinleri, node/depth ve sorgu boyut sınırları korunur. Yalnız sözlük okunur, veri yazılmaz.
- Admin detail/aggregate ortak alanları tekilleştirildi; manuel V1, V2-only alan ve tekrarlanan ölçü üretmez. API516/12skip, admin28/28, gerçek salt-okunur1/1. Build/yayın ekran kabulü bekliyor; yeni tablo/seed yok.

## Faz 7w — dinamik stok detayları ve sayısal koşullar (2026-09-11)

- Yayın ve ekran kabulü: API01/API02 `20260911_ai_stock_envelope`, admin `.56` `20260911_ai_stock_detail`. Gerçek OpenAI cevabındaki zarf boyutu engeli giderildi (ayrı1MiB zarf/64KiB proposal; plan sınırları korunur). Ortam=Tesettür açık netleştirmesiyle>=5 ve istenen7kolon1.551 sonuç; tablo>=100 filtresi AI çağırmadan7sonuç.513 API testi geçti/12opt-in atlandı. İlk çağrıda grup/özellik belirsizliğini model kendisi sormadı; bu kalite eksikliği kapanmış sayılmıyor.

- Stok, V2 ortak detay/özet motorunu kullanır. Barkod, ürün kodu, kart açılışı, ürün grubu ve izinli güncel özellikler seçilebilir. Miktar koşulları ortak predicate üzerinden uygulanır; soruya özel endpoint eklenmedi.
- Varyant kapsamında konum stokları önce varyant ve stok türüne göre toplanır; `>= 5` bu toplama uygulanır. Konum kapsamı ayrıca seçilebilir. Fiziksel/sanal stoklar ayrı tutulur. Özellik birleştirmesi stok satırlarını çoğaltmaz.
- İstekte geçen seçenekler özellik sözlüğünde sunucu tarafında sınırlı aramayla bulunur. İlk denemede bütün seçenekleri yükleme sınırı gerçek veri üzerinde aşıldı; bütün sözlüğü taşımak yerine yalnız metindeki adaylarla eşleşen seçenekleri getiren sorguya geçildi. Gerçek veride `tesettur` ifadesinin `Ortam` seçeneğine karşılığı doğrulandı.
- Salt-okunur .241 kabul testi 1/1 geçti (30 saniye): istenen yedi kolon, stok alt sınırı, bağımsız SQL karşılaştırması, tablo filtresi ve stok türüne göre toplamlar. Production verisine yazılmadı. Bu test gerçek OpenAI konuşması/tarayıcı kabulünün yerine geçmez.
- API Release paketi yerelde hazırlandı. Admin derlemesi kullanıcıya ait; yeni yayın onayı bekleniyor. Gerçek tarayıcı kabulü bitmeden faz tamamlanmış değildir.

## Faz 7v — müşteri kartı ve dönemsel aktivite kaynağı (2026-09-11)

- CustomerReportSource/Executor, mevcut dinamik detay/özet, predicate, tablo, Top ve Excel akışına bağlandı. Her satır bir güncel müşteri kartı; silinmiş/anonimleştirilmiş kartlar hariç. Ad/soyad/kimlik/durum/açılış tarihi ile yetki dahilinde dönemsel sipariş ve iade kayıt sayıları kullanılabilir. Telefon/e-posta/kimlik numarası/şifre/not açılmadı.
- Dönem müşteri açılış tarihine değil ilgili sipariş/iade oluşturulma tarihine uygulanır. İade için bağlı siparişin eski tarihi engel değildir. İki işlem kümesi ayrı gruplanıp karta bağlandığından çarpım oluşmaz. Kaynak SQL yalnız sunucu sabitleri ve typed parametrelerden oluşur; model SQL üretemez.
- Müşteri kaynağı global reports.ai.use + crm.members.view gerektirir; kanalla sınırlı rapor/CRM yetkisi global müşteri listesine dönüşmez. globalScope yalnız sunucunun türettiği capability, yeni IAM izni/seed değildir. Aktivite alanları ayrıca orders.view / orders.returns.view ve ayrı kanal kapsamlarına bağlıdır; yetkisiz işlem tablosu hiç sorgulanmaz.
- Sipariş sayısı iptaller dahil bütün durumları, iade sayısı kayıtları sayar; ödenmiş iade değildir. Sıfır yalnız yetkili kanallarda kayıt bulunmamasıdır. Misafir siparişler kartla bağlanmadıkça dahil edilmez. Ödeme yöntemi/ürün/durum bazlı müşteri aktivitesi henüz desteklenmez; sessizce toplam sayıya çevrilmez.
- Admin kaynak seçimi, kayıtlı tarif doğrulayıcı, katalog ve model sözleşmesi güncellendi. OpenAI Docs [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) yaklaşımı korundu: model aynı, strict şema ve yalnız izinli metadata; gerçek kişi/rapor sonucu gönderilmedi.
- API508 geçti/7 mevcut DB testi atlandı/0 hata; admin80/80, TSC noEmit/ilgili ESLint ve diff kontrolü geçti. Eski unknown-source testindeki customers yeni destek nedeniyle unknownSource yapıldı; test kaldırılmadı. Yeni testler kapsam, gizli alan/işlem sorgusu engeli, SQL çevirisi, detay/özet ve katalog rollout kontrolünü kapsar.
- Gerçek .241 READ ONLY kabulü1/1 geçti,0 atlandı (25s): müşteri/işlem toplamları bağımsız SQL ile eşleşti; detay şekli, boş kanal ve CRM-only sorgular çalıştı. Mevcut stok/sipariş/iade kabulü de aynı koşumda geçti. Geçici API01 tüneli kapandı, environment geri alındı. Migration/seed/DB yazımı, npm production build, yayın ve push yapılmadı. Bu aşama genel rapor projesini bitirmez: daha geniş ilişkisel müşteri filtreleri, personel/maliyet/hareketsiz kart, PDF/asenkron çıktı ve son tarayıcı kabulü açık.

## Faz 7u — bağımsız iade kaynağı (2026-09-11)

**Yayın güncellemesi:** Kullanıcının ayrıca API restart/ayar onayı sonrası20260911_ai_dynamic_returns API01/API02 ve .56 admin üzerinde aktif. İki API hash/ready kontrolleri geçti, DynamicEnabled açıldı; diğer config değerleri korundu, migration/worker/Nginx config/.59 değişmedi. API02 bağlantı kesilmesinden sonra tekrar restart yerine aktif durum/hash kontrol edildi; admin ayrı devam ettirildi. İşe ait aktarım arşivleri temizlendi, önceki release korunuyor.

Tarayıcıda gerçek OpenAI iade durum/sayı planı, Türkiye saatiyle Ağustos2026 aralığı,6 iade sonucu ve grafik doğrulandı. AI'sız tablo araması0 sonuç, temizleme yeniden6 kayıt gösterdi. Yalnız jenerik test istemi ve izinli şema gönderildi; sonuç veya kişisel kayıt gönderilmedi. Kayıt/paylaşım/Excel indirme ve çoklu node kota yarışı ayrıca açık. Bu, tüm iş kaynaklarının tamamlandığı anlamına gelmez.

ReturnReportSource/Executor aynı dinamik predicate, detay/özet, tablo ve Excel motoruna bağlandı. İade tarih aralığı sipariş CreatedAt değil iade CreatedAt üzerinden uygulanır. orders.returns.view ve rapor kanal kapsamı kesişir; silinmiş iade ve bağlı sipariş hariçtir. Tutar kayıtlı RefundAmount'tır, fiili ödeme değildir; para birimi zorunludur. Not/iletişim/müşteri kimliği eklenmedi. Yeni şema/seed yok. Kaynak rollout kapısına bağlıdır.

OpenAI Docs [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) doğrultusunda mevcut strict/izinli alan sözleşmesi korundu; modele kayıt veya sonuç gönderilmedi. Admin returns seçimi/kayıtlı tarif ve tüm para birimi kolonlarında grafik karışım koruması eklendi. API504 başarılı/7 atlandı, admin80/80; TSC/ESLint temiz. Gerçek .241 READ ONLY kabul1/1 (22s), bağımsız iade sayı/tutar SQL karşılaştırması geçti.

Kullanıcı admin build'i tamamladı, API Release publish hazır. Yayın başlamadı: otomatik onay denetimi .56 admin iznini API01/API02 config+restart onayı saymadı. API'ler20260910_ai_scope_fix üzerinde; yeni sürümün görünmesi için açık API yayın/aktivasyon onayı gerekir. Tüm dinamik raporlama tamamlandı değildir; müşteri/personel/maliyet, hareketsiz kart, PDF/asenkron export ve gerçek model/Redis kabulü açık.

## Faz 7t — kontrollü Excel kapasitesi (2026-09-11)

- Excel endpoint'i sunucu içinde ForExport ile 5.000 satırlık bütçe verir. Normal tablo üst sınırı 250 kalır; internal export işareti JSON ile verilemez, serialize/deserialize sonrasında da yetki taşımaz. Filtre/arama/sıralama korunur, sayfa 1 olur; tarifin Top semantiği değişmez.
- Stok, sipariş V1/V2 ve hareket executor'ları aynı sorgu/snapshot akışını kullanır; ayrı sayfalar birleştirilmez. Yetki kontrolleri korunur. Sayım sınırı aşılırsa dosya yok; Excel son doğrulaması tüm satırların geldiğini kontrol eder. Hücre 32.766 karakter, toplam metin 8 MiB sınırı aşılırsa kırpma yerine ret vardır. Formül metni koruması ve sayısal tipler korunur. Stok işine de 30 saniyelik deadline eklendi.
- API 500 başarılı / 7 atlandı / 0 hata; admin 79/79; TypeScript noEmit, ilgili ESLint ve diff kontrolü geçti. Yeni testler istemciden export bütçesi yükseltmeyi, 501 detay satırını, 5.000 satır kabulünü, metin ve sayım sınırlarını kapsar.
- Güncel derlemeyle tools/run-ai-readonly-acceptance.ps1 gerçek .241 hedefinde 1/1 geçti, 0 atlandı (23s). SELECT-only 1.505 satırlık fixture export modunda eksiksiz döndü; bağımsız toplam ve önceki stok/sipariş/hareket kontrolleri geçti. Tünel kapatıldı, environment geri alındı. İlk çağrıda `powershell` komutu PATH'te yoktu; mevcut PowerShell oturumundan betik doğrudan çalıştırıldı.
- Yeni tablo/seed/migration, production yazımı, yayın veya push yok. Bu senkron ve sınırlı XLSX aktarımıdır; asenkron büyük rapor/PDF, diğer iş kaynakları ve gerçek OpenAI/tarayıcı/Redis kabulü ayrıca açık kalır.

## Gerçek ortam kabul hazırlığı — 2026-09-11

**Güncelleme — salt-okunur DB kabulü tamamlandı:** Kayıtlı API01 SSH istemcisi ile geçici loopback15432 tüneli açıldı. AiStockReadAcceptanceTests gerçek .241/ecommerce_db hedef kimliği ve READ ONLY kontrolüyle1/1 geçti,0 atlandı (19s). Stok/özellik/ürün filtreleri, sentetik sayfalama, sipariş executor kontrolleri, gerçek hareket karşılaştırması ve SELECT/VALUES hareket fixture'ı çalıştı. DB'ye yazılmadı; tünel finally kapatıldı. Önceki bağlantı engeli bu DB kabulü için giderildi. Redis yarış, gerçek OpenAI ve tarayıcı/çoklu node kabulü henüz tamamlanmış değildir.

Kalıcı tekrar yöntemi: `tools/run-ai-readonly-acceptance.ps1`. Test projesi güncel derlenmiş olmalı; betik --no-build kullanır. Secret yalnız ignored config'ten belleğe ve child-process environment'a geçer. System.Data.Common.DbConnectionStringBuilder için PowerShell'de property assignment yerine set_ConnectionString/get_ConnectionString kullanılır. Anahtar içeriği/test-path ile okunmaz; kayıtlı OpenSSH istemcisi kullanır. Mevcut dolu porta müdahale etmez, yalnız kendi SSH process'ini kapatır. Başarısız test exit code'u korunur. Syntax kontrolü geçti.

- AiStockReadAcceptanceTests stok hareketleriyle genişletildi: aynı READ ONLY RepeatableRead transaction içinde son30 gün hareket grubu/sayı/ham adet, bağımsız parametreli SQL ile karşılaştırılır. Detay sayımı/sayfa şekli ve kapsamlı iznin reddi de kontrol edilir. SELECT/VALUES fixture ile boş gerçek geçmişte bile dolu örneğin tarih bitişi hariç ve ham adet davranışı sınanır; INSERT/DDL/geçici tablo/startup/audit yok.
- Test hâlâ yalnız açık ECSPROS_ACCEPTANCE_AI_STOCK_READ bağlantısıyla çalışır; loopback, ecommerce_db, hedef192.168.0.241 ve transaction_read_only=on doğrulanmadan veriye geçmez. .59 kullanılmaz. Mevcut Redis kabulü yalnız ayrı disposable16379 ve açık opt-in ile çalışır; paylaşılan Redis'e yöneltilmez.
- Bu oturumda AI salt-okunur kabul environment değeri yoktu. 15432/16379 dinleyici sorgusunda kayıt dönmedi. Private IP'ye bağlantı, yeni SSH/tünel, config veya production değişikliği denenmedi. Dolayısıyla gerçek ortam kabulü hâlâ **bloke / yapılmadı**; testin atlanması başarı değildir. Uygun onaylı bağlantı sağlandığında hazır test tekrar çalıştırılmalı.

## Faz 7r–7s — stok hareketi kaynağı ve göreli dönem (2026-09-11, yerel)

**Kod ve yerel test kapanışı:** 2026-09-11 son kontrolünde göreli dönem seçili olsa bile sabit tarih yedeğinin geçerli kalması zorunlu yapıldı. Böylece sabit döneme dönüşte bozuk/eski geçersiz tarih gizlenmez. Controller düzeyinde hareket tarifini kaydetme/paylaşma, period değerinin korunması, rollout kapalı ve kapsamlı/yetersiz izinle reddetme test edildi; gerçek DB'ye bağlanılmadı. Admin açık dönem seçimi, eski sonucu sıfırlama ve sunucu tarihlerini gösterme regresyonu eklendi. API497 başarılı/7 atlandı/0 hata; admin79/79. Bu kapanış yalnız7r–7s kod/yerel test kapsamıdır, tüm AI raporlama veya canlı kabul kapanışı değildir.

- MovementReportSource/Executor mevcut Inventory.StockMovements üzerinden çalışır; yeni tablo/alan/seed yok. Tip, kaynak/hedef depo kimliği, varyant kimliği, bağlı işlem tipi, kayıt tarihi, kayıt sayısı ve kayıtlı adet ortak predicate/aggregate/detail motoruna bağlı. Adet net stok değişimi veya geçmiş bakiye değildir; transfer yönü/tip kodu uydurulmaz. Notlar, personel ve maliyet bilgileri açılmadı; ürün kodu/barkod yerine kimlik gösterildiği açıkça belirtilir.
- Kaynak yalnız DynamicEnabled ve kapsamsız inventory.view + reports.ai.use ile kullanılabilir. Scoped stok izni global erişime dönüşmez. AI şeması, API katalog ve admin alan listesi kaynak bazlıdır; model başka kaynağa geçerse yanıt reddedilir. İlişkisiz kaynakta boş enum yerine null relation şeması kullanılır. OpenAI Docs strict şema kuralları korundu; model değişmedi: https://developers.openai.com/api/docs/guides/structured-outputs
- Aynı tarihlerle filtrelenmiş adet ve sayfa readonly RepeatableRead içinde hesaplanır; SQL süre sınırı 15s, işlem 30s. Stok/sipariş/hareket executor'ları artık tek process başına ortak iki slot kullanır; kaynak sayısı DB eşzamanlılığını artırmaz. Bu dağıtık global kilit değildir; node sayısıyla toplam kapasite artar, gerçek yük kabulü hâlâ gerekir.
- V2 tarifte isteğe bağlı Period: thisMonth, lastMonth, last30Days, last6Months. Kullanıcı önizlemede açıkça seçer; AI otomatik eklemez. Türkiye takviminde bu ay/önceki ay/bugün dahil30 gün/bu ay dahil6 ay hesaplanır. Kaydetme ve paylaşma mevcut JSON tarifinde seçimi korur; her çalıştırmada yeniden çözülür. Ek predicate tarihleri değiştirilmez; ekran uyarır. Sabit tarihli eski tarifler uyumludur. Sonuç ResolvedPeriod ile gerçek kullanılan tarihleri gösterir. Aralık başlangıcı dahil, bitişi hariçtir.
- Yeni MovementReportTests, katalog feature-gate testi ve ReportRelativePeriodTests; yıl/ay/Türkiye gece sınırı, artık yıl, izin reddi, tarih sınırı, ham adet anlamı, SQL çeviri, kaynak değiştirme reddi ve ortak slot referansı kontrol edilir. Admin kayıt/önizleme testleri güncellendi. Nihai test sayıları PROGRESS.md Faz7r–7s kaydında.
- Tamamlanmamış işler: bağımsız müşteri/iade/personel/maliyet kaynakları, hareketsiz stok kartı için katalogdan anti-join, büyük/asenkron export ve PDF, gerçek DB/OpenAI/tarayıcı/çoklu node kabulü. Güncel stok ile kayıtlı hareket raporu tarihsel bakiye çözümü değildir. Bu faz genel dinamik raporlamanın tamamlandığı anlamına gelmez.
- Production, SSH/yayın, config, migration ve GitHub değişmedi. Eski yerel/untracked çalışmalar korundu; bu iş yeni yedek veya geçici dosya bırakmadı.

## Faz 7q — alan bazlı izin ve kayıtlı müşteri kimliği (2026-09-11, yerel)

- ReportEntityDefinition.RequirePermission alanın ek yetkisini tek sözlük kaydında tanımlar; Describe/DescribeDetails yetkisiz alanı saklar. Predicate, aggregate ve detail derleyicileri seçilen alanın ek iznini sorgu üretmeden kontrol eder. NOT/OR veya eskiden kaydedilmiş plan ek izni atlayamaz; kaynak izni ayrıca zorunludur. Bu farklı rapor soruları için ayrı kod değil ortak alan güvenliği altyapısıdır.
- Sipariş sözlüğüne orders.memberId ve orders.memberCount eklendi. Kapsamsız crm.members.view gerekir; kanal kapsamlı CRM izni global müşteri yetkisi sayılmaz. MemberId siparişteki kayıtlı kimliktir, müşteri adı/iletişim bilgisi değildir; CRM profilinin güncel/aktif varlığını ayrıca doğrulamaz. Tekil sayım null misafirleri saymaz, aynı üyenin tekrar siparişleri üye sayısını artırmaz. İsim/telefon/e-posta, misafir birleştirme veya tüm müşteri ana kart raporu henüz eklenmedi.
- Ortak metin predicate'ine isNull/isNotNull eklendi; null/boş değer listesi ister, metinle null'ı karıştırmaz. Kullanıcı kabul ederse kimlik bazlı sıralama için misafirler isNotNull ile dışlanabilir. AI şeması yalnız izinli alan enum'ları üretir; talimat kimlik-only çıktının kullanıcıyla netleştirilmesini, müşteri iletişim listesi gibi sunulmamasını ister. OpenAI Docs strict/nullable şema kuralları korundu; firma modeli değiştirilmedi. Kaynak: https://developers.openai.com/api/docs/guides/structured-outputs
- API 489 başarılı/7 atlandı/0 hata. Yeni ReportCustomerIdentityTests metadata, predicate/NOT, aggregate/detail izin kaybı, scoped CRM reddi, tekil sayım/misafir/soft-delete, bağlantısız PostgreSQL IS NOT NULL/gruplama/Top çevirisi ve izinli AI şemasını test eder. İlk test derlemesinde Query yerine mevcut Rows özelliği düzeltilip genel koşu yeniden geçti. Admin kodu değişmedi; frontend testleri bu adımda tekrarlanmadı. Gerçek model/DB kabulü yok.
- Config/DB/migration/seed/yayın/push yok. Birinci ana iş (geniş veri kapsamı) henüz tamamlanmadı: müşteri ana kartı/isimler, bağımsız iade, stok hareketi, maliyet ve personel kaynakları açık. StockMovement mevcut modeli incelendi; hareket tipi, kaynak/hedef depo, adet/tarih var fakat bu aşamada rapor kaynağına bağlanmadı. Sonraki göreli dönem/PDF/büyük export ve gerçek kabul maddeleri de açık.

## Faz 7o–7p — kayıt yönetimi, paylaşım, Excel (2026-09-11, yerel)

- Kayıt güncelleme/kaldırma kullanıcı onayı ister. CompareExchangePreferenceAsync mevcut JSONB değeriyle tek SQL koşulunda karşılaştırır; başka sekme/node değiştirmişse 409, eski kayıtla yeni tarif silinmez/ezilmez. Güncelleme eski paylaşım anahtarını kaldırır. Yalnız kişisel rapor tarifi kaldırılır; kaynak işletme kayıtlarına dokunulmaz.
- Ayrı reports.ai.share izni eklendi. Paylaşım 32 byte rastgele anahtarlı bağlantıyla aynı firma/aktif kullanıcı sınırındadır; firma atanmamış veya farklı firma reddedilir (süper admin dahil firma sınırı gevşetilmez). Sahip ve alıcının güncel tarif/veri yetkisi yeniden doğrulanır; sahip paylaşım iznini kaybederse bağlantı çalışmaz. Alıcı kendi veri/kanal kapsamıyla hesaplar. Paylaşım sonuç taşımaz. Anahtar URL fragment'ındadır; sunucuya POST gövdesinde gider, query string'e yazılmaz. Audit yalnız olay/sahip/slot metadata'sı tutar. Anahtar yenileme/kapatma eski bağlantıyı geçersiz kılar; önceden kopyalanan tarifi geri alamaz. Alıcı bazlı ACL/zaman sonu henüz yok; aynı firmadaki yetkili bağlantı sahipleri açabilir.
- reports.ai.export katalog aksiyonu ve /reports/ai/export eklendi. Kullanım+kaynak izinleri ve ayrıca kapsamsız export izni hem girişte hem dosya öncesi doğrulanır. Mevcut executor/readonly snapshot ve tablo filtreleri kullanılır; page=1/pageSize=250 sunucuda belirlenir. Toplam >250, eksik/boş sonuç veya kolon hatasında dosya yok. Farklı snapshot'lardan sayfa birleştirilmez. Eski grid limitleri artırılmadı. Sonuç indirme anında hesaplanır; UI bunu açıklar.
- Mevcut MiniExcel/GridExportWriter kullanılır, ek bağımlılık yok. Kolon başlıkları kararlı alan kimlikleridir; ölçüler numeric, tarihler Türkiye saatidir. Formül başlangıçlı metinler nötrlenir; sayısal negatif korunur. Geçici dosya DeleteOnClose ile kapanınca silinir. PDF ve 250 üstü/asenkron export bu fazda yoktur.
- API 485 başarılı/7 atlandı/0 hata; admin 78/78; TypeScript/ESLint başarılı. Yeni politika testleri firma/aktiflik/izin/anahtar, CAS parametre testi, controller update/remove/403 export, gerçek XLSX zip içeriği ve geçici dosya temizliğini kapsar. Gerçek paylaşım DB akışı/iki node yarış/tarayıcı ve OpenAI kabulü yapılmadı. Salt kod/politika testleri uçtan uca kabul yerine sayılmaz.
- Değişenler: SavedAiReportsController/SavedReports, IIamDbContext/IamDbContext, ReportSharingPolicy, ReportDictionary/PermissionKatalogu, AiReportsController/AiReportsPage, ReportExcelExport ve testler. Yeni tablo/migration/config/yayın/push yok. İki yeni izin mevcut permission katalog senkronu yoluyla hedef ortamda ayrıca doğrulanmalı; DB'ye burada uygulanmadı. Kalan: diğer iş kaynakları, göreli dönem, tam paylaşım kabulü, büyük export/PDF ve gerçek ortam güvenlik/performans kabulü.
- Son ek doğrulama: ReportShareDirectory/DI üzerinden fake kullanıcı diziniyle controller akışı ve FromServicesRegistration hedefi 2/2 geçti. Aynı firma yetkili açma; yanlış token/firma ve iki tarafın izin kaybında reddetme; yanıtta/auditte token/sonuç bulunmaması ve rapor çalıştırma/yazma olmaması test edildi. Bu gerçek PostgreSQL paylaşım kabulü değildir.

## Faz 7n — kişisel rapor tarifi saklama (2026-09-11, yerel)

- SavedAiReportsController yalnız JWT sahibinin iam_users.Preferences içindeki reports.ai.saved.0–19 anahtarlarını okur/yazar. Rapor izni ve tarif doğrulaması zorunlu; V2 ayrıca DynamicEnabled ve mevcut sorgu derleyicisinden geçer. Ad, tarif ve kayıt zamanı saklanır; sonuç/satır/konuşma saklanmaz. Başka kullanıcı ID'si alınmaz. POST dolu slotu ezmez (409). Yeni tablo/migration yok.
- WritePreferenceAsync parametreli PostgreSQL jsonb_set ile tek anahtarı atomik günceller. Genel SetMyPreferenceCommand aynı metoda yönlendirildi: diğer tercih yazarları eski sözlük kopyasıyla raporları ezmez. Null ile silme, soft-delete koruması ve UTF-8 byte sınırı korunur/düzeltilir. Ortak tercih yazma değişikliği gerçek PostgreSQL eşzamanlılık kabulü gerektirir. Genel aynı anahtar güncellemelerinde son yazan kazanır; rapor ekleme insert-only'dir.
- Admin kişisel sorgu anahtarı, adla kaydetme, yenileme, taslak açma sunar. validSavedPlan bozuk kayıtların render edilmesini engeller. Açma hesaplama/AI çağrısı başlatmaz; çalıştırmada mevcut yetkiler yeniden kontrol edilir. Tarihler sabittir; tablo filtreleri kaydedilen tarife dahil değildir. Yetkisi kaldırılmış kullanıcı kendisine ait eski tarif metnini görebilir ama bu veri erişimi sağlamaz.
- API genel 477 başarılı/7 atlandı/0 hata; ardından AtomicPreferenceTests + SavedAiReportsTests 3/3. Admin 77/77; TypeScript, hedef ESLint ve diff kontrolü başarılı. SQL testi bağlantı/komutu interceptor ile bastırıp parametrelemeyi doğrular; gerçek DB yarış testi değildir. Tarayıcı/model/DB kabulü yapılmadı.
- Dosyalar: SavedAiReportsController, SavedReports.tsx, savedReportValidation.ts, AiReportsPage; IIamDbContext, IamDbContext, MyPreferences; iki API ve bir admin test dosyası. Config/yayın/push/production değişmedi. Paylaşım, kayıt güncelleme-silme yönetimi, göreli tarihler, export, diğer kaynaklar ve gerçek ortam kabulü açık. Paylaşım kişisel tercihleri açarak değil ayrı alıcı yetkisi/iptali ve denetim modeliyle yapılmalı.

## Faz 7m — özet sonuç grafiği (2026-09-10, yerel)

- Admin mevcut yetkili sonuçlardan, ek AI/DB isteği veya bağımlılık olmadan açılır yatay çubuk grafik gösterir. Ölçü seçilebilir; tüm kırılımlar etikette korunur, ölçüler birbirine eklenmez. Negatif değer sıfırın solunda, pozitif sağında; sayısal değer ve etiket ekran okuyucuya da sunulur.
- Yalnız özet ve tüm filtrelenmiş sonuçları aynı sayfada olan en fazla 50 grup kabul edilir. Sayfanın bir kısmını tüm rapormuş gibi çizmez; farklı para birimleri, eksik kolonlar, geçersiz sayılar engellenir. Tablo filtrelerini/sıralamasını değiştirmez. Eski/geçersiz sonuç kapısı grafik için de geçerlidir. Detay raporuna grafik eklenmedi.
- Admin 75/75 test, TypeScript noEmit ve hedef ESLint başarılı. Saf grafik dönüşümünün gerçek çalıştırma testleri sıra, sıfır/negatif, eksik sayfa, sınır, para birimi ve tip hatalarını kapsar. Tarayıcı görsel kabulü yapılmadı. API değişmedi; API testleri bu frontend adımında tekrarlanmadı.
- DynamicEnabled/config/DB/migration/seed/yayın/push değiştirilmedi. Bu yalnız güvenli temel grafik görünümüdür; tüm sonuçlar için sunucu export'u, kullanıcı kayıt/paylaşımı, diğer iş kaynakları ve gerçek ortam kabulü halen açıktır.

## Faz 7l — detay planı API/AI/admin bağlantısı (2026-09-10, yerel/kapalı)

- DynamicReportPlan V2 tam olarak bir aggregate veya detail kabul eder; ikisi birden/ikisi de boş reddedilir. Eski aggregate zarfı çalışır. Detay aynı sipariş kaynağı/dönem/ilişki koşullarıyla derlenir. OrderReportExecutor detay dalı aynı iki slot sınırını, 30s deadline, 15s timeout ve READ ONLY RepeatableRead transaction'ı kullanır; toplam sayım ve sayfa aynı snapshot'tadır. SQL çevirisi slot içinde, bağlantı açılmadan yapılır; footer toplamı uydurulmaz.
- Ortak alan metadata'sına dataType eklendi; catalog yetkili dynamicDetailFields döndürür. Detay kolonları ortak sözlükten gelir. Model şeması required/nullable aggregate/detail ve izinli kolon enum'ları kullanır ([OpenAI Docs](https://developers.openai.com/api/docs/guides/structured-outputs)). Firma modeli değişmedi. Model hazır dediğinde controller güncel izinle detay derleyicisini de doğrular; yorumlama raporu çalıştırmaz.
- Admin özet/detay hızlı yanıtı, detay kolon sırası/tek sipariş satırı açıklaması, türüne göre tarih/sayı/metin filtresi ve yalnız metin kolonu varsa arama sunar. Null aggregate API yanıtından çıkarıldığında V2 source üzerinden tanınır. Yeni rapor ilk çalıştırıldığında eski tablo arama/filtre/sıra/sayfa temizlenir; aynı raporu yeniden çalıştırmada korunur. Top kapsamı satır/grup olarak ayrı açıklanır.
- API 475 başarılı/7 atlandı/0 hata; admin 72/72. Yeni DynamicDetailPlanTests ve controller fake-provider detay akışı, yalnız bir çıktı dalı, eski plan uyumu, izinli şema/tür, gizli kolonun preview/audit öncesi reddi ve otomatik yürütme olmamasını doğrular. İlk koşudaki bir eski şema testi nullable aggregate yoluna güncellendi; son koşu geçti. Son tip/lint bilgisi PROGRESS.md'de.
- V2 halen kapalı; config/DB/migration/seed/yayın/push yok. Gerçek model/DB/tarayıcı/yük kabulü yapılmadı. Bu bağlama yalnız onaylı sipariş başlık detaylarını açmaya hazırlar; müşteri/personel/ürün satırı detayları, diğer kaynaklar, grafik/kayıt/paylaşım/export kapsamı tamamlanmış değildir.

## Faz 7k — ortak detay kolon projeksiyonu (2026-09-10, yerel/kapalı)

- ReportDetailPlan/ReportDetailSchema/ReportDetailQuery eklendi. Sunucuda tanımlı 1–16 kolon seçilir; metin/Guid/tarih/sayı türleri korunur. Yalnız seçilen kolonlarda ortak GridSchema ile filtre/sıralama ve metin araması yapılır. Bilinmeyen, tekrar eden, seçilmemiş veya aggregate-only kolonlar reddedilir. Top 1–1000 için açık sıralama gerekir; tablo işlemleri seçilmiş üst kümeyi yeniden doldurmaz.
- ReportEntityDefinition aynı kayıt üzerinden detay alanlarını ve DescribeDetails metadata'sını üretir; ikinci alan listesi yok. Text/Value alanları ve satır filtresi tanımlı sum ölçüsünün ham değeri seçilebilir; count/average/min/max/distinctCount detay değeri değildir. Tutar için tanımlı para birimi kolonu zorunluluğu detaya da taşınır.
- Yetki ve zorunlu satır guard'ı önce uygulanır. Tüm filtreli kaynak sayım için ayrı IQueryable olarak kalır, sayfa sorgusu Skip/Take sonrasında yalnız seçilen değerleri projekte eder. Gizli müşteri/adres/telefon alanları seçime eklenmedi; arama seçilmemiş alanlara bakmaz. Benzersiz sunucu anahtarı sıralama eşitliklerini çözer; anahtar çıktı kolonu olmak zorunda değildir.
- OrderReportSource.DynamicDetails mevcut giriş koşullarından sonra güncel sipariş/kanal yetkisini yeniden uygular. Eski Details/V1, yeni aggregate yolu, API ve admin değiştirilmedi. Bu yalnız sorgu motoru/adaptör bağlantısıdır: V2 detail zarfı, AI şeması, executor ve admin plan seçimi sonraki aşamadır; ekran henüz detay planı üretmez.
- ReportDetailTests kolon/snapshot sırası, tüm filtreli sayım/boş sayfa, gizli kolon/yetki/sınır reddi, Top/tie-breaker, para birimi ve PostgreSQL seçili kolon/LIMIT/OFFSET çevirisi ile güncel kanal kontrolünü kapsar. Gerçek DB/model çağrısı yok. Nihai test sayısı PROGRESS.md Faz 7k kaydında; migration/seed/config/yayın/push yapılmadı.

## Faz 7j — ürün ilişkisi katalog yetkisi düzeltmesi (2026-09-10, yerel/kapalı)

- Detay planına geçiş incelemesinde Faz 7i ilişkisinin mevcut OrderReportSource ürün filtresinden daha geniş yetki kabul ettiği bulundu: ürün kodu/barkod katalog okuması yalnız orders.view ile yapılabiliyordu. Bu kod henüz yayınlanmamış ve DynamicEnabled kapalıydı; gerçek veri erişimi yapılmadı.
- Items veri tanımı catalog.products.view gerektirir. OrderReportPredicates elle izin listesi kurmak yerine merkezi ResolvePermissions kullanır; bu çözümleyici kanal kapsamlı katalog yetkisini global katalog projeksiyonu için kabul etmez. Katalog izni yoksa projeksiyon dahi oluşturulmaz ve ilişki/alanlar metadata'dan saklanır. orders.view ve reports.ai.use ile kanal kesişimi yine zorunludur.
- EXISTS, NOT EXISTS, NOT ve OR içinden yetkisiz ilişki sorgulanamaz. Yetki kaldırılmışsa önceki plan yeniden çalıştırılamaz. Katalog izni olmayan kullanıcı normal sipariş raporunu almaya devam eder. Bu fazda tüm orders.items ilişkisi (SKU/ad/adet dahil) katalog kapısından geçer; yalnız snapshot alanları için daha dar ayrı kaynak henüz açılmadı.
- ReportOrderLineTests iki yeni regresyon senaryosu ve izinli fixture düzeltmesi içerir. Son test sayıları PROGRESS.md Faz 7j kaydında. Admin/AI şeması/model/config değiştirilmedi; metadata mevcut ortak sözlükten türetilir. Yeni tablo/kolon, migration/seed, DB/model çağrısı, yayın/push yok. Detay planı bu güvenlik düzeltmesi nedeniyle sonraki adım olarak açık kaldı.

## Faz 7i — sipariş ürün satırı ilişkisi (2026-09-10, yerel/kapalı)

- Ortak sözlüğe orders.items ilişkisi ve items.sku/productName/productCode/barcode/quantity filtre alanları bağlandı. Ayrı soru/rapor metodu eklenmedi; aynı predicate motoru AND/OR/NOT/EXISTS/NOT EXISTS koşullarını kullanır. Sipariş yetkisi ve kanal kapsamı ana sorguda korunur.
- OrderReportLine yalnız okuma projeksiyonudur, entity/migration/tablo değildir. IOrderDbContext.ReportLines ve OrderDbContext sabit SELECT'i, sipariş satırını katalogdaki benzersiz varyant/ürün kimlikleriyle bağlar. Kod ve barkod güncel, silinmemiş katalogdan; SKU ve ad sipariş snapshot'ından gelir. Eksik katalogda SKU'ya fallback yapılmaz; null kalır. Eski sipariş listesinin FilterOrdersByProduct davranışı değiştirilmedi.
- Aynı EXISTS altındaki koşullar aynı satırda eşleşir; ayrı EXISTS'ler farklı satırlarda eşleşebilir. Quantity tek satırın adedidir, satırlar toplamı değildir. Silinmiş sipariş satırları dışlanır; ana sorgu JOIN ile çoğaltılmaz. orders.amount seçilen siparişlerin tamamının GrandTotal'ıdır; eşleşen ürünün satış tutarı gibi sunulmaz. Satır fiyatı/tutarı, ürün kırılımı ve ürün bazlı satış toplamı bu adımda açılmadı.
- OpenAI talimatı ve sözlükten otomatik gelen enum'lar bu ilişkiyi tanır. OpenAI Docs strict şema yönergeleri korunur; firma modeli değiştirilmez. Admin önizleme ilişkili ürün koşulunun tutar anlamını açıklar. Gerçek model/DB/tarayıcı kabulü yapılmadı; DynamicEnabled kapalıdır.
- Yeni ReportOrderLineTests: aynı satır eşleşmesi, çoğalmama, kanal/soft-delete guard, eksik katalog/SKU ayrımı, yanlış scope reddi, yetkili metadata, parametreli PostgreSQL EXISTS çevirisi ve projeksiyonun migration entity'si olmaması. Son test sayıları PROGRESS.md Faz 7i kaydında. Migration/seed/config/yayın/push yok; genel işletme/detay rapor kapsamı halen açık.

## Faz 7h — dinamik ilk N sonuç sınırı (2026-09-10, yerel/kapalı)

- ReportAggregatePlan.Top nullable alanı eklendi; eski planlar sınır olmadan çalışmaya devam eder. 1–1000, açık sıralama ve en az bir kırılım zorunlu; geçersiz değerler sessizce kırpılmaz. Top aynı ortak motorla her kayıtlı kırılım/ölçü kombinasyonuna uygulanır; ayrı rapor şablonu değildir.
- Zorunlu yetki/satır koruması ve giriş koşulları → tüm veri üzerinde gruplama → plan sıralaması ve tam grup anahtarıyla deterministik eşitlik çözümü → Top → tablo filtre/arama/sıralama → sayfalama. Tablo filtreleri ilk N kümesini daraltır; yeniden ilk N seçmez veya dışarıdan doldurmaz. Eşit ölçüde sınır dışı gruplar otomatik eklenmez (WITH TIES yok).
- OpenAI strict sözleşmesine required/nullable integer top eklendi; limit istemeyen soruda null, en fazla için desc/en az için asc. Belirsiz sıralama veya N netleştirilir; desteklenmeyen kapsam hâlâ reddedilir. Firma modeli değişmedi. [OpenAI Docs Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) required/nullable kuralları kullanıldı; sunucu validasyonu ayrıca devam eder.
- Admin plan önizlemesi sınırı ve tablo filtrelerinin kapsamını gösterir. V2 kapısı kapalı kalır, V1 korunur. Yeni müşteri/personel kaynağı açılmadı; motor halen mevcut yetkili alanlarla çalışır. Tarayıcı/gerçek model/DB kabulü ve genel kaynak kapsamı tamamlanmadı.
- Yeni ReportTopLimitTests: eşitlik, yükselen/alçalan, null/boş kaynak/sınır hataları, filtreyle doldurmama, toplam ve sayfa kapsamı, bağlantısız PostgreSQL iç LIMIT/dış filtre çevirisi, AI şema sözleşmesi. API 460 başarılı/7 atlandı/0 hata; admin 71/71. Ek tip/lint sonucu PROGRESS.md kaydındadır. Migration/seed/config/yayın/push yok.

## Faz 7g — kaynak bağımsız tekil sayım (2026-09-10, yerel/kapalı)

- Ortak ReportAggregateSchema ve ReportEntityDefinition artık DistinctCount destekler. Metin/Guid/int/long ve nullable kimlik alanları bir kez tanımlanabilir; yeni rapor şablonu veya modelden SQL kabulü yoktur. Yetki/satır koruması sonrasında grup içindeki tekil değerler sayılır, null hariçtir. Boş metin geçerli değer olarak sayılır; normalize edilmez.
- Sayım tüm filtreli grupta ve sayfalama öncesinde gerçekleşir; tekrar eden kayıtlar ölçüyü artırmaz. Boş kaynak, mevcut motor sözleşmesi gereği boş sonuçtur; yalnız null içeren mevcut grup sıfır tekil değer verir. Sözlük metadata'sı aynı kayıttan üretilir. Desteklenmeyen tipler ve yinelenen alan kimlikleri tanım sırasında reddedilir.
- Bu bir motor yeteneğidir: müşteri/personel alanları henüz son kullanıcı sözlüğüne eklenmedi. Müşteri sayımı için kimlik/üyelik/anonim sipariş anlamı ve ilgili izinler ayrıca doğrulanmalıdır. Diğer işletme kaynakları, detay planı ve kabul işleri açık; tüm dinamik raporlama tamamlanmadı.
- ReportDistinctCountTests bellek sonuçları, null/tekrar, soft-delete guard, yetki reddi, boş kaynak, metadata ve bağlantısız PostgreSQL çevirisini kapsar. Test koşusu sonucu PROGRESS.md Faz 7g kaydındadır. DB/model çağrısı, migration, seed, config, yayın veya push yoktur. Admin değişmedi; DynamicEnabled kapalıdır.

## Faz 7f — ortak iş veri sözlüğü (2026-09-10, yerel/kapalı)

- ReportEntityDefinition, ReportRelationDefinition ve ReportBusinessDictionary eklendi. Alan/ilişki bir kez tanımlanır; aynı tanımdan AI metadata, izinli filtre operatörleri, predicate ve aggregate kayıtları üretilir. Sipariş/ödeme/iade tanımlarındaki tekrarlar kaldırıldı; yeni soru veya rapor şablonu eklenmedi.
- OpenAI strict şemasının alan ve ilişki enum'ları da sözlükten gelir. Guid alanına contains, metin alanına gt önerilmez. OpenAI Docs Structured Outputs yönergeleri uygulandı; model çıktısının sunucuda doğrulanması korunur.
- Kaynak/satır/kanal yetkileri, iade yetkisi olmayan kullanıcıdan ilişki metadata'sının saklanması ve para birimi zorunluluğu korundu. Sözlük kullanımdan önce sabitlenir; sonradan değiştirme ve aynı alanı iki kez kaydetme reddedilir.
- Test: acceptance dışı API 452 başarılı, 7 atlandı, 0 hata (459 toplam). Dört yeni test tek tanımın filtre/gruplama/metadata üretimini, enum tutarlılığını, değişmezliği ve yetki sızıntısını kapsar. git diff --check başarılı; yalnız mevcut CRLF uyarıları. Admin değişmedi; frontend kontrolleri tekrarlanmadı.
- DB/model/tarayıcı kabulü yapılmadı; migration/seed/config/yayın/push yok. DynamicEnabled kapalı kaldı. Yeni veri alanını sözlüğe bağlamak halen kod/derleme gerektirir; tanımlanmış alanlarla yeni soru kombinasyonları ayrı kod gerektirmez. Tüm işletme kaynakları, detay projeksiyonu ve genel çok-kaynaklı plan desteği henüz tamamlanmadı; bu adım kapsam genişletmesi değil ortak altyapıdır.

## Faz 7e — V2 AI taslak ve admin bağlantısı (2026-09-10, yerel/kapalı)

- OpenAiDynamicReportContract, firmada seçili modeli değiştirmeden Responses API strict json_schema üretir; $defs üzerinden recursive predicate, required/nullable alanlar, additionalProperties=false ve store=false. [OpenAI Docs Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) rehberi kullanıldı. Şema uyumu iş doğruluğu sayılmaz; model çıktısı sunucu derleyicisinde tekrar doğrulanır.
- DynamicReportMetadata yetkili dimension/metric/filter/relation etiketlerini verir; iade alan/ilişkileri yalnız orders.returns.view kapsamında görünür. Mevcut sözlükle sayım/toplam/ortalama/min/max ve ödeme/iade koşulları desteklenir. Ödeme yönteminin UUID'si veya doğrulanmamış statü anlamı uydurulmaz. İade tarihi isteyen soru, sipariş tarihi filtresine sessizce dönüştürülmez.
- InterpretReportRequest.planVersion varsayılan1; orders+2 yalnız DynamicEnabled açıkken kabul edilir. Mevcut firma/consent/privacy/history/Redis kota/30s HTTP/64KiB yanıt koruması paylaşılır. Rapor sonuçları modele gönderilmez. Ready V2 yanıt dynamicPlan taşır; Definition null kalır. Controller güncel effective yetki ile ValidateDynamicPlan üzerinden kolon/ilişki/ölçü kurallarını doğrular; veri sorgusu çalıştırmaz. Sonraki /grid yeniden doğrular.
- dynamic_layout soru kodu aggregate kırılım/ölçüsünü netleştirir; V1 order_layout detay seçeneği korunur. Admin DynamicEnabled=true olduğunda orders için V2 ister, okunur tarih/ölçü/sıra/iç içe koşul önizlemesi sunar. Otomatik yürütme yoktur. Kullanıcı düğmeyle çalıştırır; tablo işlemleri AI çağırmaz. Boyutsuz V2 arama kapalı; feature kapanırsa eski V2 sonucu/yürütmesi bloklanır. V1 stok ve sipariş yolu korunur.
- Test: API448 başarılı/7 atlandı/0 hata; admin70/70; tsc ve hedef ESLint temiz. Yeni14 contract/transport testi ve2 controller akış testi, fake HTTP/ayar/kota sınırlarıyla no-autoexecute, bozuk/refused/incomplete/duplicate yanıt, kaynak yetkisi ve compiler reddini kontrol eder. Gerçek API anahtarı/DB/Redis/provider veya tarayıcı kabulü yok. İki paralel dotnet test sırasında DLL kopyalama retry uyarısı oluştu; retry sonrası genel test koşusu başarılı bitti.
- İki alt ajan kullanıldı: admin kodu/testleri ve yeni backend contract testleri ayrı dosyalarda çalışıldı. Yeni config açılmadı, migration/seed/yayın/push yok. Açık: gerçek model/DB/tarayıcı/yük kabulü; otomatik çok-kaynak seçimi, müşteri/personel/hareket kaynakları, detay raporları, tarih/Guid kırılımları, topN/grafik/kayıt/paylaşım/export. Bu aşama tüm işletme raporlarını desteklediği anlamına gelmez; V2 halen sipariş aggregate adaptörüdür.

## Faz 7d — V2 plan/API çalıştırma bağlantısı (2026-09-10, yerel/kapalı)

- DynamicReportPlan sürüm2 zarfı source/from/to/predicate/aggregate taşır. İlk bağlı kaynak orders; bilinmeyen sürüm/kaynak/property ve yinelenen JSON property reddedilir. En çok16KiB, JSON derinliği24 ve en çok366 günlük timezone'lu dönem; koşul ve aggregate kendi sınırlarını ayrıca uygular.
- Mevcut /api/reports/ai/grid, definition.version=2 dalında yeni motoru kullanır. V1 korunur. /run V2'yi kabul etmez. AiReporting:Enabled yanında AiReporting:DynamicEnabled gerekir; yeni ayar varsayılanfalse, hiçbir ortam ayarı değiştirilmedi. Catalog dynamicEnabled bilgisini döner; model V2 üretmeye veya admin V2 göndermeye henüz geçirilmedi.
- OrderReportExecutor.ExecuteDynamicAsync güncel effective yetkiyi çözer, mevcut kaynak/ilişki kapsamı ve aggregate planını doğrular; V1'le aynı iki slot sınırını paylaşır. SQL çevirisi slot ve30s deadline içinde, veri bağlantısından önce yapılır. READ ONLY/RepeatableRead ve15s statement_timeout korunur.
- ReportAggregateGrid yalnız seçili kolonları filtreler/sıralar; metin araması yalnız metin boyutlarında çalışır, sıfır boyutlu raporda dolu arama açıklayıcı400 verir. Filtrelenmiş tüm grup sayısı ile sayfalı sonuç aynı snapshot'tadır; sayfa en çok250. Ortalama/min/max için yanlış toplama yapmamak amacıyla footer totals boş döner. Sonuç kolon sırası planla aynıdır; audit yalnız sürüm/satır adedi içerir.
- Test: genel API432 başarılı/7 atlandı; son arama ve çeviri sınırı düzeltmeleri sonrası ilgili10/10. PostgreSQL GROUP BY sonrası filtre/ORDER BY/LIMIT SQL çevirisi DB bağlantısı olmadan kontrol edildi. Gerçek DB/model/tarayıcı ve yük kabulü yapılmadı.
- Alt ajan salt-okunur incelemesi yapıldı; toplam raporunda arama ve SQL çevirisinin concurrency sınırı dışında olması bulguları düzeltildi. Yayın/push/migration/seed yok. Açık: V2 yetkili metadata ve AI sözleşmesi, admin bağlantısı, kabul testleri ve ek işletme kaynakları. Tam dinamik raporlama henüz hazır değildir.

## Faz 7c — dinamik gruplama/ölçü/sıralama derleyicisi (2026-09-10, yerel)

- ReportAggregatePlan ve ReportAggregateSchema<T>: onaylı alan kimliklerinden 0–4 metin kırılımı, 1–4 ölçü seçilir; sayım/toplam/ortalama/en düşük/en yüksek ölçüleri sunucu tanımından alınır. Her kombinasyon için ayrı rapor metodu yoktur. Ham property path veya SQL kabul edilmez. Sayısal/Guid/tarih kırılımları ve tarih dilimleme henüz bu derleyicide desteklenmez.
- Seçilen kolonlardan artan/azalan sıralama yapılır; eşit sonuçlarda tüm grup anahtarları deterministik sıra sağlar. EF'e çevrilebilir sabit taşıma alanları kullanılır; dış kolon kimlikleri doğrulanmış planın sırasındadır ve koleksiyonlar snapshot alınır.
- Zorunlu kaynak yetkisi ve satır koruması uygulanır. Tutar ölçüsüne bağlı currencyCode kırılımı zorunludur; count tek başına para birimsiz olabilir. Boş girdi sıfır sonuç satırı üretir; yapay sıfır toplam satırı oluşturulmaz. Sayfalama/query yürütme bu derleyicinin dışındadır, tüm girdi önce gruplanır. Ortalama gibi ölçüler sayfa ortalamalarının ortalamasıyla birleştirilmemelidir.
- OrderReportSource.DynamicSummary mevcut filtreli sorgu üzerinde çalışır ve güncel kanal yetkisini yeniden uygular. Eski Summary ve V1 API/admin akışı korunur. Sipariş adaptöründe durum, ödeme durumu/yöntemi, tip ve para birimi seçilebilir. Tutar GrandTotal'dır; net satış veya tahsilat olarak sunulmaz.
- Test: API428 başarılı/7 atlandı; altı yeni test kombinasyonlar, para birimi/yetki reddi, gerçek PostgreSQL GROUP BY/ORDER BY/COUNT/SUM/AVG/MIN/MAX/LIMIT çevirisi (bağlantı açmadan), boş sonuç, sabit kolon sırası, eşit değer sıralaması ve kanal daraltmayı kapsar. Gerçek DB/model/tarayıcı/yük kabulü yapılmadı. Migration/seed/yayın/push yok.
- Açık: sürümlü ortak plan zarfı ve AI/API/admin bağlantısı; detay kolon projeksiyonu, ek veri türleri/kaynakları, yetkili müşteri/personel alanları, göreli ve kayıt tarihine bağlı dönemler. Yeni motor henüz son kullanıcıya açılmadı; tüm dinamik raporlama tamamlandı denemez.

## Ana gereksinim — dinamik ve yetkiye bağlı işletme raporlaması (2026-09-10)

Kullanıcının son onayı: aşağıdaki eski stok/sipariş fazları nihai kapsam değildir. Her örnek soru için ayrı rapor kodlamak yerine mevcut işletme verileri üzerinde ortak, doğrulanan sorgu planı oluşturulmalıdır. Bu bölüm hedefi kaydeder; aşağıdaki yeteneklerin tamamının çalıştığı anlamına gelmez.

- AI yalnız kullanıcının yetkili olduğu veri kaynaklarının kontrollü alan/ilişki/ölçü sözlüğünü görür. Tüm DB şeması, bağlantı bilgileri veya sonuç satırları modele verilmez. SQL modelden doğrudan çalıştırılmaz.
- Kaynak seçimi kullanıcının teknik tablo bilgisine bağlı olmamalıdır. İstekten ilgili kaynaklar belirlenir; belirsiz ölçü, dönem ve kırılım konuşmayla netleştirilir. Açıkça belirtilmiş tercihler yeniden sorulmaz.
- Ortak plan; detay/özet, ilişkili kayıt var/yok koşulları, gruplama, toplam/sayım, sıralama, ilk N, göreli tarihler ve kaydın tarihine bağlı zaman aralıklarını kapsamalıdır. Yeni bir soru mevcut kaynak/işlemlerle ifade edilebiliyorsa yeni rapor kodu veya derleme gerektirmemelidir. Yeni veri kaynağı veya yeni hesaplama yeteneği ise güvenli geliştirme ve test gerektirebilir.
- Yetki sunucuda kaynak, alan ve kayıt kapsamı için hem planlama hem çalıştırma sırasında doğrulanır. Kaydedilmiş/paylaşılmış tarif yetki vermez. Müşteri/personel alanları ayrı erişim denetimi gerektirir.
- Birden çoğa ilişkiler tutarları veya stok miktarlarını çoğaltmamalıdır. Para birimleri karıştırılmaz. Gerçek ödeme, iade ve stok hareketi kendi kayıtlarından hesaplanır; sipariş durumundan varsayılmaz.
- Sonuç ortak DataGrid üzerinden tüm sonuç kümesinde filtrelenir, sıralanır ve sayfalanır; tablo işlemleri yeniden AI çağrısı gerektirmez. Kullanıcı bazlı kayıt, yetki dahilinde paylaşım ve uygun grafik hedef kapsamındadır.
- Personel performansı isteğinde önce operasyon ve ölçü netleştirilir. Yalnız kaydedilmiş işlem/adet/süre verileri raporlanır; eksik sürelerden hız veya nesnel dayanağı olmayan performans puanı türetilmez.

### Kod incelemesiyle doğrulanan başlangıç noktaları

- ReportDictionary.ForSubject yalnız stock ve orders yönlendiriyor: mevcut uygulama henüz genel raporlama motoru değildir.
- OrderPayment: OrderId, PaymentMethodId, Amount, CurrencyCode ve Status mevcut. Başarılı ödeme statülerinin iş anlamı doğrulanmadan kredi kartıyla ödeyen müşteri hesabı açılmaz.
- Return: OrderId/MemberId, iade durumu, kargonun gönderim/teslim tarihleri ve incelemeyi tamamlayan kullanıcı mevcut. İade talebi, teslim alınan iade ve geri ödeme ayrı olaylardır.
- StockMovement: VariantId, depo yönleri, MovementType, Quantity, CreatedAt ve nullable CreatedBy mevcut. Hareket etmeyen kart hesabı kart/varyant ilişkisi ve zaman aralığıyla yapılmalıdır; kullanıcısı olmayan hareket personele mal edilmez.
- Fulfillment içinde PickingPlanLine/PickingPlan ve operasyon loglarına ilişkin kod var; personel-süre bağlantıları henüz bu incelemede doğrulanmadı.

Sonraki uygulama adımı: mevcut yürütücüleri bozmadan kaynak/ilişki/ölçü ve yetki kataloğunu ortaklaştırmak; plan doğrulayıcı ve ilişkisel sorgu işlemlerini test etmek. Örnek sorular ayrı rapor şablonları değil kabul senaryolarıdır. Eski fazların başarı sayıları yeni genel motorun test sonucu olarak kullanılamaz.

## Faz 6b — sipariş kaynağı AI/API/admin bağlantısı (2026-09-10, yerel/yayınlanmadı)

> Faz 7b — ortak koşul derleyicisi (yerel): ReportPredicate ve ReportPredicateSchema<T>, all/any/not/compare/exists/notExists koşullarını onaylı lambda alanlarına ve ilişkilerine çevirir. Model SQL veya property path veremez. Field/Relation kayıtları sunucu kodudur; kaynak yetkisi ve zorunlu satır koruması sorgu dışında tutulur. İlişkiler Any/EXISTS ile filtreler; miktar/tutarı çoğaltan join üretilmez. Alt ilişki kendi yetki ve satır korumasını uygular. notExists, yalnız yetkiyle görülebilen ilişkili kayıtların yokluğudur; tüm sistemde kayıt yok diye sunulmamalıdır.
>
> OrderReportPredicates, sipariş/ödeme/iade alanlarını gerçek entity ilişkilerine bağlayan ilk kaynak adaptörüdür. OrderReportSource opsiyonel predicate'i mevcut kapsamdan sonra, toplam/sayfalama öncesi uygular. Bu parametre henüz HTTP/AI tarifine veya admin ekranına açılmadı. Eski V1 akış değişmez. Genel projection/grouping/aggregation/sort planı, göreli tarih/kart tarihine bağlı aralıklar, kaynak metadata'sının modele aktarımı ve müşteri/personel kaynakları sonraki iştir.
>
> Sınırlar:16KiB JSON, duplicate/unknown property ret, koşul derinliği6/node64/grup16/değer20; türlenmiş sayısal/Guid/tarih değerleri, timezone zorunlu tarih ve başlangıç dahil/bitiş hariç between. eq/ne/in/contains/gt/gte/lt/lte desteklenir; contains büyük-küçük harfe duyarlıdır. Parametreler EF tarafından parametrelenir. Yalnız IQueryable üretir; DB açma/yazma veya otomatik yürütme yoktur. Test: API422 başarılı/7 atlandı; yedi yeni test, gerçek entity PostgreSQL SQL çevirisi (bağlantı açmadan), injection literal parametresi, ilişki çoğaltmama, yetki/NOT, tarih ve sayfalama öncesi toplamı kapsar. Gerçek DB/model/tarayıcı kabulü yapılmadı.

> Sonraki adım Faz 7a: ortak kaynak kataloğu eklendi. ReportDictionary.ForSubject artık kaynak kataloğuna yönlendirir; çalışabilir kaynaklar halen stok ve sipariştir. Genel ilişkisel sorgu motoru henüz tamamlanmadı.

- Ekranda yetkili kaynak seçimi: güncel stok veya siparişler. Yalnız orders.view sahibi kullanıcı inventory.view olmadan sipariş hazırlayabilir; rapor kullanım izni yine zorunludur. Kaynak değişiminde konuşma/taslak/onay/sonuç temizlenir. Siparişin hazır AI taslağı yokken stok manuel formuna düşme yoktur.
- Aynı version1 tarif zarfında subject=orders, orders.* alanları kullanılır. Sipariş no boyutu detay anlamına gelir; diğer durumda durum/ödeme durumu/kanal/tip ve zorunlu para birimiyle özet üretilir. Tarih between filtresi başlangıç dahil/bitiş hariç, timezone içeren ISO8601 ve en çok366 gündür. Bilinmeyen alan/eksik tarih/para birimi/yetki reddedilir. Stok tarifi aynı kalır.
- Model yalnız seçili yetkili kaynağın sözlüğünü görür. order_dates/order_layout netleştirmeleri geçmişle birlikte taşınır; güncel İstanbul iş tarihi model bağlamına verilir. Gerçek model çağrısıyla göreli tarih doğruluğu henüz ölçülmedi. [OpenAI Docs Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) ile strict schema/additionalProperties=false yaklaşımı korundu; şema uyumu anlamsal doğruluk garantisi değildir.
- OrderReportExecutor: güncel sunucu yetkisi, kanal kesişimi, READ ONLY + RepeatableRead snapshot, sorgu15s/toplam30s, sipariş kaynağı için node başına2 eşzamanlı çalışma. Stok slotları ayrıdır. Temel tarif önce, tablo filtresi sonra uygulanır; detay/özet sayımı ve para birimi toplamları aynı snapshot'ta; sayfa250 satır. Özet sıralamasında tüm grup anahtarları tie-breaker'dır. Para birimleri tek skaler tutarda toplanmaz. Veri sonuçları modele gönderilmez.
- Admin ortak DataGrid filtre/arama/sıralama kullanır, bunlar AI çağırmaz. Sipariş/ödeme etiketleri mevcut orderConstants'tan gelir; detay sayısı her satırda1 olduğundan o kolonda filtre/sıralama yoktur. Detay araması yalnız sipariş no, özet araması gösterilen metin grup alanlarındadır. Tarih İstanbul saatinde gösterilir. Firma seçimi veri yetkisini değiştirmez.
- 409 API başarılı/7 atlandı; admin67/67, tsc ve hedef ESLint temiz. Sipariş tarif/izolasyon/tarih/para birimi/konuşma ve EF özet filtre/toplam SQL çevirileri test edildi. Genel kanal-şema regresyon testi eksik Kanal bildirimi yakaladı, SummarySchema'da düzeltildi; eski frontend metin kontrolleri yeni kaynak davranışına güncellendi.
- Gerçek PostgreSQL salt-okunur kabul1/1 geçti (20s): önceki stok kontrolleri + sipariş yürütücüsü detay/özet/numerik filtre/para birimi toplamı/boş kanal yetkisi. .241/ecommerce_db kimliği ve readonly modu doğrulanmış API01 loopback tüneli kullanıldı; production .59'a erişilmedi. Geçici betik kaldırıldı. İş verisi/DDL yazımı, migration/seed/ayar/yayın/push yok.
- Açık: gerçek AI ve tarayıcı kullanıcı kabulü, yoğun yük; sipariş gün/ay kırılımı, kanal adını UUID'ye yetkili çözümleme, gerçek iade/ödeme/net satış kaynakları, stok hareketi, grafik/kayıt/paylaşım/export. Şu an kaynak seçimi kullanıcı tarafından yapılır, otomatik konu yönlendirme yoktur. Bu aşama bütün raporlama projesinin tamamlandığı anlamına gelmez.

## Faz 6a — sipariş raporu kaynak temeli (aşama geçmişi; bağlantı Faz6b'de)

- OrderReportSource mevcut OrderGrid filtre ve sıralama kurallarını kullanır; mevcut çalışan sipariş listesi değiştirilmedi. Yeni kaynak yalnız sorgu üretir, kendi başına DB bağlantısı/çalıştırma/yazma yapmaz. AI raporlama ve orders.view izinlerinin kanal kümeleri kesiştirilir; istemcinin KanalKisiti yetki kabul edilmez. İstenen kanallar ve tarih/durum kapsamı bu yetkiyi yalnız daraltır.
- Temel tarih aralığı zorunlu, başlangıç dahil/bitiş hariç, en çok366 gün; offset değerleri UTC'ye çevrilir. “Geçen ay” yorumlaması ve gün/ay kırılımının işletme saat dilimi henüz bağlanmadı. Ürün/barkod filtresi mevcut kalem alt sorgusuna yönelir ve ayrıca genel catalog.products.view ister; doğrudan çoklayan JOIN eklenmez.
- Detay projeksiyonu sipariş no/tarih/kanal/durum/ödeme durumu-yöntemi/tip/tutar/para birimidir. Müşteri adı/telefon/adres/not/üye kimliği projeksiyonda, global aramada, filtrede ve sıralamada yoktur. Global arama yalnız sipariş numarasıdır.
- Özet kırılımları status/paymentStatus/firmPlatformId/orderType birlikte seçilebilir. Para birimi her özette zorunlu ayrıdır. Sayım ve kayıtlı GrandTotal toplamı tüm filtrelenmiş kayıtlardan alınır; detay sayfası limiti özeti kesmez. İptal/returned varsayılan gizlenmez, filtreyle seçilir. OrderAmount net satış, tahsilat veya gerçek iade tutarı değildir; gerçek iade kaydı ayrı kaynak olacaktır.
-6 kaynak testi başarılı: izin kesişimi/sahte istemci kapsamı, tarih/soft delete/kanal, tüm kapsam ve para birimi, gizli alan reddi, sınırlar, Npgsql EF SQL çevirisi (bağlantısız). Bu SQL çalıştırma veya tarayıcı kabulü değildir.
- Sonraki iş: bu kaynağın sürümlü AI tarifine ve yetkili katalog/konuşma akışına bağlanması, READ ONLY/snapshot/süre sınırlı yürütücü, sayfalı özet-grid ve admin konu seçimi. Şu anda ekranda sipariş raporu kullanıma açılmadı. Yeni endpoint/DI/seed/migration/yayın yoktur; stok raporu değişmedi.

## Faz 5 — stok sonuçlarında sunucu grid'i (2026-09-10, yerel)

- Onaylanan temel tarif ile tablo filtreleri ayrıldı. Yeni POST /reports/ai/grid yalnız tarifin seçili boyut/ölçü kolonlarına filtre/sıralama kabul eder; her çağrıda güncel yetki ve sözlük tekrar doğrulanır. Metin eşit/içerir/başlar, sayısal eşit/büyük/küçük/aralık ve genel arama parametreli SQL ile çalışır; istemciden SQL kabul edilmez.
- Filtre, tüm yetkili aggregate üzerinde uygulanır; sayım ve ölçü toplamları aynı SQL snapshot'ında, ardından sayfalama hesaplanır. Grid eski ilk1000 satır sınırında kesilmez; sayfa en çok250 satırdır. Eski /run limit davranışı korunur. READ ONLY transaction,15s statement timeout, iki eşzamanlı sorgu/node koruması devam eder; büyük kapsam yük kabulü henüz yapılmadı.
- Admin ortak DataGrid kolon ve gelişmiş filtrelerini, sıralama/arama/sayfalamasını kullanır. Bu işlemler OpenAI çağırmaz. Yeni tarifte eski tablo filtreleri temizlenir; aynı raporu tekrar çalıştırmada korunur. Yükleme/hata sırasında eski sonuç gösterilmez; hatalı filtre kaldırılabilsin diye grid görünür kalır. Sayfa boş olsa bile filtrelenmiş toplam/sayım korunur.
- Salt-okunur kabul1/1 geçti (17s): mevcut gerçek stok/özellik karşılaştırmalarına ek olarak generate_series ile1505 sonuç,1000 sonrasındaki sayfa ve arama, sayısal aralık/ters sıra, sıfır sonuç ve son sayfa ötesinde doğru toplam kontrol edildi. SELECT fixture dışında tablo/DDL/veri yazımı yok; API01 tüneli kapandı, geçici betik kaldırıldı. Production .59'a erişilmedi.
- Bu aşama güncel stok içindir. Sipariş detay/özet ve stok hareketleri, grafik, kayıt/paylaşım/export hâlâ açık; yayın, gerçek OpenAI ve tarayıcı kabulü yapılmadı. Migration/seed/ayar değişmedi.
- Yerel kontroller: son artımlı olmayan test projesi derlemesi0 hata/88 mevcut uyarı; API398 başarılı/7 atlandı, admin66/66, tsc/hedef ESLint/whitespace temiz. Production frontend build çalıştırılmadı.

## Onaylanan hedef ve Faz 4 — konuşarak rapor hazırlama (2026-09-10)

Kullanıcının son kararları önceki dar ilk-sürüm önerilerinden önceliklidir: amaç sabit rapor seçtirmek değildir. Yetkili veriler üzerinde kullanıcının istediği detay/özet, kırılım, filtre ve karşılaştırma dinamik oluşmalıdır. “Stok raporu” gibi eksik istekte asistan kullanıcıyı kısa sorularla yönlendirmeli; açıklanmış bilgiyi tekrar sormamalıdır. “Geçen ayki tüm siparişler” detay listesi de hedef kapsamdadır; önceki ham-kaydı erteleme önerisi bu kararla değişmiştir.

### Bu adımda uygulanan

- ReportConversation, önceki kullanıcı mesajlarını ve yalnız izinli netleştirme kodlarını taşır. İstemciden serbest assistant/system mesajı, SQL, sonuç satırı veya yetki kabul etmez. Eski mesajlar her gönderimde ApprovedReportPrompt gizlilik kontrolünden tekrar geçer; onay hem geçmişi hem yeni mesajı kapsar. History güvenilmeyen bağlamdır, erişim yetkisi değildir.
- En çok8 geçmiş tur ve mevcut mesajla6000 karakter; istek gövdesi32KiB. Sınırda sessiz geçmiş kırpma yoktur. Sunucu DB/Redis konuşma kaydı veya OpenAI previous_response_id kullanılmaz, store=false korunur. İstek kotası her çağrıda aynıdır.
- Model talimatı kısa cevabı önceki soruyla birleştirir, önceki açık seçimleri korur ve tek eksik soru sorar. Netleştirme kodu yanıtta gelir; metin sunucunun sabit güvenli sorusudur. Mevcut desteklenen konu hâlâ güncel stoktur; stok hareketi/sipariş/satış desteği eklenmiş gibi sunulmaz.
- Admin konuşmayı kullanıcıya bağlı component belleğinde gösterir; firma/yeni rapor/manuel geçişte temizler. Hazır olmayan konuşmada manuel rapor çalışmaz. Hızlı cevaplar yalnız metni doldurur, ücretli çağrı veya rapor hesaplaması başlatmaz. Başarısız çağrı mevcut mesajı/geçmişi kaybettirmez. Yeni mesaj için yeniden açık gönderim onayı alınır.
- OpenAI Docs [Conversation state](https://developers.openai.com/api/docs/guides/conversation-state) kullanıldı. Bu uygulama kendi doğrulanmış rapor bağlamını gönderir; modelin gizli reasoning/ham output nesnelerini yeniden oynatmaz. Gerçek model başarısı yerel testten çıkarılmaz.

### Tamamlanmayan sonraki adımlar

1. Sunucu taraflı rapor grid protokolü güncel stok için Faz5'te tamamlandı; yeni veri kaynaklarına aynı güvenlik ve toplam kuralları uygulanacak. Tarayıcı/yük kabulü bekliyor.
2. Rapor veri kaynakları: güncel stok, stok hareketleri ve sipariş detay/özet modelleri; mevcut kolon/iş anlamı/kardinalite ve yetki kurallarıyla güvenli sorgu planı. Tek tek rapor isimleri değil veri kaynakları ve işlemler yetkilendirilir. Şema bilgisi tek başına yetki veya doğru hesap formülü değildir.
3. Sipariş grid'inin OrderGrid/DataGrid davranışı yeniden kullanılmalı, fakat müşteri/telefon gibi alanlar otomatik model bağlamına taşınmamalı. Tarih/kanal taban kapsamı kullanıcı grid filtresiyle genişletilemez. İade kaydı ve sipariş durumu ayrıdır; kısmi iade tek bir returned bayrağına indirgenmez.
4. Sonuçta filtre/sıralama/sayfalama OpenAI çağrısı yapmaz. Sayım/toplam aynı filtreyle hesaplanır. Grafik, rapor kaydı/paylaşımı ve export ayrıca tamamlanacaktır.

Yeni tablo/migration/seed, DB yazımı, harici model çağrısı, sunucu işlemi veya yayın yoktur. Bu kayıt tüm dinamik raporlama işinin tamamlandığı anlamına gelmez.

## Güncel ek — dinamik özellikli stok raporu (2026-09-10, yerel)

Önceki bölümler aşama geçmişidir. Cinsiyet/renk/beden/sezon gibi seçimli özellikler artık kodda tek tek listelenmez: mevcut aktif özellik tanımlarından attribute.UUID sözlüğü oluşturulur. Yeni tablo, kolon, özellik tipi/değeri veya seed eklenmedi. Yayın henüz yapılmadı.

- Ortak sözlük API kataloğu, modelin strict JSON şeması, tarif doğrulayıcı ve salt-okunur sorgu derleyicisini besler. Dinamik alanlar genel catalog.products.view iznini de gerektirir; yetkisiz filtreleme veya kapsamlı izni genele çevirme yoktur.
- Varyant değeri ürün değerinden önceliklidir. Bir stok satırının çoklu özelliği birleşik bir değer kümesidir, her seçeneğe ayrı yazılıp stok çoğaltılmaz. Eksik özellik Belirtilmemiş grubunda kalır. Sadece aktif select/multi_select tanımları ve aktif, silinmemiş seçenekler desteklenir; CustomValue ve serbest metin/JSON çıkarımı yapılmaz. Türkçe değer adları eq/in parametreleriyle filtrelenir.
- Ürün özellikleri en çok 256 tanım ve 128 karakter tanım adı/kod ile sınırlıdır; taşmada sessiz eksik sözlük yerine hata döner. Tarifin mevcut üç gruplama/sekiz filtre/1000 satır/süre sınırları korunur.
- Stok türü belirtilmemiş bir özellik raporunda fiziksel/sanal ayrı stockType kırılımı önerilir; kullanıcının açık toplam isteği olmadan birleştirme yapılmaz. Bu model talimatıdır; gerçek model değerlendirmesi hâlâ gereklidir. Desteklenmeyen satış/maliyet raporu stok raporuna çevrilmez.
- Resmi kaynak: [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs). Şema uyumu anlamsal doğruluk garantisi değildir; model cevabı yine sunucuda doğrulanır, otomatik çalıştırılmaz.
- Test: 385 API başarılı/7 atlandı, admin64/64, tsc ve hedef eslint temiz. Gerçek yeni sistem DB'de aynı snapshot'ta EF sözlüğü + dinamik kırılım toplamları ve bağımsız stok toplamı eşleşti; salt-okunur kabul1/1 (16s). VALUES-only CTE ile varyant önceliği, çoklu/tekrarlı seçim, eksik/pasif değer ve Türkçe filtre kabulü yapıldı. Temp tablo/iş verisi yazımı yok. Tünel kapandı, geçici betik silindi.
- Gerçek OpenAI çağrısı, tarayıcı, eşzamanlı yük ve yayın yapılmadı. Diğer rapor konuları, grafik, kullanıcı kaydı/paylaşımı/export ve panelden talimat yönetimi kalan işlerdir; bütün AI raporlama işi tamamlanmış sayılmaz.

Durum: OpenAI firma entegrasyonu şeması, stok tarifi/doğrulayıcısı, salt-okunur yürütücü ve panel/API hazırlığı yerelde kodlandı. Gerçek PostgreSQL salt-okunur hesap kontrolü geçti. Hesaplama varsayılan kapalıdır; yük/HTTP/UI kabulü ve doğal dil OpenAI bağlantısı henüz yapılmadı. Kullanıcı ortak stok erişimi + rapor iznini onayladı. Veri aktarımı ve ilk sürüm kapsamının kalan kararları açık. Bu belge tamamlanmış özellik değildir.

Kaynak: Kullanıcının ECSProsAI_AI_Raporlama_Degerlendirme.pdf belgesi (5 sayfa).

## Korunacak kararlar

- Sabit sipariş/tedarik raporları geri eklenmeyecek. AI raporlama ayrı bir işlev olacak; mevcut Stok Durumu korunacak.
- AI serbest SQL üretip çalıştıramaz. Sadece izin verilen rapor tarifini önerir; sunucu doğrular ve hesaplar.
- Rapor dışı sorular yanıtlanmaz. İşletme verisi olarak satış, stok ve sonraki onaylı alanlar desteklenir; site analizi kapsam dışıdır.
- Kayıtlı rapor sonuç değil, sürümlü tariftir. Açma, çalıştırma, paylaşma ve dışa aktarmada güncel yetki tekrar kontrol edilir.
- Konu, satır/firma/platform, alan ve işlem izinleri birbirinden ayrılır. Gizli alanla filtreleme de yasaktır. Paylaşım ek veri yetkisi vermez.
- Fiziksel, rezerve ve kullanılabilir stok ayrı ölçülerdir. Ödeme/iade ilişkileri doğrudan birleştirilerek sipariş tutarları çoğaltılmaz; her kaynak kendi tanecik düzeyinde önce toplanır.
- Belgeden çıkarılmış V3/legacy aktarım gecikmesi maddesi yeniden gereksinim yapılmaz.

## Kullanıcıya önerilen, henüz onaylanmamış ilk sürüm

1. Satış + stok ile başlamak; maliyet/kâr ve diğer alanları hesap tanımları onaylandıktan sonra eklemek.
2. Kur dönüşümü yapmamak; parasal sonuçları kaynak para birimiyle ayrı göstermek. Birden fazla para birimini tek toplamda birleştirmemek.
3. Ham kayda inişi ilk sürüm dışında tutmak; sonraki aşamada ayrı izinle eklemek.
4. AI'a sonuç satırları veya müşteri bilgileri göndermemek; yalnız istek ve yetkili sözlük tanımlarını göndermek. Kullanıcı metnindeki kişisel veriler için ayrıca gönderim öncesi koruma gerekir.

## Uygulama sırası

1. Tek kaynaklı metrics/dimensions/filters sözlüğü, sürümlü tarif şeması, izin doğrulayıcı ve sınır testleri.
2. Salt-okunur, sınırlı sorgu yürütücüsü: tarih/satır/süre limitleri, iptal desteği, para birimi ayrımı ve mükerrer toplam regresyonları. Satış tutarı, iptal ve kısmi iade formülleri ayrıca doğrulanacak.
3. Admin ekranı: istek alanı, “Seni şöyle anladım” onayı, netleştirme seçenekleri, mevcut DataGrid ile sonuçlar, tablo/grafik geçişi. Uydurma demo sonuçlar gerçek veri gibi gösterilmeyecek.
4. Kullanıcıya özel tarif kaydı ve yetkili paylaşım; sahiplik devri, sürüm uyumu ve erişim iptali testleri. Yeni tabloların migration'ı ayrı incelenecek ve hedef DB onayı olmadan uygulanmayacak.
5. Ayrı izinli CSV/XLSX, güvenli hücre çıktıları, denetim kaydı; büyük işler için limit veya kuyruk. Replica kararı ancak yük ölçümüyle.
6. Sağlayıcı bağlandıktan sonra 50–100 istekli değerlendirme: en az %90 tarif eşleşmesi, sessiz yanlış varsayım sıfır. Model kesilince kayıtlı tarifin modelsiz çalışması doğrulanacak.

## Açık işletme kararları

- AI sağlayıcısı: OpenAI onaylandı. Firma entegrasyon şemasında apiKey (şifreli credentials/password) ve model (settings/text) zorunlu alanları hazırlandı. Hesap/API anahtarı kullanıcı tarafından panelden girilecek. Gönderilebilecek veri, saklama ve erişim politikası henüz kesinleşmedi. Harici çağrı yapılmadı.
- Satış + stok ile başlangıç, döviz ayrımı ve ham kayda iniş ertelemesi: öneri, henüz onay değil.
- Sayısal limitler, maliyet bütçesi ve finansal formüller: veri hacmi ve iş kurallarıyla belirlenecek; tahminle onaylı sayılmayacak.

## Bu incelemede yapılanlar

- PDF tamamen okundu; mevcut kaynakta OpenAI/Anthropic/AiReport entegrasyonu adları tarandı, eşleşme bulunmadı.
- Yerel çalışmalar korundu. Uygulama kodu, DB, servis ve yayın değiştirilmedi; yeni test sonucu iddia edilmiyor.

## Faz 1 ilerleme — tarif sözleşmesi (2026-09-10)

- Yukarıdaki “Bu incelemede yapılanlar” ilk belge okuma adımının tarihsel kaydıdır. Sonraki adımda Services/AiReporting altında saf sözlük ve JSON tarif doğrulayıcı eklendi; API endpoint'i, DI kaydı veya veri sorgusu henüz yoktur.
- Sürüm 1 sözlük şu an yalnız stok miktarı, rezerve ve kullanılabilir miktarı içerir. Ürün kodu, depo kimliği ve stok türü gruplama/eq-in filtre alanlarıdır. Satışın kapsam dışında bırakılması nihai ürün kararı değil; onaysız finansal formül çalıştırmamak için geçici geliştirme sınırıdır.
- Kullanım için reports.ai.use + inventory.view, dışa aktarım için ayrıca reports.ai.export aranır. Bu izin adları henüz IAM kataloğuna eklenmedi/kimseye atanmadı. Sunucu izinlerini modele veya kullanıcı tarifine bırakmak yasaktır.
- Bilinmeyen özellikler (SQL/firma/yetki dahil), tekrar eden JSON anahtarları, null alanlar, geçersiz tipler/sürümler, yetkisiz filtreler reddedilir. Tablo ve tek boyutlu sütun grafiği sözleşmesi var; görselleştirme henüz yoktur.
- Geçici koruyucu geliştirme üst sınırları: 16 KiB JSON, derinlik 8, 1000 sonuç satırı, 3 ölçü/3 boyut, 8 filtre, filtre başına 20 değer/128 karakter. Bunlar performans kabulü veya işletme bütçesi onayı değildir.
- Doğrulanan tarif satır yetkisi verilmiş anlamına GELMEZ. Yürütücüde güncel, onaylı veri kapsamı sunucudan çözülmeli; literal değerler parametrelenmeli; alanlar sabit sorgu ifadelerine bağlanmalı. UI veya AI tarafından bildirilen firma kapsamı kabul edilmemeli. Önceki firma/platform/depo kapsamı ifadesi mevcut sistemle doğrulanmamış varsayımdı; aşağıdaki inceleme bu varsayımı düzeltir.
- Fiziksel/sanal türler kaynakta birlikte bulunur. stock.quantity salt fiziksel diye etiketlenmez; stockType=physical açıkça seçilir. Kullanılabilir miktar Quantity-ReservedQuantity; negatif değer gizlenmez. Çoklu raf satırları sayılır, rezervasyon tablosuyla join edilmez.

## Yürütücü öncesi kapsam uyuşmazlığı — karar verildi

- docs/panel-yetki-sistemi-tasarim.md K1 ve N bölümleri: kapsam birimi satış kanalıdır; ayrı firma/depo kapsamı bilinçli olarak dışarıda bırakılmıştır. Firma kanaldan türetilir.
- IAM EtkinYetkiServisi izinleri kanal kümeleriyle hesaplar. Kullanıcıda FirmId olsa da bu, depo sahipliği tanımı değildir. Stock ve Warehouse entity'lerinde FirmId/FirmPlatformId yoktur; mevcut stok export sorgusu stok listesini depo/ürün filtreleriyle sorgular, firma sahipliği çözmez.
- Dolayısıyla “firma/depo yetkilerini hazır sistemden alacağız” varsayımı yanlıştı. AI'ın seçtiği warehouseId yalnız kullanıcı filtresi olabilir; yetki kanıtı değildir.
- Kullanıcı onayı: stok raporu mevcut stok ekranının erişim modeliyle, inventory.view + ayrı AI rapor izniyle ortak stok verisini görür; satış raporları mevcut kanal yetkisini izleyecek. Ayrı firma/depo stok izolasyonu eklenmeyecek.
- Bu karar verilmeden veri yürütücüsü/API açılmadı. Kodda izin genişletilmedi, DB veya sunucuya dokunulmadı.

## Faz 2 — yerel stok yürütücüsü

- StockReportQuery: doğrulanmış sözlük kimlikleri sabit PostgreSQL ifadelerine bağlanır. Filtreler text[]/uuid[] parametreleridir. Stok soft-delete filtresi uygulanır; ürün kırılımında yalnız tekil PK üzerinden LEFT JOIN yapılır, kayıp katalog kaydı toplamdan sessizce düşmez (ürün kodu null olabilir). Rezervasyon/ödeme gibi çoğaltıcı ilişki join'i yoktur.
- StockReportExecutor: kullanıcı kimliğiyle mevcut IEtkinYetkiServisi her çağrıda okunur (servisin mevcut cache süreleri geçerlidir). Kapsamlı bir izin genel stok erişimine dönüştürülmez; kanal sınırlı inventory.view veya rapor izni bu yürütücüde reddedilir. Süper admin mevcut sistem kuralıyla desteklenir.
- READ ONLY transaction, 15 saniye PostgreSQL statement_timeout, 20 saniye komut timeout, iptal aktarımı, node başına en çok 2 eşzamanlı rapor uygulanır. Bunlar geçici koruyucu geliştirme sınırlarıdır; çoklu-node global kota veya canlı yük kabulü değildir.
- limit+1 ile taşma tespit edilir; eksik satırlarla tam rapor izlenimi verilmez. Toplamda bigint dönüşümü çıkarma işleminden önce yapılır; negatif kullanılabilir stok korunur. Miktarlar PostgreSQL SUM(bigint) nedeniyle numeric dönebilir; yürütücü değeri değiştirmez.
- Henüz DI/HTTP kaydı, panel sonucu, audit, export, kayıt/paylaşım veya OpenAI çağrısı yok. Gerçek PostgreSQL sorgu yürütme ve yük kabulü yapılmadan yayın hazır sayılmaz. Anahtar entegrasyon kaydı da hedef DB'ye uygulanmadı.

## Faz 3 başlangıcı — panel ve API bağlantısı (yerel)

- Önceki Faz 2 kapanışından sonra /reports/ai admin sayfası ve GET /api/reports/ai/catalog, POST /api/reports/ai/run eklendi. Stok yürütücüsü DI'a kaydedildi. reports.ai.use kod sahipli izin kataloğuna eklendi; hedef DB katalog senkronu çalıştırılmadı, kimseye izin atanmadı.
- Açık rollout kapısı AiReporting:Enabled varsayılan false. Kaynak appsettings veya sunucu ayarı değiştirilmedi. Kapalıyken katalog alanları yetkili kullanıcıya gösterilir, run 503 verir ve stok DB sorgusu yapılmaz. Gerçek DB ve yük kabulünden önce açılmamalı.
- Panel mevcut DataGrid ile tablo, kullanıcıya göre izole state, kullanıcı tarafından başlatılan hesaplama, ürün tam eşleşmesi, stok türü ve tek alan gruplamasını içerir. Form değişince/eski istek başarısız olunca eski sonuç gösterilmez. Sonuçlar localStorage'a yazılmaz. Doğal dil yorumlamanın bağlı olmadığı açık yazılır; çalışmayan sohbet veya sahte veri yoktur.
- HTTP tarafı etkin izinleri tekrar kontrol eder; kapsam sınırlı izin ortak stok erişimine dönüşmez. Yanıtlar no-store; body 16 KiB. DB ayrıntısı hata mesajına konmaz. Başarılı hesaplamada mevcut IVitrinAuditLogger yalnız sürüm/satır sayısını kaydeder; filtre/prompt/sonuç değerleri loglanmaz. Bu logger mevcut best-effort davranışındadır: kesin audit garantisi değildir ve ileride hassas rapor/export açılmadan değerlendirilmelidir.
- Eksik: gerçek PostgreSQL sonuç/yük kabulü, tarayıcı görsel kabulü, OpenAI yorumlama/PII koruması, grafik, kayıt/paylaşım ve export. Bu ekran bir rapor hazırlama ara aşamasıdır; AI raporlamanın tamamı değildir. Yayın yoktur.

## Gerçek veri doğrulaması — 2026-09-10

- AiStockReadAcceptanceTests eklendi: yalnız ECSPROS_ACCEPTANCE_AI_STOCK_READ açıkça verildiğinde çalışır. Loopback tüneli, ecommerce_db ve gerçek sunucu IP .241 doğrulanır; default_transaction_read_only=on + 15 saniye statement timeout zorlanır. Aynı REPEATABLE READ snapshot'ı içinde hesaplar karşılaştırılır; test sonunda rollback yapılır.
- API01 üzerinden geçici 127.0.0.1:15439 tüneliyle yeni sistem .241/ecommerce_db'de 1/1 test geçti (test süresi yaklaşık 1 saniye). Üretilmiş gerçek StockReportQuery SQL'i çalıştırıldı: genel toplam, depo/stok türü kırılım toplamı, physical filtresi ve mevcut bir ürünün productCode filtresi. Ürün referansı PK JOIN yerine EXISTS semijoin ile bağımsız kontrol edildi. Rakamlar/ürün verisi terminale veya bu belgeye yazılmadı.
- İlk denemeler hedef guard'ında durdu: PowerShell DbConnectionStringBuilder erişimi get_Item/set_Item ile düzeltildi; inet_server_addr text çıktısının /32 biçimi host(inet_server_addr()) ile normalize edildi. Hedef kısıtlaması kaldırılmadı. Ardından test başarılı oldu.
- Test bağlantısı yokken atlanır; atlama başarı sayılmaz. İlk bağlantısız koşu atlandı, yukarıdaki başarılı koşu gerçek bağlantılıdır. Production .59/MySQL/Nginx değişmedi, yeni sistem verisi de değiştirilmedi. Geçici tüneller finally ile kapandı ve bu işin geçici PS dosyası kaldırıldı; kalıcı regression testi korundu.
- Sınır: yalnız SQL hesap doğruluğu. HTTP/auth/audit uçtan uca, eşzamanlı yük ve UI görsel kabulü henüz yapılmadı. AiReporting:Enabled açılmadı; yayın yapılmadı.

## OpenAI yorumlama adapter'ı — yerel, henüz erişime açılmadı

- Resmi Structured Outputs belgesi esas alındı: https://developers.openai.com/api/docs/guides/structured-outputs . Responses API text.format json_schema/strict kullanılır. Şema ve prompt aynı izinli ReportDictionary alanlarından oluşturulur; ready tarif ayrıca mevcut doğrulayıcıdan geçer.
- OpenAiReportContract yalnız ready/clarify/out_of_scope kararı ve nullable tarif kabul eder. Netleştirme mesajları yerel sabitlerden gelir; modelin serbest genel sohbet cevabı/HTML'i ekrana aktarılmaz. Refusal, incomplete/failed, hatalı JSON, yinelenen/bilinmeyen alan ve yetkisiz tarif reddedilir. Şema uyumu, isteğin anlamının doğru yorumlandığını KANITLAMAZ; 50–100 istekli gerçek eval kapısı hâlâ açık.
- OpenAiReportInterpreter sabit https://api.openai.com/v1/responses adresini kullanır. HttpClient yönlendirme izlemez; anahtar her isteğin Authorization header'ında, ortak DefaultRequestHeaders'da değil. 30s süre ve 64KiB yanıt sınırı var; otomatik retry yok. Sağlayıcı hata gövdesi kullanıcıya/loga aktarılmaz. Cancellation çağırana iletilir.
- store=false gönderilir; bu seçenek “OpenAI hiçbir veri saklamaz” anlamında bir güvence değildir. KVKK/retention onayı ayrı kalır. Adapter kullanıcı metnini gönderir; metin kişisel bilgi içerebileceğinden gönderim öncesi gizlilik kontrolü yapılmadan endpoint'e bağlanmayacak. Sonuç satırları veya müşteri verisi için adapter parametresi eklenmedi.
- DI kaydı var ama controller çağrısı yok; firma entegrasyonu çözümleme, kota ve izinli gönderim/PII koruması henüz bağlanmadı. Panel naturalLanguageEnabled=false kalır. Bu tur gerçek OpenAI anahtarı okunmadı ve OpenAI'a çağrı yapılmadı; yalnız mock HTTP/yanıt testleri çalıştırıldı. Model seçimi kullanıcıya ait; belirli bir modelin hesapta erişilebilirliği iddia edilmedi.

## Faz 3 — onaylı doğal dil taslağı bağlantısı (2026-09-10)

- Önceki aşağıdaki kayıtlar tarihsel aşamalardır. Artık POST /api/reports/ai/interpret firma ayar çözümleyicisi ve OpenAI adapter'ına bağlıdır; AiReporting:NaturalLanguageEnabled varsayılan false olduğundan harici çağrı kapalıdır. Rapor hesaplamanın ayrı AiReporting:Enabled kapısı da açılmadı.
- ApprovedReportPrompt açık gönderim onayı, uzunluk/görünmez karakter denetimi ve belirgin e-posta, telefon/kimlik/kart, IBAN, anahtar ve URL kalıplarını denetler. Bu sezgisel kontroldür; tam kişisel veri tespiti veya anonimleştirme garantisi değildir. Metin sessizce değiştirilmez ve hata metnine geri basılmaz.
- Panelde metin + gönderim onayı → Taslak hazırla → kapsamı gör → Raporu çalıştır akışı var. interpret hiçbir stok hesaplaması başlatmaz. Metin değişikliği onayı/taslağı/eski sonucu sıfırlar. Sonuç satırları OpenAI'a gönderilmez; yalnız istek ve yetkili sözlük aktarım sözleşmesindedir.
- Endpoint güncel yetkiyi çağrı öncesi/sonrası kontrol eder. Audit yalnız durum/entegrasyon kimliğidir; mevcut best-effort davranışı kesin kayıt garantisi değildir. Typed AddHttpClient kaydını tanımayan kaynak tabanlı DI regresyon testi bu geçerli kayıt biçimini destekleyecek şekilde düzeltildi.
- Açık işler: süper admin için panelde açık firma seçimi (şimdilik kullanıcı FirmId bağlamı), çoklu-node kullanıcı/firma kotası, veri aktarımı/saklama politikası ve gerçek model/HTTP/görsel/yük kabulü. Grafik, kayıt/paylaşım/export ve diğer rapor konuları tamamlanmadı. Gerçek anahtar okunmadı, harici çağrı/DB seed/migration/yayın yapılmadı.

## Faz 3 — açık firma seçimi (2026-09-10)

- GET /api/reports/ai/firms güncel rapor/stok iznini denetler. Aktif DB kullanıcısının firma bağı ve IsSuperAdmin değeri esas alınır: normal kullanıcı yalnız kendi aktif firmasını, süper admin aktif firmaları görür. Firma bağı olmayan normal kullanıcı boş liste alır; yetki genişletilmez.
- SQL projeksiyonu yalnız Id/Code/NameI18n içerir. Vergi/iletişim bilgileri, entegrasyon kayıtları veya credentials çekilmez. Firmayı listede görmek hesabın yapılandırılmış olduğu garantisi değildir; interpret sırasında mevcut tekillik/aktif entegrasyon kontrolleri yeniden uygulanır.
- Panel firma seçimi açık ve zorunludur; ilk kayıt otomatik seçilmez. Firma değişince gönderim onayı/taslak/sonuç temizlenir. Sorgu cache anahtarı kullanıcı bazlıdır; yükleme/hata/boş liste ayrı gösterilir. Stok veri kapsamı firma seçimiyle değiştirilmez.
- API regresyonu: 364 başarılı / 7 atlandı / 0 hata. İki yeni test görünür firma kapsamını ve SQL'in hassas alan çekmediğini doğrular; controller yetkisiz listede servise erişmez. Gerçek DB veya OpenAI bağlantısı/yayın yok. Dağıtık kullanım kotası henüz uygulanmadı.

## Faz 3 — paylaşılan AI istek kotası (2026-09-10)

- AiReportQuota + RedisAiReportQuotaStore, mevcut kritik-state IConnectionMultiplexer üzerinden çalışır; memory/cache fallback yoktur. Interpret firma doğrulamasından sonra, OpenAI çağrısından önce hak ayırır. Eksik/geçersiz ayar veya Redis/süre hatası 503; kota doluysa 429 + Retry-After. Süper admin de kotaya tabidir.
- Zorunlu ayarlar: AiReporting:Quota:Scope (1–64 ASCII harf/rakam/tire/alt çizgi), UserPerMinute, UserPer24Hours, FirmPer24Hours (1–1.000.000 tamsayı). Varsayılan kota sayısı uydurulmadı; onaylanan değerler tüm node'larda aynı olmalı. Aynı ortam aynı scope/state Redis'i, farklı ortam ayrı scope kullanmalı. appsettings/sunucu ayarı değiştirilmedi.
- Üç sayaç tek Lua çağrısında kontrol/artanır, ortak cluster hash etiketiyle atomiktir. Kullanıcı sayaçları firma değiştirilince sıfırlanmaz; firma sayacı tüm kullanıcıların kullanımını birleştirir. Pencereler ilk hak ayırmada başlayan 60 saniye/24 saattir; takvim günü veya kayan pencere değildir. Sabit pencere sınırında kısa süreli çift pencere kullanımı mümkündür.
- Başarısız/iptal edilmiş model çağrısının hakkı iade edilmez. Redis bekleme zaman aşımında komut sonradan işlenmiş olabilir; OpenAI yine çağrılmaz, hak tüketilmiş olabilir. Otomatik tekrar yok. Sayaçlarda prompt/anahtar/sonuç saklanmaz; yalnız kullanıcı/firma UUID ve sayılar tutulur, TTL ile silinir.
- Bu istek sayısı kotasıdır; token/para bütçesi, eşzamanlı model çağrısı sınırı veya kalıcı muhasebe defteri değildir. Redis eviction/veri kaybı sayaçları sıfırlayabilir: state persistence/no-eviction işletim doğrulaması ve gerçek iki-node yarış/TTL kabulü yapılmadan özellik açılmaz. Yeni tablo/migration veya gerçek Redis/OpenAI çağrısı yapılmadı.

## Kota Redis kabul hazırlığı — 2026-09-10

- Yerel redis-server/Docker bulunamadı; wsl --list --quiet WSL kurulu değil sonucu verdi. Kurulum veya uzak production Redis'e bağlantı yapılmadı.
- Acceptance/AiQuotaRedisAcceptanceTests eklendi. Yalnız ayrılmış, atılabilir 127.0.0.1:16379 Redis ve ECSPROS_ACCEPTANCE_AI_QUOTA_DISPOSABLE_REDIS=1 açık yazma onayıyla çalışır. appsettings/production bağlantıları okunmaz. Bu porta production Redis tünellenmemelidir.
- Aynı üretim Lua script'i iki bağımsız client bağlantısıyla test edilir: kullanıcı yarış sınırı, farklı kullanıcıların ortak firma sınırı, kısa pencere dolarken günlük sayacın korunması, bozuk state'te diğer sayaçların artmaması. Bu iki process veya canlı API01/API02 testi değildir. TTL testi script parametresini 100ms yapar; üretim süreleri değiştirilmez.
- Her test benzersiz beş key kullanır; finally yalnız bu key'leri temizler. FLUSH/SCAN/wildcard yoktur; beklenmeyen kesintide TTL kalıntıları sınırlar.
- Yerel hedefli koşu: 8 unit testi başarılı, 4 Redis kabul testi ortam yokluğunda ATLANDI. Atlananlar kabul başarısı değildir. İzole Redis sağlanmadan yarış/TTL doğrulaması tamamlanmış sayılmaz; özellik kapalı kalır.

## Mevcut state Redis gerçek kabul sonucu — 2026-09-10

- Kullanıcının açık onayıyla API01 üzerinden 192.168.0.243:6380 state Redis kullanıldı. Hedef ecspros.service process/config'inden doğrulandı; cache 6379 veya .59 değiştirilmedi. Anahtar yalnız sunucu belleğinde okundu, loglanmadı.
- Mevcut üretim Lua kaynaktan alınarak iki bağımsız RESP bağlantısıyla çalıştırıldı: kullanıcı yarışı 5/40 kabul, ortak firma yarışı 7/40 kabul, kısa TTL sonrası uzun kotanın korunması ve bozuk sayaç ret kontrolü 4/4 geçti. Pencereler testte kısaltıldı; kaynak üretim süreleri aynı kaldı.
- Beş benzersiz test key'i DEL sonrası EXISTS=0 doğrulamasıyla temizlendi. Yerel geçici Python dosyası silindi; uzak dosya/ayar değişmedi. Kalıcı C# testleri yerinde durur. Bu C# test sınıfının koşusu veya iki-node HTTP/OpenAI testi olarak raporlanmaz; gerçek Lua atomikliği/TTL kabulüdür.
- Feature flag/ücretli OpenAI çağrısı açılmadı. Tam uygulama uçtan uca kabulü, işletme limitlerinin belirlenmesi ve Redis persistence/eviction işletim kontrolü hâlâ ayrı işlerdir.

## Controller yorumlama akışı kabulü — 2026-09-10

- IOpenAiFirmSettingsProvider arayüzü eklendi; aynı üretim sağlayıcısı DI üzerinden kullanılır. Firma erişim kuralları/credential çözümleme davranışı değişmedi. Arayüz testte gerçek DB/anahtar yerine kontrollü sınır kullanmak içindir.
- AiInterpretFlowTests gerçek controller, AiReportQuota, OpenAiReportInterpreter ve tarif doğrulayıcıyı birlikte çalıştırır; firma deposu, kota deposu, audit ve HTTP taşıması test karşılığıdır. Onaysız/hassas istek, yetkisiz kullanıcı, kota dolu/erişilemez, başarılı taslak ve model beklerken yetki iptali denetlenir. Başarıda executor null bırakılarak otomatik stok hesaplaması yapılmadığı doğrulanır; audit'e anahtar/tarif içeriği yazılmaz.
- Bu ASP.NET middleware/authentication/model binding veya tarayıcı/gerçek OpenAI uçtan uca testi değildir. Modelin anlamsal doğruluğu da mock yanıtla kanıtlanmaz. Bu tur harici çağrı, DB veya Redis yazımı, yayın ve feature flag değişikliği yoktur.

## Test ortamı yayını — 2026-09-10

- Kullanıcı onayıyla API01/API02 ve mevcut admin hedefi 20260910_ai_reporting release'ine geçti. İki özellik kapısı açıldı; ortak multi-test quota kullanıcı3/dakika,20/24saat ve firma50/24saat olarak doğrulandı. Önceki kapalı durum notları tarihsel kayıttır.
- .241 DB'de yalnız OpenAI servis form kataloğu eklendi. Firma credentials/model veya yetki rolü oluşturulmadı. Genel seed/migration, worker veya .59 değişikliği yok. Nginx'te yalnız onaylanan admin yayını, config/reload yok.
- İki API sağlık/ana sayfa200, anonim katalog401, DLL hash/kota doğrulaması ve başlangıç hata kontrolü temiz. Admin HTML/asset içerik eşleşmesi geçti. Aktarım arşivleri temizlendi, geri dönüş release'leri korundu.
- Kullanıcı anahtar/modeli firma-geneli entegrasyona girmeden gerçek model kabulü yapılamaz. Ücretli çağrı, yetkili tarayıcı akışı, anlam doğruluğu, kayıt/paylaşım/grafik/export ve diğer rapor konuları tamamlandı sayılmaz.

## Açık stok isteğine gereksiz netleştirme ve manuel kapsam karışması düzeltmesi

- Kullanıcı ekranında “Fiziksel stokların genel toplamını tablo olarak göster” isteğine stock_type netleştirmesi geldi. Önceki talimatta fiziksel/physical ve genel toplam/boş dimensions eşlemeleri açık değildi. Bu eksiklik düzeltildi; tek başına model cevabının kesin kök nedeni veya gelecekte yüzde100 başarı kanıtı olarak değerlendirilmez.
- OpenAiReportContract talimatına fiziksel/fiziki→physical, sanal→virtual, açık birlikte istek→stockType filtresi yok, genel toplam→dimensions=[] ve ölçü eşlemeleri eklendi. Olumlu örneklerin yanında kararsız/çelişkili istekte netleştirme korunur. Şema/yetki doğrulaması atlanmaz; anahtar sözcükle hazır tarif dayatan fallback veya ücretli otomatik retry eklenmedi. Resmi kaynak: https://developers.openai.com/api/docs/guides/structured-outputs .
- AI metni dolu ve hazır taslak yoksa AiReportsPage form submit ve düğme üzerinden manuel raporu çalıştırmaz. Manuel kapsam gizlenir, nedeni açıklanır. Elle devam ancak açık temizleme düğmesiyle istek/onay/taslak/sonuç sıfırlanarak yapılır. Hazır taslak ve metinsiz manuel akış korunur.
- Kod/test düzeltmesi yereldir; bu adımda yayın/DB/anahtar/harici model çağrısı yapılmadı. Gerçek modelde aynı istemin yeniden denenmesi ve görsel kabul sonraki yayın adımıdır.

## Kapsam düzeltmesi yayın sonucu

- 2026-09-10: kullanıcı build'i sonrasında20260910_ai_scope_fix API01/API02 ve admin hedefine yayınlandı. Açık Türkçe eşleme/örnek talimatı ve taslak yokken manuel fallback engeli devrede. Panelden talimat yönetimi önerisi uygulanmadı.
- API sağlık/başlangıç/hash/kota ve admin asset içerik kontrolleri geçti; önceki ayarlar/anahtarlar/DB ve worker'lar değişmedi. Aktarım arşivleri temizlendi, geri dönüş release'i korundu. Kullanıcının aynı istemle gerçek model sonucunu yeniden doğrulaması bekleniyor.

## Firma entegrasyonu çözümleme — yerel altyapı

- OpenAiFirmSettingsProvider eklendi. Kullanıcı DB'den aktif/silinmemiş olarak okunur; normal kullanıcı yalnız kendi User.FirmId kaydını kullanabilir. Firma belirtilmediğinde rastgele/ilk firma seçilmez. Süper admin başka firmayı açıkça seçebilir; firma bağlamı yoksa hata verir. Bu, stok veri kapsamı değil harici hesabın/harcamanın sahibini seçme sınırıdır. Firma ataması olmayan normal kullanıcı için kanal/grup üyeliğinden hesap kullanma yetkisi türetilmedi; böyle bir iş ihtiyacı ayrı netleştirilmeli.
- Firma-geneli (FirmPlatformId null), aktif durumlu, tarih aralığı geçerli, silinmemiş openai_reporting/ai_reporting kaydı aranır. Firma ve servis kataloğu da aktif olmalıdır. Önce yalnız kimliklerle tekillik kontrol edilir; sıfır veya birden fazla kayıt varsa credentials okunmaz. Ardından aynı uygunluk sorgusuyla tek kayıt alınır. Başka firmaya, platforma veya appsettings'e fallback yok; global key cache yok.
- API anahtarı yalnız Credentials.apiKey'den, model yalnız Settings.model'den okunur. Maskeli/eksik/yanlış tipli alanlar reddedilir. OpenAiFirmSettings JSON serileştirmesi ApiKey alanını atlar ve ToString gizli bilgi içermez. Mevcut EF Data Protection converter değişmedi.
- GetAsync/DB anahtar okuması canlıda denenmedi. Predicate ve tekillik/firmaya erişim testleri ile PostgreSQL ToQueryString çeviri testi eklendi; çeviri testi DB bağlantısı açmaz. Controller'a bağlanmadığından panel hâlâ doğal dil çağrısı yapmaz. Gerçek anahtar kullanıcıdan alınmadı; entegrasyon kataloğu hedef DB'ye yazılmadı.
