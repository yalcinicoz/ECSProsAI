# ERP sözlüğü ve manuel grup eşleme listesi — 2026-09-07

## Sonuç

- V3 `dbo.cdItemAttributeDesc`: ItemTypeCode=1, AttributeTypeCode=2, LangCode=TR; 204 kaynak kod.
- Hedef: yalnız .241/ecommerce_db. 204 ERP sözlük kaydı ve 44 doğrudan eşleme eklendi.
- Mevcut 145 ürün grubu korundu; yeni ürün grubu, özellik tipi veya ürün kartı oluşturulmadı.
- Kodların baştaki sıfırları korundu. Otomatik eşleme yalnız tekil, birebir ad üzerinden yapıldı; Türkçe büyük/küçük harf ve boşluk normalizasyonu kullanıldı.
- Mevcut kayıtlar değiştirilmedi. Apply sonrası bağımsız Inspect ve rollback Rehearse: 0 yeni sözlük, 0 yeni eşleme, 44 zaten eşli.
- .59'a ve V3'e yazılmadı. Yayın, push veya worker aktivasyonu yapılmadı.

## Manuel işlem

Pazaryerleri → Eşleştirme — Pazaryeri & ERP alanında ERP sözlüğünün ürün gruplarını kullanın.
Aşağıdaki 160 kayıt sözlükte hazırdır; uygun mevcut ürün grubunu siz belirleyebilirsiniz.
Birden fazla V3 kodu aynı bizim gruba bağlanacaksa mevcut eşlemeyi ezmek yerine havuz/kurallar ihtiyacını değerlendirin; ters yönde belirsiz eşleme oluşturmayın.

