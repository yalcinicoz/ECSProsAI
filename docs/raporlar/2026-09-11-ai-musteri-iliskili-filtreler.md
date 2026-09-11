# AI müşteri raporu — ilişkili faaliyet filtresi

## Tamamlanan yerel adım

Müşteri kaynağı `customers.orders` ilişkisiyle ortak EXISTS/NOT EXISTS motoruna bağlandı. Sipariş durum/tarih/tutar koşulları ve aynı siparişin ürün/ödeme/iade ilişkileri birleştirilebilir. Hazır soru veya yeni rapor endpoint'i eklenmedi.

- Dönem ilişkili siparişin açılış tarihine uygulanır; müşteri açılışını sınırlamaz.
- Kanal ve alan yetkileri her çalıştırmada sunucudan gelir; yetkisiz katalog ilişkisi kurulmaz.
- İlişkiler müşterileri seçer; var olan orderCount/returnCount ölçülerinin anlamını değiştirmez. Bu ölçüler dönemin tüm yetkili kayıtlarını sayar. Eşleşen işlem sayısı gibi sunulamaz.
- İlişkili alanlar müşteri detay kolonu/gruplaması olarak açılmaz. Çoklu kayıtlar müşteri sayısını çoğaltmaz.
- Ödeme yöntemi UUID'si ve ödeme/iade durum anlamı uydurulmaz. "Kredi kartıyla gerçekten ödeme yapan" isteğinin otomatik sözlük çözümü henüz tamam değildir.

## Dosyalar

- `CustomerReportRelations.cs` yeni ilişki adaptörü.
- `CustomerReportSource.cs`, `OrderReportPredicates.cs`: mevcut güvenli sorgu şeması tekrar kullanıldı.
- `DynamicReportMetadata.cs`, `ReportSourceCatalog.cs`, `OpenAiDynamicReportContract.cs`: yetkili ilişki alanları ve anlamları.
- `admin/src/pages/reports/AiReportsPage.tsx`: seçim filtresi ile toplamların farkı açıklandı.
- `CustomerReportTests.cs`, `Acceptance/AiStockReadAcceptanceTests.cs`, `admin/tests/ai-reports.test.cjs`: regresyon ve bağımsız sayım kontrolleri.

## Doğrulama

- Hedefli API testleri: 36 geçti.
- `dotnet test ... --filter "TestCategory!=Acceptance"`: 525 geçti, 12 opt-in ortam testi atlandı.
- Admin ilgili testler: 18 geçti; TypeScript noEmit ve dosyaya yönelik ESLint geçti.
- API01 SSH tüneli üzerinden .241 salt-okunur gerçek veri kabulü: 1 geçti (29 saniye). İlişkili müşteri sayımı bağımsız SQL ile eşleşti; boş kanal kapsamı ve NOT EXISTS tamamlayıcılığı doğrulandı. Tünel kapatıldı, ortam değişkeni geri alındı.
- İlk sandbox SSH denemesi başlamadı; izinli araç çalıştırmasıyla kabul tamamlandı.
- Gerçek model konuşması ve yeni ekranın tarayıcı kabulü henüz yapılmadı. Test başarısı doğal dilin her isteği doğru yorumlayacağı garantisi değildir.

## Yayın ve kalanlar

Yayın, migration, seed, iş verisi yazımı veya GitHub push yok. Kullanıcının tüm kalanları bitirip birlikte yayınlama kararı korunuyor. Personel performansının esas alınacak ölçütü henüz net değil; maliyet/hareketsiz kart, büyük asenkron aktarım/PDF ve diğer açık kabuller tamamlandı sayılmıyor.

OpenAI Docs ile mevcut strict JSON şeması yaklaşımı korundu; model/anahtar değişmedi: https://developers.openai.com/api/docs/guides/structured-outputs
