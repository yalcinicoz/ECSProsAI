# 0.59 IAM referans aktarımı — 2026-09-11

## Kapsam ve güvenlik

Kullanıcı, menüdeki yetki grupları, izin içerikleri ve kullanıcıların 0.59'dan multi-test DB'ye aktarımını onayladı. Ek olarak mevcut şifre/süper admin durumlarının korunması, yeni kullanıcıların kaynak giriş bilgileriyle alınması teyit edildi.

- Kaynak .59 PostgreSQL: API01 SSH tüneli üzerinden, default_transaction_read_only=on. Kaynakta yazım yok.
- Hedef: API01 üzerinden 192.168.0.241/ecommerce_db, yalnız IAM tabloları. Nginx, servis konfigürasyonu, fiyat/iş verileri ve migration değişmedi.
- Mevcut kullanıcı kimlikleri, kullanıcı adı/e-posta, şifre hash'i, şifre değiştirme bayrakları ve süper admin durumu korundu. Yeni hesaplar kaynak aktif/pasif durumuyla alındı; pasif hesaplar aktifleştirilmedi.
- Oturum/token ve kullanıcı tercihleri kopyalanmadı. Şifre/hash/snapshot dosyası oluşturulmadı. Veri bellekte ve SSH üzerinden taşındı, çıktılar yalnız sayımları içeriyor.
- Firma/kanal kodları ve kullanıcı/grup/izin kimlikleri eşleştirildi. Belirsiz veya eksik kimlik/kapsamda işlem durur; kanal kapsamı genel yetkiye çevrilmez.
- Kaynakta olmayan yerel hesaplar korunur. test1 kaynakta yoktur, bizde tutuldu; otomatik grup atanmadı.

## Uygulanan değişiklikler

| Kayıt | Eklenen | Güncellenen |
|---|---:|---:|
| Yetki grubu | 8 | 0 |
| Yetki tanımı | 63 | 11 |
| Kullanıcı | 41 | 0 |
| Grup–izin bağlantısı | 261 | 1 |
| Kullanıcı–grup bağlantısı | 3 | 0 |
| Kullanıcı izin istisnası | 0 | 0 |

Hiçbir mevcut bağlantı bu çalışmada pasife alınmadı; kayıt silinmedi. Hedefte 11 grup, 74 izin, 50 kullanıcı (49 kaynak + yerel test1), 290 grup–izin ve 8 kullanıcı–grup bağlantısı bulunur. Aktarım özeti IAM denetim tablosuna `yetki.referans.aktarim` olarak yazıldı; aktör kimliği taklit edilmedi, bakım işlemi olarak kaydedildi.

## Doğrulamalar

- İlk kontrol, eski silinmiş bağlantı ile aktif bağlantının aynı anahtarı taşıdığını saptadı. Yazmadan durdu; aktif kayıt seçimi düzeltildi. Birden fazla aktif eşleşme hâlâ işlemi durdurur.
- İki transaction denemesi tüm beklenen alanları PostgreSQL tipleriyle karşılaştırdı ve ROLLBACK ile bitti. İkinci deneme denetim kaydını da kapsadı.
- Asıl işlem tablo kilitleri ve eşzamanlı değişikliklere karşı snapshot kontrolüyle, doğrulamalar geçince COMMIT edildi.
- Son salt okunur Inspect: tüm tablolarda insert/update/deactivate sayısı sıfır.
- API01 ve API02 /ready: Healthy (PostgreSQL, Redis, DataProtection).
- Uygulama build/publish/restart yapılmadı; sadece veri bakımı. Yetki önbelleğinin mevcut L2 süresi 5 dakika, L1 süresi 10 saniye; önbellekler topluca temizlenmedi. Açık panelde yeniden giriş gerekebilir.
- Test hesabıyla yetkili rapor/paylaşım testi kapanmış değildir: test1 için ayrıca uygun grup seçilmelidir.

## Tekrar kullanılabilir bakım dosyaları

- `tools/veri-bakim/sync-reference-iam.ps1`: varsayılan Inspect; mevcut host-pinli salt okunur referans bağlantısını kullanır. API01 üzerinden 127.0.0.1:12259 tüneli gerektirir.
- `tools/veri-bakim/sync-reference-iam-snapshot.sql`: IAM ve firma/kanal anahtarlarını okur; çıktı hassastır, dosyaya/loga yazılmamalıdır.
- `tools/veri-bakim/sync-reference-iam.py`: Inspect/Rehearse/Apply; kimlik kontrolleri, transaction ve tipli sonuç doğrulamaları. Kaynak yönetimindeki bağlantıları hizalar; gelecekte farklı bir kaynak snapshot'ı için önce Inspect/Rehearse gereklidir.

SSH tüneli iş bitiminde kapatıldı; sunucuda geçici dosya veya yedek bırakılmadı.

## test1 için ayrıca onaylanan yetkiler

- Kullanıcının sonraki açık isteğiyle yalnız `reports.ai.use` ve `inventory.view` doğrudan kullanıcı izni olarak verildi. Grup, süper admin, paylaşım, export, ürün yönetimi, sipariş veya müşteri izni eklenmedi.
- `reports.ai.use` kaynak aktarımında bulunmuyordu; mevcut `PermissionKatalogu` tanımıyla hedef IAM kataloğuna eklendi. Stok izni aktif ve kanal-kapsamsız doğrulandı.
- `tools/veri-bakim/grant-test1-report-stock.sql`: kullanıcı kimliği/durumu, izin kapsamı ve verilen iki izni doğrulayan transaction. Önce ROLLBACK denemesi, ardından COMMIT başarılı. Son SELECT iki `grant` kaydını doğruladı; IAM denetim kaydı eklendi.
- Açık tarayıcı ilk yenilemede eski erişimsiz ekranı gösterdi; ekran kabulü tamamlandı diye işaretlenmedi. Mevcut yetki önbelleği en fazla 5 dakika (+L1 10 saniye) eski kalabilir; yeniden giriş/yenileme gerekebilir. Toplu cache temizliği, restart veya production işlemi yapılmadı.

### Firma bağlantısı ve ekran kontrolü

- Yeniden giriş sonrası test1 rapor/stok menülerine erişti; AI kaynakları yalnız güncel stok ve stok hareketleri. P-00022295 stok araması 31 kayıt gösterdi.
- AI ekranındaki boş firma nedeniyle kullanıcı ayrıca MİŞAROĞLU bağlantısını onayladı. Yalnız test1 FirmId null -> `1235fa20-920c-466f-ad67-c5d5c6b6a86f` güncellendi. Kimlik ve önceki null durumu transaction içinde doğrulandı; IAM denetim kaydı eklendi. İzin, şifre ve süper admin bayrağı değişmedi.
- AI ekranında MİŞAROĞLU seçeneği geldi ve seçildi. Bu kontrol yeni AI isteği çalıştırıldığı anlamına gelmez. Kaynak .59'a veya production ayarlarına dokunulmadı.
