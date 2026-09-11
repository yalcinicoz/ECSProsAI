# Personel işlem raporu — yerel uygulama

## Kapsam

Kullanıcı operasyon (toplama, paketleme, fatura) ve personelin telesatış/sosyal medya satışlarının ayrı değerlendirilmesini istedi. Bu adım yalnız doğrulanmış operasyon/fatura kayıtlarını dinamik motora bağlar. Satış personeli ataması henüz bulunmadı; sipariş CreatedBy/ConfirmedBy alanı satış yapan kişi olarak kabul edilmedi.

- Kaynak: `staffActivities`. Bir satır bir işlem kaydıdır.
- `fulfillment.ful_operation_logs`: `line_picked` ve `package_packed`; personel `ActorId`, zaman `CreatedAt`.
- `order.ord_invoices`: legacy olmayan, internal numaralı, CreatedBy dolu kayıtlar. Paket kapanışında otomatik oluşturulan faturalar DAHİLDİR. Manuel/otomatik ayrımı mevcut veriden güvenle çıkarılamaz; panel ve model bunu açıkça belirtir.
- Dış/legacy fatura kayıtları, bunları içeri aktaran kullanıcıya fatura kesme performansı olarak yazılmaz.
- İsimler `iam.iam_users` güncel ad/soyad alanlarıdır; silinmiş/eksik kullanıcıda isim null, kimlik korunur.
- Kayıt sayısı ürün adedi, mesai/işlem süresi, ciro veya performans puanı değildir. Bunlar hesaplanmış gibi gösterilmez.
- Genel kullanıcı görüntüleme + rapor yetkisi gerekir; operasyon/fatura dalları kendi ayrı yetkisine göre sorgulanır. Rapor kanalları ve ilgili domain kanalları kesişir. Yetkisiz dalın tablosu sorgulanmaz. Sonuç yalnız erişilebilen faaliyetleri kapsar.

## Değişen dosyalar

- Yeni `StaffActivitySource.cs`, `StaffActivityExecutor.cs`.
- `DynamicReportPlan.cs`, `DynamicReportMetadata.cs`, `ReportSourceCatalog.cs`, `OrderReportExecutor.cs`, `OpenAiDynamicReportContract.cs` kaynak entegrasyonu.
- `AiReportsPage.tsx`, `savedReportValidation.ts`: kaynak seçimi, kayıtlı tarif, kapsam açıklaması; V1 stok alanlarına personel alanları karışmaz.
- `StaffActivityTests.cs`, `AiStockReadAcceptanceTests.cs`, `ai-reports.test.cjs`: yetki, SQL, metadata ve ekran regresyonları.

## Testler

- Hedefli API: 10 geçti. Genel API: 528 geçti, 12 opt-in ortam testi atlandı.
- Admin: 19 geçti; TypeScript noEmit ve scoped ESLint başarılı. İlk admin testi sabit eski kaynak listesi nedeniyle başarısız oldu; yeni kaynakla genişletilip tekrar geçti.
- API01 SSH tüneliyle .241 READ ONLY kabul: 1 geçti (27s). Bağımsız SQL olay/fatura sayımı, grupların toplamı ve boş kanal kapsamı eşleşti. Tünel kapandı. Veri oluşturulmadı; bu kontrol gerçek modelin anlamsal doğruluğu veya dolu tarihsel personel verisi garantisi değildir.
- `git diff --check` başarılı. Üretim build/yayın, migration/seed, iş verisi yazımı, GitHub push ve .59 işlemi yok.

## Açık noktalar

Yeni kaynak için gerçek model/tarayıcı kabulü ve birleşik yayın yapılmadı. Kullanıcı telesatış/sosyal medya satış ekranının henüz yapılmadığını doğruladı; satış-personel rapor bağlantısı o ekran geliştirilene kadar bağımlı açık iş olarak bırakıldı. Aynı bilgi tekrar sorulmayacak. Operasyon sürelerinin iş anlamı, manuel/otomatik fatura ayrımı, diğer önceki raporlama fazları açık.

OpenAI Docs ile strict JSON sözleşmesi korundu; model veya anahtar değiştirilmedi: https://developers.openai.com/api/docs/guides/structured-outputs
