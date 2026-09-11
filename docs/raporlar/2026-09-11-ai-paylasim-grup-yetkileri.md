# AI rapor paylaşımı — etkin grup yetkileri

Kullanıcının açık talebiyle paylaşımda `FirmId` eşitliği şartı kaldırıldı. Aynı gruba üyelik zorunluluğu eklenmedi: mevcut IAM servisinin hesapladığı grup izinleri, kullanıcıya özel izin/kısıtlamalar ve kanal kapsamı geçerlidir.

## Değişiklik

- `ReportShareDirectory.cs`: paylaşım kimlik sorgusu artık FirmId okumuyor; silinmiş kullanıcıları dışlıyor.
- `ReportSharingPolicy.cs`, `SavedAiReportsController.cs`: paylaşan/alıcı aktif olmalı; paylaşan `reports.ai.share` ve rapor/veri yetkilerini taşımalı. Alıcının rapor tarifi alanları kendi etkin izinleriyle yeniden doğrulanır. Başka kullanıcının veri kapsamı taşınmaz.
- `SavedReports.tsx`, `PermissionKatalogu.cs`: firma şartını anlatan metinler grup/kullanıcı yetkileriyle uyumlu hale getirildi. Katalogdaki açıklama güncellemesi mevcut DB açıklamasını kendiliğinden ezmez.
- `ReportShareFlowTests.cs`, `ReportSharingPolicyTests.cs`: firmasız paylaşım oluşturma, grup izni, kullanıcıya özel iptal, izin kaybından sonra bağlantı kapatma, pasif kullanıcı, alıcı yetki kaybı ve token kontrolleri.

## Sınırlar

Yalnız tarif paylaşılır; sonuç satırları paylaşılmaz. Mevcut geçerli bağlantılar bu yeni kuralla, firma farkı yerine etkin yetkiler üzerinden değerlendirilecektir. Bağlantıyı alan kişi tarifteki filtre/kolon bilgilerini görebilir, fakat veri erişimi kazanmaz. Kullanıcı/grup atamaları, firma kayıtları, OpenAI ücret hesabı ve production değiştirilmedi. Yeni migration yok. Önceden hazırlanmış ortak AI hesabı değişikliği ayrı bir iştir; bu çalışma onu etkinleştirmez.

Henüz yayın yapılmadı. Admin üretim build komutu çalıştırılmadı. Yayın sonrası iki gerçek kullanıcıyla olumlu/olumsuz paylaşım ve Excel kabulü açık.

## Kontroller

- Hedefli API paylaşım/kayıt testleri: 10 geçti, 0 başarısız. Mevcut derleyici uyarıları devam ediyor.
- Admin rapor testleri: 20 geçti. TypeScript `--noEmit` ve SavedReports ESLint başarılı.
- Üretim veya multi-test DB'ye yazım yapılmadı; bu doğrulamalar gerçek yayımlanmış ekran kabulünün yerine geçmez.
