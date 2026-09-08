# MySQL dışa aktarımıyla ERP grup adayları — 2026-09-07

> Sonraki kullanıcı talebiyle yalnız tutarlı 17 aday kaydedildi: Çanta 9, Pijama 2,
> Eşofman 2, Plaj Giyim 3, Vantilatör 1. Mevcut 44 korunarak toplam 61 eşli/143 açık
> duruma geçildi. Plan: tools/veri-bakim/2026-09-07-kesin-mysql-esleme-plani.json.
> Aşağıdaki 160 kayıt tablosu uygulama öncesi inceleme anını gösterir; ilk6 öneri
> kullanıcı tarafından onaylanmadı ve uygulanmadı. Tüm135 aday topluca taşınmadı.

## Sonuç ve kapsam

Kaynak: kullanıcının Query Result.xls dosyası, Sheet1, 774 veri satırı. Dosya değiştirilmedi.
Hedef .241 salt-okunur sorguyla doğrulandı: 145 aktif ürün grubu, 204 ERP sözlük grubu ve 44 aktif eşleme.
Excel, eski CreateStockCard.cs:786 UrunSinifBulAsync metodunun sınıf/cinsiyet/grup/altgrup ilişkilerini içeriyor.

Kalan 160 grup:
- 135 tek aktif PG hedefli aday.
- 13 birden fazla PG hedefli; otomatik seçilmedi.
- 12 birebir normalleştirilmiş MySQL alt grup adı yok.
- Tek hedefli adayların 35'i mevcut başka ERP kodlarının kullandığı gruba gidiyor; doğrudan kaydetmek mevcut eşlemeyi ezebilir.
- Mevcut eşlemelerden 12'si eski yöntemden farklı hedefe gidiyor; değiştirilmedi.

**Bu bir aday raporudur; hiçbir eşleme/ürün/tanım kaydedilmedi.** 135 tek hedefli adayın ticari anlam doğruluğu ayrıca onaylanmalıdır.
Örneğin Topuklu Terlik → Peluş Terlik, Ortam Kokusu → Mutfak Gereçleri ve Triko Ceket → Triko eski gruplamanın sonucudur; yeni sistemin doğru tercihi olduğu varsayılmadı.
Cinsiyet koşulları korunmalı. Tesettür ayrı Ortam özelliğidir; ad öneki otomatik silinmedi.

## Yöntem

### İlk karar paketi — öneri, henüz onaylanmadı

Eski veride cinsiyet kaynaklı çelişkiler var. Bunların iş kuralı olduğu varsayılmadı;
tek V3 kodunu farklı PG gruplarına bağlamak mevcut içe çözümde desteklenmiyor.
Aşağıdaki altı öneri aktif mevcut hedefler üzerinde ürün türüne göre hazırlanmıştır.
Ürün örneği incelemesinin yerine geçmez ve hiçbir kayıt değiştirilmemiştir.

| V3 kodu | V3 adı | Önerilen hedef | Eski verideki sorun |
| --- | --- | --- | --- |
| 02 | Basic Body | Body | 8 cinsiyet satırının 7'si Body, erkek bebek satırı Elbise; çoğunluk otomatik karar sayılmadı. |
| 1111 | şişme mont | Mont | Erkek çocuk Kaban'a, diğerleri Mont'a bağlı. |
| 145 | Şortlu Takım | İkili Takım | Kadın satırı Pantolon'a, erkek çocuk İkili Takım'a bağlı. |
| 2400 | Şişme Yelek | Yelek | Eski satırlar Mont/Kaban; ürün adıyla tutarsız üst grup. |
| 708 | Saç Kurutma Makinesi | Elektirikli Ev Aletleri | Kadın satırı Kişisel Bakım; cinsiyetsiz satır elektrikli aletler. |
| 1023 | Topuklu Terlik | Terlik | Tek hedef çıksa da eski hedef Peluş Terlik; peluş olduğu kanıtlanmıyor. |

