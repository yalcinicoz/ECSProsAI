# test1 AI ekran kabulü — 2026-09-11

## Chrome Excel dosyası kontrolü

- Kullanıcının Chrome'dan indirdiği `C:/Users/garku/Downloads/ai-rapor.xlsx` salt okunur ZIP/XML incelemesiyle açıldı; dosya değiştirilmedi.
- `Rapor!A2:D2`: P-00022295, 13, 0, 13. Önceki ekran sonucuyla eşleşiyor. Ürün kodu metin, üç miktar sayısal hücre; aralıkta formül yok. `A1:D2` otomatik filtre ve başlık satırı sabitlemesi mevcut.
- **Görsel/içerik eksiği:** başlıklar Türkçe değil: productCode, stock.quantity, stock.reserved, stock.available. Veri aktarımı geçti; kullanıcı dostu başlık kabulü açık.
- Chrome'da dosya indirmesi ve içerik aktarımı doğrulandı. Gömülü tarayıcıdaki başarısız indirme tüm tarayıcılara genellenemez; oradaki kesin kök neden kanıtlanmadı. Excel uygulamasında açılış/render kontrolü bu incelemeye dahil değildir.

Yayındaki multi-test ekranında, mevcut test1 oturumu ve değiştirilmemiş izinlerle kontrol edildi. Ortak AI hesabı için yayınlanmamış yerel değişiklikler bu teste dahil değildir. Firma, kullanıcı, kota ve production ayarları değiştirilmedi.

## Geçen senaryolar

- Görünen kaynaklar yalnız güncel stok ve stok hareketleri; sipariş/müşteri/personel kaynakları görünmüyor.
- Gerçek AI isteği: fiziksel stok genel toplamı; stok/rezerve/kullanılabilir ölçüleri, ürün/depo kırılımı olmadan. Taslak physical koşulunu ve üç ölçüyü korudu; tek stok türüne göre bir satır döndü.
- İlk sonuç: stok 254938, rezerve 1208, kullanılabilir 253730 (ekran hesaplama 13:28:42). Aritmetik tutarlı; bu turda bağımsız DB sayımı yapılmadı.
- Sütun filtresi stok miktarı > 999999999: 0 kayıt. Filtre temizlenince 1 kayıt geri geldi; yeniden hesaplamada 254999/1208/253791 (13:29:11). Canlı veri değişebildiğinden sabit snapshot sonucu iddia edilmez. Arada yeni AI isteği gönderilmedi; kodda grid isteği ayrı uç kullanıyor.
- Yetkisiz alan isteği: “Bu ay en fazla sipariş veren 20 müşterinin adını ve sipariş toplamını listele.” Taslak verilmedi, raporu çalıştır düğmesi kapandı, veri dönmedi. Ekran mesajı genel “desteklenmiyor”; bunu tüm API yetki testlerinin yerine geçen bir kanıt olarak değerlendirmiyoruz.
- AI rapor sonuçlarında export düğmesi yok; test1'e export/paylaşım oluşturma izni verilmedi.

## Açık

- İkinci kullanıcıyla paylaşım: paylaşma yetkili yönetici oturumu gerekli. Test1 izinleri genişletilmedi, sahte paylaşım tokenı oluşturulmadı.
- Excel indirme kabulü: dışa aktarma yetkili oturum gerekli.
- Önceki yönetici hesabındaki `Kabul testi — ilk 6 ay hareketsiz kartlar` tarifinin kontrollü temizliği o oturumda yapılmalı. Bu turda yeni tarif/veri eklenmedi.
- Kaynak dışı isteğin mesajı yetki eksikliği ile motor desteği eksikliğini ayırmıyor; açıklık iyileştirmesi ayrıca değerlendirilebilir.

## Yönetici oturumu devamı

- Gokce yönetici girişi tarayıcıda doğrulandı. Mevcut test tarifi için paylaşım oluşturma denemesi “İşlem başarısız oldu.” mesajı verdi.
- Salt okunur DB kontrolü: Gokce FirmId boş, test1 MİŞAROĞLU'na bağlı. `SavedAiReportsController.Sharing` mevcut politikada sahibin firma bağlantısı yoksa Forbid döndürüyor. Bu koşul paylaşımı engelliyor; firma ataması veya kural değişikliği yapılmadı.
- İki sekme aynı tarayıcı profiline ait; eski test1 ekranının açık görünmesi tek başına ayrı kimlik doğrulama oturumu kanıtı değildir.
- Paylaşım pozitif kabulü ve Excel indirme/içerik kontrolü henüz tamamlanmadı. Yeni paylaşım tokenı başarıyla oluşturulmadı; mevcut test tarifi korunuyor.

## İkinci kullanıcı paylaşım kabulü — 11 Eylül, 17:04 Türkiye saati