| V3 kodu | V3 adı | Otomatik bağlanmama nedeni |
| --- | --- | --- |
| 00 | tozlu | Birebir aynı adlı ürün grubu yok |
| 0017 | Kot Gömlek | Birebir aynı adlı ürün grubu yok |
| 002 | Kolonya | Birebir aynı adlı ürün grubu yok |
| 011 | Alışveriş Çantası | Birebir aynı adlı ürün grubu yok |
| 02 | Basic Body | Birebir aynı adlı ürün grubu yok |
| 036 | Şemsiye | Birebir aynı adlı ürün grubu yok |
| 054 | Koruyucu Önlük | Birebir aynı adlı ürün grubu yok |
| 069 | Trekking Ayakkabı | Birebir aynı adlı ürün grubu yok |
| 10 | Jean Pantolon | Birebir aynı adlı ürün grubu yok |
| 1000 | Cilt Maskesi | Birebir aynı adlı ürün grubu yok |
| 1002 | Jartiyer | Birebir aynı adlı ürün grubu yok |
| 1005 | Çıtçıtlı Atlet | Birebir aynı adlı ürün grubu yok |
| 1006 | Hastahane Çıkışı | Birebir aynı adlı ürün grubu yok |
| 1007 | Babet Çorabı | Birebir aynı adlı ürün grubu yok |
| 1009 | Banyo Ürünleri | Birebir aynı adlı ürün grubu yok |
| 101 | Triko Ceket | Birebir aynı adlı ürün grubu yok |
| 1012 | Pantolon Çorabı | Birebir aynı adlı ürün grubu yok |
| 1013 | Soket Çorap | Birebir aynı adlı ürün grubu yok |
| 1015 | Fantazi Takım | Birebir aynı adlı ürün grubu yok |
| 1023 | Topuklu Terlik | Birebir aynı adlı ürün grubu yok |
| 1025 | Topuklu Bot | Birebir aynı adlı ürün grubu yok |
| 1028 | Topuklu Çizme | Birebir aynı adlı ürün grubu yok |
| 1032 | Düz Sandalet | Birebir aynı adlı ürün grubu yok |
| 1040 | Günlük Portföy Çanta | Birebir aynı adlı ürün grubu yok |
| 105 | ev botu | Birebir aynı adlı ürün grubu yok |
| 1052 | Külotlu Çorap | Birebir aynı adlı ürün grubu yok |
| 1053 | İçlik | Birebir aynı adlı ürün grubu yok |
| 107 | Triko Yelek | Birebir aynı adlı ürün grubu yok |
| 108 | Triko Elbise | Birebir aynı adlı ürün grubu yok |
| 109 | Triko Süveter | Birebir aynı adlı ürün grubu yok |
| 1111 | şişme mont | Birebir aynı adlı ürün grubu yok |
| 113 | Penye Pantolon | Birebir aynı adlı ürün grubu yok |
| 119 | Deri Ceket | Birebir aynı adlı ürün grubu yok |
| 12 | Tayt | Birebir aynı adlı ürün grubu yok |
| 122 | Topuklu Sandalet | Birebir aynı adlı ürün grubu yok |
| 1223 | Bileklik | Birebir aynı adlı ürün grubu yok |
| 1224 | Mevsimlik Mont | Birebir aynı adlı ürün grubu yok |
| 123 | Kolye | Birebir aynı adlı ürün grubu yok |
| 1234 | küpe | Birebir aynı adlı ürün grubu yok |
| 1250 | Ağda | Birebir aynı adlı ürün grubu yok |
| 130 | Çapraz Çanta | Birebir aynı adlı ürün grubu yok |
| 131 | Sırt Çantası | Birebir aynı adlı ürün grubu yok |
| 133 | Günlük Çanta | Birebir aynı adlı ürün grubu yok |
| 1423 | bebek battaniye | Birebir aynı adlı ürün grubu yok |
| 145 | Şortlu Takım | Birebir aynı adlı ürün grubu yok |
| 147 | Yüz Siperi | Birebir aynı adlı ürün grubu yok |
| 15 | Triko Bluz | Birebir aynı adlı ürün grubu yok |
| 150 | Aseton | Birebir aynı adlı ürün grubu yok |
| 1502 | Tüy Dökücü Krem | Birebir aynı adlı ürün grubu yok |
| 1510 | Diş İpi | Birebir aynı adlı ürün grubu yok |
| 1515 | Abiye Çanta | Birebir aynı adlı ürün grubu yok |
| 1525 | Ağda Bezi | Birebir aynı adlı ürün grubu yok |
| 1529 | Manikür & Pens | Birebir aynı adlı ürün grubu yok |
| 1531 | Ağda Isıtıcı | Birebir aynı adlı ürün grubu yok |
| 1540 | Kalem Tıraş | Birebir aynı adlı ürün grubu yok |
| 1545 | Dudak Makyajı | Birebir aynı adlı ürün grubu yok |
| 1550 | Makyaj Fırçası | Birebir aynı adlı ürün grubu yok |
| 1551 | Makyaj Pamuğu | Birebir aynı adlı ürün grubu yok |
| 1553 | Ağda Temizleme Yağı | Birebir aynı adlı ürün grubu yok |
| 1655 | Göz Farı | Birebir aynı adlı ürün grubu yok |
| 1972 | Triko Panço | Birebir aynı adlı ürün grubu yok |
| 1978 | DENİZ ŞORT | Birebir aynı adlı ürün grubu yok |
| 199 | Triko Takım | Birebir aynı adlı ürün grubu yok |
| 20 | Ev Terliği | Birebir aynı adlı ürün grubu yok |
| 200 | Triko Pantolon | Birebir aynı adlı ürün grubu yok |
| 2004 | Mevsimlik Ceket | Birebir aynı adlı ürün grubu yok |
| 201 | Organizer | Birebir aynı adlı ürün grubu yok |
| 21 | Abiye | Birebir aynı adlı ürün grubu yok |
| 23 | Kemer | Birebir aynı adlı ürün grubu yok |
| 233 | Cilt Bakımı | Birebir aynı adlı ürün grubu yok |
| 2400 | Şişme Yelek | Birebir aynı adlı ürün grubu yok |
| 245 | Aplikatör | Birebir aynı adlı ürün grubu yok |
| 25 | Bere | Birebir aynı adlı ürün grubu yok |
| 260 | Cımbız | Birebir aynı adlı ürün grubu yok |
| 261 | Dipliner | Birebir aynı adlı ürün grubu yok |
| 265 | Dudak Kalemi | Birebir aynı adlı ürün grubu yok |
| 268 | Epilasyon Yayı | Birebir aynı adlı ürün grubu yok |
| 27 | Salopet | Birebir aynı adlı ürün grubu yok |
| 275 | Eyeliner | Birebir aynı adlı ürün grubu yok |
| 276 | Kaş Farı | Birebir aynı adlı ürün grubu yok |
| 278 | Fırça | Birebir aynı adlı ürün grubu yok |
| 279 | Fondoten | Birebir aynı adlı ürün grubu yok |
| 28 | Triko Tunik | Birebir aynı adlı ürün grubu yok |
| 280 | Göz Kalemi | Birebir aynı adlı ürün grubu yok |
| 294 | Kapatıcı | Birebir aynı adlı ürün grubu yok |
| 2956 | Blazer Ceket | Birebir aynı adlı ürün grubu yok |
| 297 | Kaş Makası | Birebir aynı adlı ürün grubu yok |
| 30 | Boyunluk | Birebir aynı adlı ürün grubu yok |
| 301 | Vücut Çorabı | Birebir aynı adlı ürün grubu yok |
| 303 | Makyaj Sabitleyici | Birebir aynı adlı ürün grubu yok |
| 304 | Makyaj Süngeri | Birebir aynı adlı ürün grubu yok |
| 31 | Eşofman Altı | Birebir aynı adlı ürün grubu yok |
| 312 | Oje | Birebir aynı adlı ürün grubu yok |
| 315 | Dudak Parlatıcı | Birebir aynı adlı ürün grubu yok |
| 319 | Pudra | Birebir aynı adlı ürün grubu yok |
| 32 | Triko Hırka | Birebir aynı adlı ürün grubu yok |
| 320 | Ruj | Birebir aynı adlı ürün grubu yok |
| 334 | Takma Kirpik | Birebir aynı adlı ürün grubu yok |
| 335 | Takma Tırnak | Birebir aynı adlı ürün grubu yok |
| 34 | Çorap | Birebir aynı adlı ürün grubu yok |
| 348 | Törpü | Birebir aynı adlı ürün grubu yok |
| 35 | Telefon Kılıfı | Birebir aynı adlı ürün grubu yok |
| 35014 | Vantilatör | Birebir aynı adlı ürün grubu yok |
| 361 | Desenli Şal | Birebir aynı adlı ürün grubu yok |
| 388 | Highlighter | Birebir aynı adlı ürün grubu yok |
| 3942 | Şarj Kablosu | Birebir aynı adlı ürün grubu yok |
| 40 | Pijama Altı | Birebir aynı adlı ürün grubu yok |
| 405 | Fantazi Çorap | Birebir aynı adlı ürün grubu yok |
| 410 | Tırnak Bakım Ürünleri | Birebir aynı adlı ürün grubu yok |
| 434 | Pareo | Birebir aynı adlı ürün grubu yok |
| 4455 | Bandana | Birebir aynı adlı ürün grubu yok |
| 4654 | Şapka | Birebir aynı adlı ürün grubu yok |
| 47 | Mouse | Birebir aynı adlı ürün grubu yok |
| 501 | Bel çantası | Birebir aynı adlı ürün grubu yok |
| 5230 | Şarj Aleti | Birebir aynı adlı ürün grubu yok |
| 5477 | İç Giyim Aksesuar | Birebir aynı adlı ürün grubu yok |
| 550 | Sabahlık | Birebir aynı adlı ürün grubu yok |
| 5562 | Triko Etek | Birebir aynı adlı ürün grubu yok |
| 5645 | Plaj Çantası | Birebir aynı adlı ürün grubu yok |
| 60 | Büstiyer | Birebir aynı adlı ürün grubu yok |
| 620 | Ortam Kokusu | Birebir aynı adlı ürün grubu yok |
| 64 | Parfüm | Birebir aynı adlı ürün grubu yok |
| 65 | Deodorant | Birebir aynı adlı ürün grubu yok |
| 650 | Futbol Ayakkabısı | Birebir aynı adlı ürün grubu yok |
| 66 | Roll On | Birebir aynı adlı ürün grubu yok |
| 70 | Omuz Şalı | Birebir aynı adlı ürün grubu yok |
| 708 | Saç Kurutma Makinesi | Birebir aynı adlı ürün grubu yok |
| 712 | Powerbank | Birebir aynı adlı ürün grubu yok |
| 721 | Usb Hoparlör | Birebir aynı adlı ürün grubu yok |
| 7452 | Makyaj Çantası | Birebir aynı adlı ürün grubu yok |
| 75 | İç Giyim Takım | Birebir aynı adlı ürün grubu yok |
| 756 | Kaş Kalemi | Birebir aynı adlı ürün grubu yok |
| 789 | mama önlüğü | Birebir aynı adlı ürün grubu yok |
| 78923 | Gecelik | Birebir aynı adlı ürün grubu yok |
| 7898 | Toka | Birebir aynı adlı ürün grubu yok |
| 82 | Triko Kazak | Birebir aynı adlı ürün grubu yok |
| 85 | Triko Tulum | Birebir aynı adlı ürün grubu yok |
| 89 | Cüzdan | Birebir aynı adlı ürün grubu yok |
| 960 | Dezenfektan | Birebir aynı adlı ürün grubu yok |
| 992 | Bikini | Birebir aynı adlı ürün grubu yok |
| 993 | Mayo | Birebir aynı adlı ürün grubu yok |
| 994 | Wax | Birebir aynı adlı ürün grubu yok |
| 996 | Saç Boya Fırçası | Birebir aynı adlı ürün grubu yok |
| 997 | Basic Spor Atlet | Birebir aynı adlı ürün grubu yok |
| 998 | Spor Büstiyer | Birebir aynı adlı ürün grubu yok |
| 999 | Fitness Tayt | Birebir aynı adlı ürün grubu yok |
| AG | Güneş Gözlüğü | Birebir aynı adlı ürün grubu yok |
| AH | Kol Saati | Birebir aynı adlı ürün grubu yok |
| AL | Pijama Takımı | Birebir aynı adlı ürün grubu yok |
| AM | Eşofman Takım | Birebir aynı adlı ürün grubu yok |
| AN | Fantazi Gecelik | Birebir aynı adlı ürün grubu yok |
| AP | Korse | Birebir aynı adlı ürün grubu yok |
| AR | Sütyen | Birebir aynı adlı ürün grubu yok |
| AT | Külot | Birebir aynı adlı ürün grubu yok |
| AU | Slip | Birebir aynı adlı ürün grubu yok |
| BA | Patik | Birebir aynı adlı ürün grubu yok |
| BC | Boxer | Birebir aynı adlı ürün grubu yok |
| gy13 | Anahtarlık | Birebir aynı adlı ürün grubu yok |
| PT | Pijama Tulum | Birebir aynı adlı ürün grubu yok |
| sbm | Saç Bakımı | Birebir aynı adlı ürün grubu yok |