Triko ailesi için ayrıca tek politika gerekli: ürün türü (Ceket/Yelek/Pantolon…)
mü yoksa mevcut Triko grubu mu? Ortam Kokusu, Organizer, Cüzdan, Ev Botu/Terliği ve
Yüz Siperi ürün örnekleri veya işletme kararı bekliyor. Excelde bulunmayan 12 grup
isim benzerliğiyle eklenmeyecek. Mevcut 44 eşleme korunacak.

V3 sözlük adı → Excel v3GrupAdi (Türkçe küçük harf, trim ve boşluk normalizasyonu) → MySQL grup ID.
MigrationTool.EnsureProductGroupMap ile aynı sıra: aktif grp_{MySQL ID} varsa o; yoksa docs/grup_eslesme.md birleşme kodu.
Her hedef güncel aktif PG listesinde doğrulandı. MySQL kısa kodlarıyla V3 kodları join edilmedi.
Eski metodun boş sonuçta 1/1/493 ataması ve Rows[0] seçimi kullanılmadı.
Excel bir dışa aktarım anıdır; kaynak canlılığı ve gerçek ürün bazında atamalar bu dosyayla doğrulanamaz.
Birden fazla V3 kodu aynı PG gruba gidecekse mevcut direct eşlemeyi ezmeden havuz/kurallı yapı değerlendirilmelidir.

## Kalan 160 kaydın tamamı

