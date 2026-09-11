# AI ürün kartı ve hareket varlığı — 2026-09-11

## Uygulanan

- `ProductCardReportSource.cs` ve `ProductCardReportExecutor.cs`: ortak dinamik filtre, detay, gruplama, tablo ve Excel motoruna `productCards` kaynağı eklendi. Hazır soruya özel rapor değildir.
- Başlangıç kümesi silinmemiş ürün kartlarıdır; stok satırı/varyantı bulunmayan kartlar kaybolmaz. `cards.movements` ilişkisi EXISTS/NOT EXISTS kullanır; hareketler ürün satırlarını çoğaltmaz.
- Hareket dönem sınırı zorunludur. Kart açılış tarihi ayrı filtredir. Silinmiş/pasif varyantların kalan hareket geçmişi göz ardı edilmez.
- Genel rapor, katalog ve stok görüntüleme yetkileri birlikte gerekir; kanal sınırlı yetki genel katalog erişimine dönüştürülmez.
- Dinamik plan/katalog/metadata/dispatch/AI sözleşmesi ve admin kaynak seçimi, kapsam açıklaması, kayıtlı tarif doğrulaması güncellendi. Yeni tablo, seed veya migration yok.

## Kontroller

- Hedefli API: 7 geçti. Acceptance dışı genel API: 531 geçti, 7 atlandı, 0 hata.
- İlk filtresiz genel çalıştırma: 531 geçti, 24 atlandı, 21 başarısız. Ayrıntılı tekrar ERP SQL Server bağlantısında erişim hatası gösterdi; bağlantı tekrarları durduruldu. Tüm dış ortam testleri başarılı diye raporlanmaz.
- API01 SSH tüneli üzerinden .241 salt okunur kabul: 1 geçti (27 saniye). Hareketli/hareketsiz kart toplamı ve bağımsız SQL anti-join sonucu eşleşti. Tünel kapatıldı, veri yazılmadı.
- Admin: 20 test geçti; TypeScript noEmit ve ilgili ESLint başarılı. Üretim build yapılmadı.

## Açık sınırlar / sonraki adım

- Her kartın açılışından sonraki ilk N ayda hareket görmeme, ortak son N aylık dönemle aynı değildir. Aşağıdaki devam çalışmasıyla ayrı parametre üzerinden desteklendi.
- Eksik/aktarılmamış geçmiş için tüm zamanlarda hareket yokluğu iddia edilmez.
- Gerçek model ve tarayıcı kabulü, ardından birleşik yayın yapılacak. Henüz yayın, GitHub push, .59 veya fiyat işlemi yok. Personel satış ekranına bağlı raporlama kullanıcı kararıyla açık kaldı.

## Devam — kart açılışına göre ilk N ay

- `DynamicReportPlan.CardWindowMonths` yalnız productCards için isteğe bağlı 1..12 ay. Eski tariflerde null olduğundan önceki hareket dönemi değişmez.
- Bu modda from/to kart açılış dönemidir (en fazla366 gün); her kartın kendi açılışından itibaren N takvim ayı incelenir. Türkiye saat dilimi, başlangıç dahil/bitiş hariç ve ay sonu yuvarlaması kullanılır. Henüz N ayını doldurmayan kartlar sorgunun zorunlu kapsamından çıkarılır.
- Tüm değerler SQL parametresi; yetki, süre, satır ve salt okunur işlem sınırları korundu. Açılış dönemi belirtilmediyse AI sormalı, son N ayı onun yerine kullanmamalı.
- Plan/source/AI sözleşmesi, admin önizleme/kayıtlı tarif doğrulaması, ProductCardReportTests ve salt-okunur kabul genişletildi. OpenAI Docs doğrultusunda strict JSON şeması korundu: https://developers.openai.com/api/docs/guides/structured-outputs
- Acceptance dışı API533 geçti/7 atlandı; hedefli5 geçti. Admin24 geçti. İlk JSX kontrolü kapanış parantezi hatası verdi; düzeltildi, TypeScript noEmit ve ilgili ESLint tekrar başarılı.
- API01 tüneliyle .241 READ ONLY kabul1/1 (27s): bağımsız ilk6ay anti-join sayımı, ay sonu ve bitiş sınırı denetlendi; tünel kapandı. Kayıt eklenmedi/silinmedi. Bu, gerçek AI cevabının veya tüm ekran akışının kabulü değildir.
- Gerçek model/tarayıcı kabulü ve birleşik yayın hâlâ açık. 12 aydan uzun pencere, bütün tarihçe ve aktarılmamış geçmiş için destek iddiası yok.

