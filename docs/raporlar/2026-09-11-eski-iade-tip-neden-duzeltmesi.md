# Eski iade tip ve neden düzeltmesi

Kullanıcı onayıyla yalnız multi-test .241/ecommerce_db üzerinde, API01 SSH tüneliyle uygulandı. .59'a bağlanılmadı/yazılmadı; hedefteki aktarım kaynak metadata'sı kullanıldı.

- 13 aktif legacy iadenin `legacyImport.returnId/rawType` bilgisi kimliği ve mevcut tipiyle eşleşti: 8 teslimatsız, 5 müşteri iadesi.
- 13 ReturnType güncellendi: legacy_type_1 -> undelivered, legacy_type_2 -> customer.
- Teslimatsız iadelerin 14 kaleminde legacy_not_delivered nedeni, güncel LegacyReturnImportSlice ile aynı sabit Teslim Edilemedi kimliğine bağlandı.
- 15 müşteri iade kalemindeki legacy_size / legacy_unspecified / legacy_disliked eşlemeleri zaten geçerli olduğundan korundu. Kaynak neden belirsizliği yeni bir neden tahmin edilerek giderilmedi.
- Yalnız ReturnType, ReturnReasonId ve ilgili UpdatedAt değişti. Stok, miktar, tutar, Status, RefundStatus, RefundMethod ve sipariş numaraları değiştirilmedi. legacy_imported gibi durum etiketleri bu dar kapsamın dışında kaldı.
- Tek transaction; beklenen 13/14 değişiklik sayısı uyuşmazsa rollback koruması. Tip/neden doğrulaması sonrası commit başarılı. Araç build: 0 hata/0 uyarı.
- Kapsam kimlikleri: 197069,199473,200369,209811,214171,214195,214799,215265,215401,217572,218529,219491,221201.
- Çalıştırıcı: tools/run-reviewed-schema-update.ps1 -ReturnFix. Bu tek seferlik komut tekrar çalıştırılırsa 13/14 sayısı karşılanmayacağı için rollback yapar; salt-okunur kontrol için -ReturnAudit kullanılır.

## Eski worker'ın geri yazması ve kalıcı düzeltme

- Sonraki incelemede eski worker'ın 27 değişiklikle tip/neden düzeltmesini geri yazdığı doğrulandı. İlk manuel düzeltme bu yüzden kalıcı değildi.
- Yalnız API01 ecspros-legacy-import.service, /opt/ECSProsAI/worker-releases/20260911_return_fix_6ae426a8 paketine geçirildi. Ayarlar korundu; Node__MigrateOnStartup=false, WorkerProfile=LegacyImport. API, ERP ve stok worker yolları değiştirilmedi.
- Paket/DLL SHA256 kontrolü geçti. Ready Healthy. Ek /health/detail isteği 404 verdiğinden betik hata bildirdi, fakat aktivasyon başarılıydı; aktif symlink ve ready ayrıca doğrulandı, betik ready kullanacak şekilde düzeltildi.
- 14:31:19 UTC: returns OK değişiklik=42, atlanan=0. Güncel importer kendi kaynak kurallarıyla başlık/kalem durum ve ödeme alanlarını da uzlaştırdı (ilk dar SQL'den farklı). 14:33:22 UTC: ikinci returns OK değişiklik=0, atlanan=0. Böylece yeniden eski tipe dönmediği doğrulandı.
- DB: 8 undelivered, 5 customer; 14 teslimatsız kalem sabit Teslim Edilemedi kimliğinde. Müşteri nedenleri korundu. 3 teslimatsız kayıt closed, diğerleri refunded durumunda.
- Worker'ın açık diğer dilimleri aynen korundu; ilk tur orders=53, images-missing=90 değişiklik raporladı. İkinci tur members'da 1 atlanan kayıt ayrı inceleme konusu; iade aktarımında atlanan/hata yok.
- Bu yayının yerel ve uzak geçici tar.gz arşivleri silindi. Önceki worker release geri dönüş için korundu. .59'a yazılmadı.
- Admin ve API genel yayını bu işlem kapsamında yapılmadı; eski adminin Türkçe tip/durum sözlükleri için güncel admin build/yayını hâlâ gerekli.