- Yönetici hesabında oluşturulan `Kabul testi — stok paylaşımı` tarifi, kullanıcı test1 ile giriş yaptıktan sonra bağlantı alanından başarıyla açıldı. Fiziksel stok ve `P-00022295` koşulu korundu.
- Rapor test1 oturumunda çalıştı: bir sonuç, stok 13, rezerve 0, kullanılabilir 13. Böylece iki farklı kullanıcıyla olumlu paylaşım akışı doğrulandı; tüm kanal kapsamlarının izolasyonunu tek başına kanıtlamaz.
- Alıcıda yalnız güncel stok ve stok hareketleri kaynakları görünüyor. Excel indirme ve paylaşım oluşturma düğmeleri yok. İzinler değiştirilmedi; test1 hesabına tarif kopyası kaydedilmedi.
- Görsel kusur: paylaşılan raporda ürün kodu kolon başlığı ve kapsam etiketi `productCode` olarak görünüyor. Veri doğru; Türkçe başlık çözümlemesi ayrıca incelenmeli.
- **Açık:** yeni `Kabul testi — stok paylaşımı` kaydının ve bağlantısının yönetici oturumunda temizlenmesi; gerçek indirilen Excel dosyasının içeriğinin kontrolü. Önceki bölümdeki temizlenen tarif farklıdır. Paylaşım tokenı bu nota yazılmadı.
- **17:05 sonrası güncelleme:** kullanıcı yönetici hesabına döndü. Paylaşım kapatıldı, `Kabul testi — stok paylaşımı` onay ekranından kaldırıldı ve “Henüz kayıtlı rapor yok.” doğrulandı. Kaynak veri silinmedi. Excel içerik kabulü için aynı ürünle rapor yeniden hazırlandı (13/0/13); tarayıcı indirmesi erişilebilir dosya olarak alınamadığından kullanıcıdan XLSX dosyasını iletmesi gerekiyor.

## Yayın sonrası yönetici kontrolü — 11 Eylül, 16:53 sonrası Türkiye saati

Bu bölüm yukarıdaki eski açık maddelerin güncel durumudur. Kullanıcı ve grup izinleri değiştirilmedi.

- İadeler ekranında 13 kayıt, Türkçe tip/durum/geri ödeme etiketleri ve Ödenecek/Kapanan sekmeleri doğrulandı. Üç geri ödemesiz kapanan kaydın tutarı 0 görünüyor.
- AI ekranında manuel fiziksel stok raporu `P-00022295` ile sınırlandı: bir satır, stok 13, rezerve 0, kullanılabilir 13. Bu ekran kontrolüdür; bağımsız DB mutabakatı değildir.
- Stok miktarı sütun filtresi `= 999999` yapıldığında sıfır satır ve sıfır toplam geldi. Filtre kaldırılınca bir satır ve 13/0/13 geri geldi. Grafik de aynı ürün için 13 gösterdi.
- Yönetici mevcut kabul testi tarifinden paylaşım bağlantısı oluşturabildi; önceki firma engeli bu oturumda tekrarlanmadı. Bağlantı kapatıldı. Kapatılmış bağlantıyı açma denemesi reddedildi, taslak yüklenmedi. Mesaj genel “İşlem başarısız oldu.”; hata nedenini tek başına kanıtlamaz.
- `Kabul testi — ilk 6 ay hareketsiz kartlar` kişisel test tarifi onay ekranından kaldırıldı; “Henüz kayıtlı rapor yok.” görüldü. Kaynak veriler silinmedi. Test paylaşımı açık bırakılmadı.
- Excel düğmesi hazırlama durumundan normal duruma hatasız döndü. Ancak indirilen dosya bu oturumda erişilebilir yerel dosya olarak bulunamadı; gerçek indirmenin dosya içeriği kabulü **açık**. Düğme davranışı dosya doğrulaması sayılmaz.
- İkinci sekme giriş ekranında; bağımsız test1 oturumu yok. İki kullanıcı arasında olumlu paylaşım kabulü **açık**. Yönetici oturumunu kapatma, parola sıfırlama veya yetki genişletme yapılmadı.
- Kontrol sonunda tarayıcı İadeler/Tümü sayfasına geri bırakıldı. Bu turda yayın, migration veya production değişikliği yapılmadı.
- Güncel yerel kaynakta `dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ReportShareFlowTests|FullyQualifiedName~ReportSharingPolicyTests|FullyQualifiedName~ReportExcelExportTests|FullyQualifiedName~SavedAiReportsTests" --verbosity minimal`: **14 geçti, 0 başarısız, 0 atlandı**. Derleyici uyarıları mevcut. Excel testleri gerçek geçici XLSX üretimini, sayısal hücreleri, formül metni güvenliğini ve geçici dosyanın temizliğini kapsıyor; tarayıcıdan indirilen dosyanın kabulünün yerine geçmez. Yerel kaynak son GitHub güncellemelerini içerir; yayındaki binary ile aynı sürüm olduğu iddia edilmez.