## Kaydedilen kesin eşlemeler

| V3 kodu | V3 adı | Bizim ürün grubu |
| --- | --- | --- |
| 0016 | Kot Ceket | Kot Ceket |
| 021 | İç Giyim | İç Giyim |
| 04 | Bluz | Bluz |
| 05 | Yelek | Yelek |
| 06 | Gömlek | Gömlek |
| 07 | Etek | Etek |
| 09 | Pantolon | Pantolon |
| 1080 | Eşarp | Eşarp |
| 11 | Şort | Şort |
| 111 | Spor Ayakkabı | Spor Ayakkabı |
| 114 | Mont | Mont |
| 117 | Kapri | Kapri |
| 124 | Terlik | Terlik |
| 125 | Topuklu Ayakkabı | Topuklu Ayakkabı |
| 13 | Elbise | Elbise |
| 14 | Tunik | Tunik |
| 1528 | Makyaj Temizleyici | Makyaj Temizleyici |
| 16 | Hırka | Hırka |
| 17 | Ceket | Ceket |
| 18 | Trençkot | Trençkot |
| 26 | Sweatshirt | Sweatshirt |
| 288 | Saç Boyası | Saç Boyası |
| 29 | Tulum | Tulum |
| 2955 | Kaban | Kaban |
| 307 | Maskara | Maskara |
| 406 | Bone | Bone |
| 413 | Kulaklık | Kulaklık |
| 435 | Stiletto | Stiletto |
| 437 | Günlük Ayakkabı | Günlük Ayakkabı |
| 51 | T-Shirt | T-Shirt |
| 5623 | Panço | Panço |
| 590 | Kimono | Kimono |
| 61 | Hamile Giyim | Hamile Giyim |
| 63 | şal | Şal |
| 6687 | Klasik Ayakkabı | Klasik Ayakkabı |
| 83 | Babet | Babet |
| 84 | Sandalet | Sandalet |
| 86 | Bot | Bot |
| 87 | Çizme | Çizme |
| 90 | Kap | Kap |
| AZ | Body | Body |
| BB | Zıbın | Zıbın |
| BU | Atlet | Atlet |
| TKM | İkili Takım | İkili Takım |

## Worker sınırı

Mevcut V3 prosedürü bazı ürünlerde ham grup adına “Tesettür ” öneki ekliyor; okunan ürün satırında ayrı ham grup kodu yok.
Bu türetilmiş adlar ham 204 kodun yeni bir grubu gibi sözlüğe eklenmedi.
Panel tabanlı worker yolu henüz aktive edilmedi; bu kaynak ad/kod konusu ve kalan eşlemeler aktivasyondan önce doğrulanmalıdır.
Bu çalışma periyodik sözlük senkronizasyonu kurmaz; güvenlik kontrolleri olan tek seferlik, tekrar çalıştırılabilir import aracıdır.