| V3 kodu | V3 adı | Sınıf | Aktif PG hedefleri | MySQL grup ID'leri | Cinsiyetler | Hedefteki mevcut ERP kodları |
| --- | --- | --- | --- | --- | --- | --- |
| 00 | tozlu | MySQL alt grup adı yok |  |  |  |  |
| 0017 | Kot Gömlek | MySQL alt grup adı yok |  |  |  |  |
| 002 | Kolonya | Tek hedefli aday | Kişisel Bakım | 197 | Unisex |  |
| 011 | Alışveriş Çantası | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 02 | Basic Body | Birden fazla PG hedefi | Body; Elbise | 75, 69, 97, 44, 259, 154, 202, 218 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 036 | Şemsiye | MySQL alt grup adı yok |  |  |  |  |
| 054 | Koruyucu Önlük | Tek hedefli aday | Aksesuar | 2, 117 | Kadın, Unisex |  |
| 069 | Trekking Ayakkabı | MySQL alt grup adı yok |  |  |  |  |
| 10 | Jean Pantolon | Tek hedefli aday | Pantolon | 3, 61, 87, 45, 175 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Kız Bebek | 09 |
| 1000 | Cilt Maskesi | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 1002 | Jartiyer | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 1005 | Çıtçıtlı Atlet | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 1006 | Hastahane Çıkışı | MySQL alt grup adı yok |  |  |  |  |
| 1007 | Babet Çorabı | Tek hedefli aday | İç Giyim | 118, 121, 120 | Kadın, Kız Çocuk, Erkek Çocuk | 021 |
| 1009 | Banyo Ürünleri | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 101 | Triko Ceket | Tek hedefli aday | Triko | 14 | Kadın |  |
| 1012 | Pantolon Çorabı | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 1013 | Soket Çorap | Tek hedefli aday | İç Giyim | 118, 119, 189 | Kadın, Erkek, Unisex | 021 |
| 1015 | Fantazi Takım | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 1023 | Topuklu Terlik | Tek hedefli aday | Peluş Terlik | 26 | Kadın |  |
| 1025 | Topuklu Bot | Tek hedefli aday | Bot | 21 | Kadın | 86 |
| 1028 | Topuklu Çizme | Tek hedefli aday | Çizme | 24 | Kadın | 87 |
| 1032 | Düz Sandalet | Tek hedefli aday | Sandalet | 27 | Kadın | 84 |
| 1040 | Günlük Portföy Çanta | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 105 | ev botu | Birden fazla PG hedefi | Peluş Terlik; Bot; Terlik | 26, 30, 144 | Kadın, Erkek, Erkek Çocuk |  |
| 1052 | Külotlu Çorap | Tek hedefli aday | İç Giyim | 118, 121, 120, 206, 221 | Kadın, Kız Çocuk, Erkek Çocuk, Kız Bebek, Erkek Bebek | 021 |
| 1053 | İçlik | Tek hedefli aday | İç Giyim | 118, 119, 120 | Kadın, Erkek, Erkek Çocuk | 021 |
| 107 | Triko Yelek | Birden fazla PG hedefi | Triko; Yelek | 14, 67, 104, 40, 230 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Erkek Bebek |  |
| 108 | Triko Elbise | Tek hedefli aday | Triko | 14 | Kadın |  |
| 109 | Triko Süveter | Tek hedefli aday | Triko | 14, 102 | Kadın, Kız Çocuk |  |
| 1111 | şişme mont | Birden fazla PG hedefi | Mont; Kaban | 78, 73, 85, 49 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk |  |
| 113 | Penye Pantolon | Tek hedefli aday | Pantolon | 3, 87 | Kadın, Kız Çocuk | 09 |
| 119 | Deri Ceket | Tek hedefli aday | Ceket | 76, 130 | Kadın, Erkek | 17 |
| 12 | Tayt | Tek hedefli aday | Pantolon | 3, 87, 150, 175, 223 | Kadın, Kız Çocuk, Bebek, Kız Bebek, Erkek Bebek | 09 |
| 122 | Topuklu Sandalet | Tek hedefli aday | Sandalet | 27 | Kadın | 84 |
| 1223 | Bileklik | Tek hedefli aday | Aksesuar | 2 | Kadın |  |
| 1224 | Mevsimlik Mont | Tek hedefli aday | Mont | 78, 73, 85, 251 | Kadın, Erkek, Kız Çocuk, Çocuk | 114 |
| 123 | Kolye | Tek hedefli aday | Aksesuar | 2, 71, 115 | Kadın, Erkek, Kız Çocuk |  |
| 1234 | küpe | Tek hedefli aday | Aksesuar | 2, 115 | Kadın, Kız Çocuk |  |
| 1250 | Ağda | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 130 | Çapraz Çanta | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 131 | Sırt Çantası | Tek hedefli aday | Çanta | 83, 84 | Kadın, Kız Çocuk |  |
| 133 | Günlük Çanta | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 1423 | bebek battaniye | MySQL alt grup adı yok |  |  |  |  |
| 145 | Şortlu Takım | Birden fazla PG hedefi | Pantolon; İkili Takım | 3, 48 | Kadın, Erkek Çocuk |  |
| 147 | Yüz Siperi | Birden fazla PG hedefi | Kişisel Bakım; Aksesuar | 132, 71, 197 | Kadın, Erkek, Unisex |  |
| 15 | Triko Bluz | Tek hedefli aday | Triko | 14, 102, 157, 212, 228 | Kadın, Kız Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 150 | Aseton | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 1502 | Tüy Dökücü Krem | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 1510 | Diş İpi | Tek hedefli aday | Banyo ve Ev Gereçleri | 185 | Cinsiyetsiz |  |
| 1515 | Abiye Çanta | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 1525 | Ağda Bezi | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 1529 | Manikür & Pens | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 1531 | Ağda Isıtıcı | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 1540 | Kalem Tıraş | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 1545 | Dudak Makyajı | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 1550 | Makyaj Fırçası | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 1551 | Makyaj Pamuğu | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 1553 | Ağda Temizleme Yağı | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 1655 | Göz Farı | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 1972 | Triko Panço | Tek hedefli aday | Triko | 14 | Kadın |  |
| 1978 | DENİZ ŞORT | Tek hedefli aday | Aktif Spor | 70 | Erkek |  |
| 199 | Triko Takım | Tek hedefli aday | Triko | 14 | Kadın |  |
| 20 | Ev Terliği | Birden fazla PG hedefi | Peluş Terlik; Terlik | 26, 33, 148, 52 | Kadın, Erkek, Kız Çocuk, Çocuk |  |
| 200 | Triko Pantolon | Birden fazla PG hedefi | Triko; Pantolon | 14, 175 | Kadın, Kız Bebek |  |
| 2004 | Mevsimlik Ceket | MySQL alt grup adı yok |  |  |  |  |
| 201 | Organizer | Birden fazla PG hedefi | Makyaj Malzemeleri; Banyo ve Ev Gereçleri | 137, 191 | Kadın, Unisex |  |
| 21 | Abiye | Tek hedefli aday | Elbise | 1, 86 | Kadın, Kız Çocuk | 13 |
| 23 | Kemer | Tek hedefli aday | Aksesuar | 2, 71 | Kadın, Erkek |  |
| 233 | Cilt Bakımı | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 2400 | Şişme Yelek | Birden fazla PG hedefi | Mont; Kaban | 78, 73, 85, 49 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk |  |
| 245 | Aplikatör | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 25 | Bere | Tek hedefli aday | Aksesuar | 2, 71 | Kadın, Erkek |  |
| 260 | Cımbız | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 261 | Dipliner | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 265 | Dudak Kalemi | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 268 | Epilasyon Yayı | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 27 | Salopet | Tek hedefli aday | Pantolon | 3, 87 | Kadın, Kız Çocuk | 09 |
| 275 | Eyeliner | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 276 | Kaş Farı | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 278 | Fırça | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 279 | Fondoten | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 28 | Triko Tunik | Tek hedefli aday | Triko | 14 | Kadın |  |
| 280 | Göz Kalemi | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 294 | Kapatıcı | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 2956 | Blazer Ceket | Tek hedefli aday | Ceket | 76 | Kadın | 17 |
| 297 | Kaş Makası | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 30 | Boyunluk | Tek hedefli aday | Aksesuar | 2, 71, 117 | Kadın, Erkek, Unisex |  |
| 301 | Vücut Çorabı | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 303 | Makyaj Sabitleyici | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 304 | Makyaj Süngeri | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 31 | Eşofman Altı | Tek hedefli aday | Eşofman | 13, 65, 99, 47, 255, 135, 204, 219 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 312 | Oje | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 315 | Dudak Parlatıcı | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 319 | Pudra | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 32 | Triko Hırka | Birden fazla PG hedefi | Triko; Hırka | 14, 67, 102, 40, 205, 220 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Kız Bebek, Erkek Bebek |  |
| 320 | Ruj | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 334 | Takma Kirpik | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 335 | Takma Tırnak | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 34 | Çorap | Tek hedefli aday | İç Giyim | 118, 119, 121, 120, 162, 189, 206, 221 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Bebek, Unisex, Kız Bebek, Erkek Bebek | 021 |
| 348 | Törpü | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 35 | Telefon Kılıfı | Tek hedefli aday | Telefon ve Aksesuarları | 176 | Cinsiyetsiz |  |
| 35014 | Vantilatör | Tek hedefli aday | Elektirikli Ev Aletleri | 179 | Cinsiyetsiz |  |
| 361 | Desenli Şal | Tek hedefli aday | Aksesuar | 2 | Kadın |  |
| 388 | Highlighter | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 3942 | Şarj Kablosu | MySQL alt grup adı yok |  |  |  |  |
| 40 | Pijama Altı | Tek hedefli aday | Pijama | 15, 68, 96, 43, 156, 208, 224 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 405 | Fantazi Çorap | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 410 | Tırnak Bakım Ürünleri | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 434 | Pareo | Tek hedefli aday | Plaj Giyim | 123 | Kadın |  |
| 4455 | Bandana | Tek hedefli aday | Aksesuar | 2 | Kadın |  |
| 4654 | Şapka | Tek hedefli aday | Aksesuar | 2, 71, 115 | Kadın, Erkek, Kız Çocuk |  |
| 47 | Mouse | Tek hedefli aday | Bilgisayar | 177 | Cinsiyetsiz |  |
| 501 | Bel çantası | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 5230 | Şarj Aleti | Tek hedefli aday | Telefon ve Aksesuarları | 176 | Cinsiyetsiz |  |
| 5477 | İç Giyim Aksesuar | Tek hedefli aday | Aksesuar | 2 | Kadın |  |
| 550 | Sabahlık | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 5562 | Triko Etek | Tek hedefli aday | Triko | 14 | Kadın |  |
| 5645 | Plaj Çantası | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 60 | Büstiyer | MySQL alt grup adı yok |  |  |  |  |
| 620 | Ortam Kokusu | Tek hedefli aday | Mutfak Gereçleri | 184 | Cinsiyetsiz |  |
| 64 | Parfüm | Tek hedefli aday | Kişisel Bakım | 132, 138, 197 | Kadın, Erkek, Unisex |  |
| 65 | Deodorant | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 650 | Futbol Ayakkabısı | Tek hedefli aday | Spor Ayakkabı | 28, 143, 51 | Erkek, Erkek Çocuk, Çocuk | 111 |
| 66 | Roll On | Tek hedefli aday | Kişisel Bakım | 132, 138 | Kadın, Erkek |  |
| 70 | Omuz Şalı | Tek hedefli aday | Aksesuar | 2 | Kadın |  |
| 708 | Saç Kurutma Makinesi | Birden fazla PG hedefi | Kişisel Bakım; Elektirikli Ev Aletleri | 132, 179 | Kadın, Cinsiyetsiz |  |
| 712 | Powerbank | Tek hedefli aday | Telefon ve Aksesuarları | 176 | Cinsiyetsiz |  |
| 721 | Usb Hoparlör | Tek hedefli aday | Telefon ve Aksesuarları | 176 | Cinsiyetsiz |  |
| 7452 | Makyaj Çantası | Tek hedefli aday | Çanta | 83 | Kadın |  |
| 75 | İç Giyim Takım | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 756 | Kaş Kalemi | Tek hedefli aday | Makyaj Malzemeleri | 137 | Kadın |  |
| 789 | mama önlüğü | MySQL alt grup adı yok |  |  |  |  |
| 78923 | Gecelik | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| 7898 | Toka | Tek hedefli aday | Aksesuar | 2 | Kadın |  |
| 82 | Triko Kazak | Tek hedefli aday | Triko | 14, 67, 102, 40, 157, 212, 228 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 85 | Triko Tulum | Tek hedefli aday | Triko | 102 | Kız Çocuk |  |
| 89 | Cüzdan | Birden fazla PG hedefi | Çanta; Aksesuar | 83, 71 | Kadın, Erkek |  |
| 960 | Dezenfektan | Tek hedefli aday | Kişisel Bakım | 197 | Unisex |  |
| 992 | Bikini | Tek hedefli aday | Plaj Giyim | 123, 124, 155, 209, 225 | Kadın, Kız Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 993 | Mayo | Tek hedefli aday | Plaj Giyim | 123, 124, 199, 155, 209, 225 | Kadın, Kız Çocuk, Erkek Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| 994 | Wax | Tek hedefli aday | Kişisel Bakım | 132, 138, 197 | Kadın, Erkek, Unisex |  |
| 996 | Saç Boya Fırçası | Tek hedefli aday | Kişisel Bakım | 132 | Kadın |  |
| 997 | Basic Spor Atlet | Tek hedefli aday | Bustiyer | 4 | Kadın |  |
| 998 | Spor Büstiyer | Tek hedefli aday | Bustiyer | 4 | Kadın |  |
| 999 | Fitness Tayt | Tek hedefli aday | Pantolon | 3 | Kadın | 09 |
| AG | Güneş Gözlüğü | Tek hedefli aday | Aksesuar | 2, 71, 115, 116, 117 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Unisex |  |
| AH | Kol Saati | Tek hedefli aday | Aksesuar | 2, 71, 115, 116, 117 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Unisex |  |
| AL | Pijama Takımı | Tek hedefli aday | Pijama | 15, 68, 96, 43, 156, 208, 224 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| AM | Eşofman Takım | Tek hedefli aday | Eşofman | 13, 65, 99, 47, 135, 204, 219 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk, Bebek, Kız Bebek, Erkek Bebek |  |
| AN | Fantazi Gecelik | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| AP | Korse | Tek hedefli aday | İç Giyim | 118 | Kadın | 021 |
| AR | Sütyen | Tek hedefli aday | İç Giyim | 118, 121 | Kadın, Kız Çocuk | 021 |
| AT | Külot | Tek hedefli aday | İç Giyim | 118, 121, 120 | Kadın, Kız Çocuk, Erkek Çocuk | 021 |
| AU | Slip | Tek hedefli aday | İç Giyim | 119 | Erkek | 021 |
| BA | Patik | Tek hedefli aday | İç Giyim | 118, 162, 206, 221 | Kadın, Bebek, Kız Bebek, Erkek Bebek | 021 |
| BC | Boxer | Tek hedefli aday | İç Giyim | 118, 119, 121, 120 | Kadın, Erkek, Kız Çocuk, Erkek Çocuk | 021 |
| gy13 | Anahtarlık | MySQL alt grup adı yok |  |  |  |  |
| PT | Pijama Tulum | MySQL alt grup adı yok |  |  |  |  |
| sbm | Saç Bakımı | Tek hedefli aday | Kişisel Bakım | 132, 138, 197 | Kadın, Erkek, Unisex |  |

