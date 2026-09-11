# Personel firmasından bağımsız AI hesabı

- `AiReporting:AccountFirmId` yöneticinin açık ortak hesap seçimidir. Tanımlandığında hesap listesi yalnız bu firmayı döndürür; kullanıcının FirmId alanı hesap seçiminde kullanılmaz. Kullanıcının aktif olması, rapor ve veri izinleri hâlâ zorunludur.
- Farklı hesap ID'si gönderilirse superadmin dahil reddedilir. Geçersiz ayarda veya eksik/pasif/çift entegrasyonda başka hesaba otomatik geçiş yapılmaz. Anahtar/model mevcut firma entegrasyonundan okunmaya devam eder; ücret kotası bu firmaya yazılır.
- Ayar yoksa mevcut firmaya bağlı davranış korunur; böylece diğer kurulumların ücretlendirme kapsamı kendiliğinden genişlemez.
- Admin tek yetkili hesabı otomatik seçer. Birden fazla hesap varsa ilkini tahmin etmez. Ekran metni AI hizmet hesabıyla veri yetkisini ayırır.
- Paylaşımın aynı-firma güvenlik kuralı bu değişiklik kapsamında kaldırılmadı. Ortak AI hesabı, kullanıcılar arasında rapor paylaşma yetkisi vermez.

## Yayın sırasında gerekli adım

API01/API02 multi-test ayarına `AiReporting:AccountFirmId = 1235fa20-920c-466f-ad67-c5d5c6b6a86f` eklenerek MİŞAROĞLU ortak hesap seçilmeli. Ardından güncel API ve kullanıcı tarafından build edilen admin yayınlanmalı. Kaynak .59/Nginx ayarları değişmemeli.

Bu turda uzak ayar veya kullanıcı firma kaydı değiştirilmedi, yayın yapılmadı. Test1'in önceki firma bağlantısı korunuyor; ortak hesap etkinleşip doğrulandıktan sonra bu test amaçlı bağlantı ayrıca geri alınabilir.

## Dosyalar ve kontroller

- Değişenler: `OpenAiFirmSettingsProvider.cs`, `AiReportsPage.tsx`, `OpenAiFirmSettingsTests.cs`, `admin/tests/ai-reports.test.cjs` ve bu not.
- Hedefli API testleri: 10 geçti, 0 başarısız. Mevcut proje derleyici uyarıları devam ediyor.
- Admin: 20 test geçti; `npx tsc --noEmit` başarılı. Üretim build çalıştırılmadı.
- Ortak hesabın uzakta etkinleştirilmesi ve firmasız kullanıcıyla gerçek ekran kabulü yayın sonrasında yapılmalı; mevcut sonuçlar bunu doğrulamış sayılmaz.
