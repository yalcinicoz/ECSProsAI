# Yayın öncesi ortam kabulü

- GitHub kaynak sürümü: 6ae426a8. API Release publish `output/publish/ai-final-6ae426a8` başarılı; mevcut uyarılar var. Henüz sunucuya aktarılmadı.
- Admin dist, SavedReports ve FilterBar kaynaklarından eski. Kullanıcıdan yeniden build istendi; agent build komutunu çalıştırmadı.
- API01 üzerinden salt okunur tünelde stok toplamı kabulü: 1/1 geçti.
- Genişletilmiş salt okunur DB testleri: 8 geçti, 1 başarısız, 0 atlandı. Hata: eksik InstallmentCount / RefundAccountHolder alanları ve catalog.mv_product_stats görünümü. Ayrıca genel şema testi barcode/productCode ilişkili filtrelerini doğrudan şemaya uyguluyordu; test gerçek OrderGrid.ApplyAll yoluna yönlendirildi, değişiklik henüz yeniden doğrulanmadı.
- Redis1 üzerinde localhost:16379 portunda kalıcılığı kapalı, mevcut servisten bağımsız geçici Redis ile 4/4 kota testi geçti; süreç shutdown nosave ile kapatıldı, SSH tüneli kapatıldı. Mevcut Redis verilerine dokunulmadı.
- API01 disk: %24 kullanım, 55G boş; servis aktif. Redis1 disk: %26 kullanım, 34G boş.
- Yeni migration'lar uygulanmadı. AddReturnFlowFields mevcut return/refund tiplerini customer olarak dönüştürüyor; bu dönüşüm için kullanıcıya karar sorulacak. .59'a hiçbir işlem yapılmadı.
- Kalan: migration kararı/uygulaması, 3 transaction rollback DB testi, yeniden şema kabulü, admin build/yayın, iki kullanıcı paylaşım ve gerçek Excel indirme kabulü. Personel satış ekranı kapsam dışı/açık kalıyor.

## Kullanıcı onayı sonrası migration uygulaması

- API01 SSH tüneli üzerinden yalnız .241/ecommerce_db güncellendi. Order (3), Inventory (1), Catalog (1) olmak üzere 5 migration uygulandı; bu üç bağlamda pending=0 doğrulandı.
- Taksit alanları, iade akış/banka alanları, raf sayım tabloları ve ürün istatistik görünümü açıldı. AddReturnFlowFields içindeki return/refund -> customer veri dönüşümü onay kapsamında yürütüldü.
- Core değiştirilmedi: daha önce özellikle ertelenmiş 20260906120857_DropFirmPlatformInvoiceSeriesId bulundu. PROGRESS.md eski worker bağımlılığı kontrolünü şart koşuyor. İki yeni iade nedeni seed'i bu migration'ın arkasında; kolonu koruyarak ayrı uygulama için kullanıcı kararı istendi. Otomatik geniş seed çalıştırılmadı.
- SchemaUpdate aracı yalnız açık izin listesindeki migration'ları yürütür; bilinmeyen migration içeren bağlamı atlar. Bağlantı bilgileri ortama aktarılır, dosyaya yazılmaz. Kilit bekleme 5 saniye, sorgu süresi 120 saniye sınırlı.
- API/admin yayını henüz yok. Güncel admin build hâlâ gerekli. Migration sonrası salt okunur kabul yeniden başlatıldı.
- Son doğrulama: 9/9 salt okunur DB testi, 3/3 rollback DB testi, 4/4 izole Redis testi geçti; bu seçili koşularda atlanan yok. Önceki 12 atlanan test ve bağlantı yüzünden başarısız not testi böylece çalıştırıldı. İki API düğümü ready=Healthy.
- Bildirim kabul testi artık ihtiyaç duyduğu üç şablonu kendi rollback transaction'ında kuruyor; kalıcı seed/panel durumuna bağımlılık kaldırıldı. Üretim bildirim davranışı değişmedi. GridSchemasDbTests gerçek OrderGrid yolunu kullanıyor ve barkod/ürün kodunda desteklenmeyen startswith operatörünün reddini doğruluyor.
- Bir test build denemesi devam eden testin DLL kilidi nedeniyle MSB3021/MSB3027 verdi; test tamamlandıktan sonra aynı build başarılı (0 hata). Son rollback test koşusu bu başarılı build ile yapıldı.

## İade nedeni seed'leri — tamamlandı

- Kullanıcı ayrı uygulamayı onayladı. İki seed'in kendi Up SQL'i tek transaction'da çalıştırıldı; migration geçmişine yalnız bu iki başarılı seed kaydedildi. DropFirmPlatformInvoiceSeriesId uygulanmış gibi işaretlenmedi.
- Teslim Edilemedi: 1 lookup kaydı; Defo (Eski Sistem) ve Kalitesiz (Eski Sistem): 2 iade nedeni kaydı eklendi. Commit öncesinde bu üç kaydın ve InvoiceSeriesId kolonunun varlığı doğrulandı.
- Core'da yalnız 20260906120857_DropFirmPlatformInvoiceSeriesId bekliyor; bilinçli olarak korunuyor. Genel Core Migrate/otomatik migration açılmamalı; eski worker bağımlılığı çözülmeden bu adım yürütülmemeli.
- İlk denemede SQL içindeki JSON süslü parantezleri ExecuteSqlRaw biçimlendirmesine takıldı; transaction geri alındı. Aynı migration SQL'i transaction'a bağlı DbCommand ile yürütülecek şekilde düzeltildi. Araç build'i 0 uyarı/0 hata; ikinci uygulama başarılı.
- .59 değişmedi. Admin build ve yayın/ekran kabulü hâlâ bekliyor.

## Admin ve API yayını tamamlandı

- Kullanıcının güncel admin build'i doğrulandı. API01 ve API02 current: /opt/ECSProsAI/releases/20260911_final_returns_6ae426a8. İki düğümde ready=Healthy; paket/DLL hash doğrulaması başarılı. Ortam ayarları korundu, açılış migration'ı kapalı.
- .56 üzerinde yalnız multi-test admin statik release/link güncellendi: /usr/share/nginx/admin-releases/20260911_final_returns_6ae426a8. Nginx ayarı değişmedi; .59'a işlem yapılmadı.
- HTTP doğrulaması: /admin/login yeni /admin/assets/index-kNgFOUtb.js dosyasını sunuyor; yerel build ile aynı. Canlı bundle içinde Teslimatsız İade, Ödenecek ve Kapanan doğrulandı.
- İlk aktivasyon denemesinde CRLF shell sorunu oluştu; sürüm değişmeden durdu. Remote komutları LF'ye normalize edilip yüklenmiş arşiv hash doğrulamasıyla devam edildi. Son yayın başarılı.
- Geçici yerel/uzak yayın arşivleri temizlendi; önceki release'ler korundu. Browser giriş ekranında: oturum açılmış iade ekranı, paylaşım ve Excel görsel kabulü henüz yapılmadı; kullanıcıdan giriş istendi.
