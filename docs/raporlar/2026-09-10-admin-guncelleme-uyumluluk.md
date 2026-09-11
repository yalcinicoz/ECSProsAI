# Admin güncelleme uyumluluk düzeltmeleri

Tarih: 2026-09-10. Taban: GitHub `164033f5`. Kapsam: yerel kod/test düzeltmesi; yayın değil.

## Başlangıç ve nedenler

GitHub alımı sonrası admin paketinde 51 testin 19'u başarısızdı:

| Alan | Sayı | Neden ve düzeltme |
|---|---:|---|
| ERP eşleme | 8 | Dictionary/modal testleri dosyaya eklenen grid importunu çözemiyordu. Yalnız bu testlerin kullanmadığı grid sekmesi için açık sınır mock'u eklendi; beklenmeyen grid hook çağrısı hata verir. Eşleme, çakışma ve yazma kapıları aynı testlerle doğrulanır. |
| Favoriler | 3 | Eski test kullanıcısı tüm izinleri reddediyordu; yeni sayfa izinleriyle erişebileceği favori kalmıyordu. Asgari izin kümesi tanımlandı; yetkisiz entegrasyonlar kapalı kaldı. Ek test: izin iptalinde favori gizlenir, sayaç sorgusu kapanır, kayıt silinmez. |
| Menü | 1 | Eski envanter 65 öğe bekliyordu; üç yeni yetki sayfasıyla 68 oldu. Yeni sayfaların ad/izinleri açıkça denetleniyor; eski URL/ikon/rozet tekilliği ve tüm sayfalarda izin zorunluluğu korunuyor. |
| Ürün grupları | 7 | Eski testler istemcide filtrelenen HTML tablosunu bekliyordu; ekran artık sunucuda sayfalı DataGrid. Gerçek useGridState ve sayfa olaylarıyla uç, filtre, sayfa sıfırlama, export, gezinme ve yeni grup sözleşmeleri test ediliyor. |

## Bulunan gerçek davranış farkı

Ürün grubu aramasının yeni sunucu uygulaması yalnız kod ve Türkçe adı basit küçük harf karşılaştırmasıyla arıyordu. Önceki Türkçe/ASCII ve diğer dil adları araması kaybolmuştu.

- Arama tüm NameI18n değerlerini PostgreSQL yerleşik JSONPath fonksiyonuyla tarar; dil anahtarları sonuç sayılmaz.
- Türkçe harf eşdeğerleri ve sözcükler arası boşluklar desteklenir; `gomlek`, `GÖMLEK`, `Shirt`, `ic giyim`, `ISIKLI` test edilir.
- Kullanıcı metni regex için kaçırılır ve JSONPath parametre olarak bağlanır; serbest SQL birleştirilmez.
- Sayfalama/sıralama sunucuda kalır. Liste ve export ortak ApplyNamed yolunu kullanır; dropdown ucuna dokunulmaz.
- Yeni DB fonksiyonu, tablo veya migration eklenmedi. PostgreSQL built-in fonksiyonuna EF çevirisi eklendi.

Kart mesajlarında boş liste her render'da yeni referans oluşturuyordu. `useMemo` ile stabilize edildi; mevcut önizleme/liste davranışı değiştirilmedi.

## Değişen dosyalar

- `admin/tests/erp-mapping-workflow.test.cjs`
- `admin/tests/favorites.test.cjs`
- `admin/tests/sidebar-navigation.test.cjs`
- `admin/tests/product-group-search.test.cjs`
- `admin/src/pages/storefront/ProductCardPage.tsx`
- `src/Modules/Catalog/ECSPros.Catalog.Application/Helpers/ProductGroupSearch.cs` (yeni)
- `src/Modules/Catalog/ECSPros.Catalog.Application/Queries/GetProductGroups/ProductGroupGrid.cs`
- `src/Modules/Catalog/ECSPros.Catalog.Infrastructure/Persistence/CatalogDbContext.cs`
- `tests/ECSPros.Api.Tests/ProductGroupSearchTests.cs` (yeni)
- Bu rapor ve `PROGRESS.md`.

## Doğrulama ve sınırlar

- Admin Node testleri: 52/52 başarılı; test kapatma veya skip eklenmedi.
- Yeni API arama testleri: 3/3 başarılı; SQL çevirisi `ToQueryString` ile bağlantısız doğrulandı.
- Genel API (DB/Acceptance hariç): 276 başarılı, 1 mevcut atlama, 0 başarısız.
- TypeScript ve genel ESLint başarılı: 0 hata, 0 lint uyarısı.
- Gerçek PostgreSQL üzerinde sorgu çalıştırılması, performans ölçümü ve oturumlu tarayıcı kabulü bu yerel testlerin yerine geçmiş sayılmaz; yapılmadı.
- Production build, migration uygulama, sunucu/DB işlemi, yayın ve GitHub push yapılmadı. Önceki PDF/notlar korunuyor.

## Önerilen sonraki adım

Yayın öncesinde izole test ortamında ürün grubu aramasını ve aynı filtreyle Excel çıktısını doğrulamak; ardından kullanıcı onayıyla build/yayın. GitHub'dan gelen altı migration ayrı yayın ön koşuludur; bu görevde uygulanmadı.