## Yayın — 2026-09-11

- Kullanıcının admin build'i `index-D6-bgTgK.js` içinde personel, ürün kartı ve cardWindowMonths alanlarını içerdiği doğrulandı. API Release publish başarılı (mevcut derleyici uyarıları var).
- `tools/publish-ai-dynamic-20260911.ps1 -CardWindow` ile `20260911_ai_card_window` API01/API02 ve .56 multi-test admin olarak etkinleştirildi. API ayarları korundu; migration başlangıç kapısı false doğrulandı. İki düğüm aktif ve /ready Healthy (PostgreSQL/Redis/DataProtection). Yeniden başlatma sırasında ilk bağlantı denemeleri reddedildi, tekrar denemelerinde sağlık kontrolü geçti.
- Dış HTTP admin ve yeni JS200; doğru bundle ve yeni alanlar doğrulandı. İlk sandbox HTTP isteği socket izni nedeniyle reddedildi; izinli salt-okunur tekrarı geçti.
- Bu yayının yerel/uzak tar.gz dosyaları temizlendi. Önceki çalışan release'ler korundu. .59/Nginx ayarı/fiyat değişikliği yapılmadı. Fiyat geri alımına ait yedi dosyada içerik diff'i olmadığı paketlemeden önce doğrulandı.
- Gerçek OpenAI konuşması, yeni kaynakların tarayıcı etkileşim kabulü ve kalan genel raporlama işleri kapanmış sayılmaz.

## Gerçek AI ekran kontrolü — 2026-09-11

- Kullanıcı onayıyla yalnız multi-test API01/API02 kota değerleri geçici 100 kullanıcı/200 firma (24 saat) yapıldı; dakika limiti 3 korundu. Denemeler sonunda eski 20/50 değerleri geri kondu, her iki /ready Healthy doğrulandı. Redis sayaçları sıfırlanmadı.
- Ocak 2025 açılış dönemi ve tamamlanmış ilk 6 ay kapsamlı, hareket koşulu olmayan ürün kartı taslağı gerçek AI ile üretildi. Tarihler/kolonlar/pencere ekranda doğru göründü; rapor 185 kayıt döndürdü. Ürün kodu araması 1 kayıt, temizlenmesi yeniden 185 kayıt verdi.
- Aynı kapsamda ilk 6 ayda kayıtlı hareketi olmayan kartlar isteği hem ilk istekte hem konuşmaya koşul eklenince `AI isteği veya planı doğrulanamadı; rapor çalıştırılmadı.` hatası verdi. Bu kabul BAŞARISIZ ve açık; kök neden henüz kanıtlanmadı, bir kod düzeltmesi yapılmadı. Koşulsuz rapor bunun yerine başarılı sayılmaz.
- İş verileri, .59 ve Nginx değiştirilmedi. Personel ekran kabulü bu turda yapılmadı.

## Koşul ve tarih şeması düzeltmesi — 2026-09-11

