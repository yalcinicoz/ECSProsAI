# test1 AI ekran kabulü — 2026-09-11

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