## Mevcut eşlemelerde eski yöntemden farklı sonuçlar

Bu farklar mevcut eşlemenin yanlış olduğunu kanıtlamaz; eski sistem daha kaba üst gruplar kullanıyor olabilir.

| V3 kodu | V3 adı | Mevcut hedef | MySQL yönteminin hedefi |
| --- | --- | --- | --- |
| 0016 | Kot Ceket | Kot Ceket | Ceket |
| 1080 | Eşarp | Eşarp | Aksesuar |
| 117 | Kapri | Kapri | Pantolon |
| 1528 | Makyaj Temizleyici | Makyaj Temizleyici | Makyaj Malzemeleri |
| 288 | Saç Boyası | Saç Boyası | Kişisel Bakım |
| 2955 | Kaban | Kaban | Mont |
| 307 | Maskara | Maskara | Makyaj Malzemeleri |
| 406 | Bone | Bone | Banyo Giyim |
| 413 | Kulaklık | Kulaklık | Aksesuar |
| 435 | Stiletto | Stiletto | Topuklu Ayakkabı |
| 63 | şal | Şal | Aksesuar |
| BU | Atlet | Atlet | İç Giyim |

## Çok hedefli 13 grubun kanıt satırları

| V3 kodu | V3 adı | Cinsiyet | MySQL grup ID | MySQL grup adı | Alt grup ID | PG hedefi |
| --- | --- | --- | --- | --- | --- | --- |
| 02 | Basic Body | Kadın | 75 | Kadın Body | 135 | Body |
| 02 | Basic Body | Erkek | 69 | Erkek Basic Body Atlet | 120 | Body |
| 02 | Basic Body | Kız Çocuk | 97 | Kız Çocuk Body | 177 | Body |
| 02 | Basic Body | Erkek Çocuk | 44 | Erkek Çocuk Body | 86 | Body |
| 02 | Basic Body | Çocuk | 259 | Çocuk Body | 819 | Body |
| 02 | Basic Body | Bebek | 154 | Bebek Body | 473 | Body |
| 02 | Basic Body | Kız Bebek | 202 | Kız Bebek Body | 723 | Body |
| 02 | Basic Body | Erkek Bebek | 218 | Erkek Bebek Elbise | 746 | Elbise |
| 105 | ev botu | Kadın | 26 | Kadın Peluş Ayakkabı Terlik | 791 | Peluş Terlik |
| 105 | ev botu | Erkek | 30 | Erkek Bot | 792 | Bot |
| 105 | ev botu | Erkek Çocuk | 144 | Erkek Çocuk Terlik | 806 | Terlik |
| 107 | Triko Yelek | Kadın | 14 | Kadın Triko | 324 | Triko |
| 107 | Triko Yelek | Erkek | 67 | Erkek Triko | 338 | Triko |
| 107 | Triko Yelek | Kız Çocuk | 104 | Kız Çocuk Yelek | 789 | Yelek |
| 107 | Triko Yelek | Erkek Çocuk | 40 | Erkek Çocuk Triko | 807 | Triko |
| 107 | Triko Yelek | Erkek Bebek | 230 | Erkek Bebek Yelek | 799 | Yelek |
| 1111 | şişme mont | Kadın | 78 | Kadın Mont | 780 | Mont |
| 1111 | şişme mont | Erkek | 73 | Erkek Mont | 779 | Mont |
| 1111 | şişme mont | Kız Çocuk | 85 | Kız Çocuk Mont | 781 | Mont |
| 1111 | şişme mont | Erkek Çocuk | 49 | Erkek Çouk Kaban | 778 | Kaban |
| 145 | Şortlu Takım | Kadın | 3 | Kadın Pantolon | 28 | Pantolon |
| 145 | Şortlu Takım | Erkek Çocuk | 48 | Erkek Çocuk İkili Takım | 442 | İkili Takım |
| 147 | Yüz Siperi | Kadın | 132 | Kadın Kişisel Bakım | 568 | Kişisel Bakım |
| 147 | Yüz Siperi | Erkek | 71 | Erkek Aksesuar | 434 | Aksesuar |
| 147 | Yüz Siperi | Unisex | 197 | Unisex Kişisel Bakım | 694 | Kişisel Bakım |
| 20 | Ev Terliği | Kadın | 26 | Kadın Peluş Ayakkabı Terlik | 59 | Peluş Terlik |
| 20 | Ev Terliği | Erkek | 33 | Erkek Terlik | 70 | Terlik |
| 20 | Ev Terliği | Kız Çocuk | 148 | Kız Çocuk Terlik | 453 | Terlik |
| 20 | Ev Terliği | Çocuk | 52 | Çocuk Terlik | 100 | Terlik |
| 200 | Triko Pantolon | Kadın | 14 | Kadın Triko | 855 | Triko |
| 200 | Triko Pantolon | Kız Bebek | 175 | Kız Bebek Pantolon | 798 | Pantolon |
| 201 | Organizer | Kadın | 137 | Kadın Makyaj Malzemeleri | 366 | Makyaj Malzemeleri |
| 201 | Organizer | Unisex | 191 | Unisex Makyaj Ürünleri | 669 | Banyo ve Ev Gereçleri |
| 2400 | Şişme Yelek | Kadın | 78 | Kadın Mont | 143 | Mont |
| 2400 | Şişme Yelek | Erkek | 73 | Erkek Mont | 133 | Mont |
| 2400 | Şişme Yelek | Kız Çocuk | 85 | Kız Çocuk Mont | 160 | Mont |
| 2400 | Şişme Yelek | Erkek Çocuk | 49 | Erkek Çouk Kaban | 95 | Kaban |
| 32 | Triko Hırka | Kadın | 14 | Kadın Triko | 318 | Triko |
| 32 | Triko Hırka | Erkek | 67 | Erkek Triko | 337 | Triko |
| 32 | Triko Hırka | Kız Çocuk | 102 | Kız Çocuk Triko | 466 | Triko |
| 32 | Triko Hırka | Erkek Çocuk | 40 | Erkek Çocuk Triko | 439 | Triko |
| 32 | Triko Hırka | Kız Bebek | 205 | Kız Bebek Hırka | 787 | Hırka |
| 32 | Triko Hırka | Erkek Bebek | 220 | Erkek Bebek Hırka | 794 | Hırka |
| 708 | Saç Kurutma Makinesi | Kadın | 132 | Kadın Kişisel Bakım | 396 | Kişisel Bakım |
| 708 | Saç Kurutma Makinesi | Cinsiyetsiz | 179 | Elektirikli Ev Aletleri | 647 | Elektirikli Ev Aletleri |
| 89 | Cüzdan | Kadın | 83 | Kadın Çanta | 152 | Çanta |
| 89 | Cüzdan | Erkek | 71 | Erkek Aksesuar | 126 | Aksesuar |