- Önceki başarısız kabul araştırıldı. Güvenli sunucu tanılaması `ReportPredicateSchema.Build` reddini, sonraki denemede ilişkili hareket koşulunda `ParseValue` tarih dönüşüm reddini gösterdi. Ham AI yanıtı/istek/değerler kaydedilmedi; ilk hatalı nesnenin tam içeriği bilinmiyor.
- `OpenAiDynamicReportContract.cs`: karşılaştırma/grup/ilişki için ayrı anyOf şekilleri; kullanılmayan alanlar zorunlu null. Tarih karşılaştırmalarının değerleri date-time formatında. Yetkiler ve yürütücü doğrulamaları gevşetilmedi. Pencerenin tek başına hareket yokluğu anlamına gelmediği açıklaması eklendi. OpenAI Docs doğrulaması: https://developers.openai.com/api/docs/guides/structured-outputs (nested anyOf ve recursive schema desteği).
- `AiReportsController.cs`: yalnız sabit hata kodu ve kod yığını tanılaması; prompt/plan/literal/anahtar kaydı yok, logger isteğe bağlı. `ProductCardReportTests.cs` regresyonu eklendi; `MovementReportTests.cs` ve `ReportBusinessDictionaryTests.cs` sözlük testleri yeni şema şekline uyarlandı. `tools/publish-ai-response-diagnostics-20260911.ps1` sürüm/hash kontrollü yayın seçenekleri güncellendi.
- Test sürecinde logger olmayan test düzeneği ve eski şema yolunu kullanan testler düzeltildi. Bir paralel MSBuild çalışması MSB4166/MSB4242 ile kapandı; tek işçili tekrar başarılı. Son `dotnet test ... --no-restore -m:1 -nr:false --filter "TestCategory!=Acceptance"`: 534 geçti, 12 ortam testi atlandı, 0 başarısız. Release publish başarılı; mevcut derleyici uyarıları devam ediyor.
- Son sürüm `20260911_ai_predicate_dates`, API01/API02 üzerinde etkin; her ikisi Healthy. Admin, Nginx, .59, fiyat ve iş verilerine yazım yapılmadı.
- Gerçek AI kabulü: aynı Ocak2025/ilk6ay isteği; doğru notExists hareket koşulu ve `2025-01-01T00:00:00+03:00` tarih değeri ekranda doğrulandı. Rapor 185 sonuç; P-00014713 araması 1 sonuç. Ara denemede hareket koşulunu atlayan bir taslak görüldü ve çalıştırılmadı; bu nedenle bütün olası doğal dil istekleri için eksiksizlik garantisi verilmez. Bu senaryonun son kabulü geçti; personel ekranı ve genel kalan işler bununla kapanmaz.
- Geçici 100/200 kota test sonunda iki düğümde 20/50'ye döndürüldü, dakika3 korundu; sayaçlar silinmedi. Son sağlık kontrolleri başarılı.
- Temizlik: yüklenen /tmp DLL'ler yayın betiği tarafından kaldırıldı. Dört ara release dizinini silme isteği güvenlik denetimince reddedildi; hiçbir release silinmedi. Silme için kullanıcı onayı bekleniyor: `20260911_ai_predicate_diagnostics`, `20260911_ai_predicate_shape`, `20260911_ai_predicate_reason`, `20260911_ai_window_absence`. Görev öncesi ve son çalışan sürüm korunmalı. Ara release'lerde test kotası 100/200 kopyaları olduğundan bilinçsiz rollback yapılmamalı.

### Ara sürüm temizliği tamamlandı

### Ek ekran kabulü ve ikinci kullanıcı — 2026-09-11

- `Kabul testi — ilk 6 ay hareketsiz kartlar` adlı test tarifi kaydedildi; sayfa yenilenip açılınca dönem, cardWindowMonths, notExists koşulu ve kolonlar korundu. Bu test tarifi paylaşım kontrolü için henüz kaldırılmadı.
- Personel kimliği/işlem türü bazında Ağustos raporu ve konuşmayla Eylül dönemine geçiş doğru taslak üretti; ikisi de hatasız 0 sonuç verdi. Dolu personel verisi doğrulaması yapılmış sayılmaz.
- İlk 20 müşteri isteği gönderildi, ancak yönetici oturumu değiştiği için sonucu doğrulanamadı. Excel ve iki kullanıcı arasında paylaşım kabulü açık.
- Kullanıcının test1 olarak belirttiği oturumda yetki grubu bulunmadığı ekran mesajıyla görüldü. AI rapor URL'sini doğrudan açmak da erişim vermedi. Bu yalnız ekran erişim engelini doğrular; API ve paylaşım yetki kabulünün yerine geçmez. Hesap yetkileri değiştirilmedi.
- Bu ek testte geçici yükseltilen kota iki API'de yeniden dakika3/kullanıcı20/firma50 değerlerine döndürüldü. Redis sayaçları temizlenmedi. İki /ready kontrolü başarılı; production değişmedi.

- Kullanıcının ayrıca verdiği açık silme onayıyla yukarıdaki dört ara release API01 ve API02'den kalıcı olarak kaldırıldı (toplam 8 dizin). Öncesinde current bağlantısı ve çalışan servis process cwd'sinin `20260911_ai_predicate_dates` olduğu doğrulandı; hedefler symlink değil ve release kökü altında kontrol edildi.
- Son çalışan `20260911_ai_predicate_dates` ve görev öncesi `20260911_ai_card_window` korundu. Silme sonrası iki /ready kontrolü Healthy. Servis yeniden başlatılmadı, ayarlar ve production değiştirilmedi. Sunucudaki silinen ara kopyalar geri dönüşüm kutusunda değildir; yerel publish çıktıları bu işlemde silinmedi.
