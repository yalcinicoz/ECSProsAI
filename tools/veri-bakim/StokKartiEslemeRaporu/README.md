# Stok kartı grup eşleme raporu

Varsayılan Read modu yalnız okur. V3 tip 2 kaynak kodu sözlük JOIN'iyle
kanonikleştirilir; .241 üzerinde aktif doğrudan eşlemeler, ürün grupları ve
urun_grubu varsayılanı karşılaştırılır. Mevcut farklı değerler korunacak olarak
raporlanır. Çıktı özet ve durum başına beş örnek içerir; tam ürün dökümü değildir.

`run-readonly.ps1 -Assembly <derlenmiş DLL>` ignored appsettingsTest.json'u
bellekten okuyarak yalnız API01 üzerinden geçici loopback 11433/15432 tüneli
açar. Dolu porta müdahale etmez; finally yalnız kendi tünelini kapatır.
Bağlantılar stdin JSON ile iletilir, CLI argümanına veya dosyaya yazılmaz.
Kaynak/hedef guard'ları kayıtlı topolojiye özeldir; farklı hedefe kör yönlendirmeyin.

Read için PostgreSQL READ ONLY transaction ve kimlik kontrolü vardır. API host,
worker ve seed çağrılmaz. Sadece bu projeyi derleyin;
API/admin build çıktısını değiştirmek gerekmez.

`-Action Rehearse` gerçek hedefte aynı UPDATE/INSERT işlemlerini tek transaction
içinde deneyip ROLLBACK yapar; ayrı DB yazım onayı gerektirir. `-Action Apply`
COMMIT yapar ve yalnız açık uygulama onayıyla kullanılmalıdır. Eski worker'ın
geri ezmesini önlemek için API/worker panel modu aktivasyonu ön koşuldur.
Tablo kilitleri en fazla 5 sn bekler; SQL statement timeout 30 sn. Onaylı
13433 grup ve 29088 özellik üst sınırı aşılırsa transaction durur. Farklı veya
soft-delete özellik korunur. Fiyat/stok/görsel/definition tablolarına yazılmaz.
Yeni rapor/scope onayı olmadan üst sınırlar artırılmaz. Eski dosya adı
run-readonly.ps1 geriye uyumluluk için korunmuştur; varsayılan hâlâ Read'dir.
