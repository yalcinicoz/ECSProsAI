# .59 ürün grupları / ERP eşleme karşılaştırması — 2026-09-07

## Tamamlanan referans hizalaması — 16:14 UTC

Kullanıcı `.59`'u gruplar, özellik tipleri/değerleri, şablonlar ve ERP eşlemelerinde
otorite kabul etti; hedefteki farklılıkların değiştirilmesini onayladı.
Kaynak `.59` yalnız read-only okundu; yazılar `.241 / ecommerce_db` üzerinde yapıldı.

| İşlem | Sonuç |
|---|---:|
| Yeni ürün grubu | 157 |
| Güncellenen mevcut grup | 1 |
| Eklenen/değişen özellik tipi | 0 / 0 (zaten aynı) |
| Yeni özellik değeri | 172 |
| Referansa göre güncellenen değer | 4.319 |
| Yeni/güncellenen grup özellik bağlantısı | 1.770 / 1 |
| Yeni eksen alt özellik bağlantısı | 138 |
| Güncellenen ERP sözlük satırı | 10 |
| Yeni/güncellenen ERP eşleme satırı | 156 / 49 |

- Son durumda 304 aktif grup var: kaynakta bulunan 303 grup ve korunan yerel
  `abiye_elbise_2`. Yerel fazla grup/ürün bağlantıları silinmedi.
- Kaynaktaki **203 doğrudan ERP kodunun tamamı aynı grup koduna eşli; fark 0**.
  Abiye artık `abiye`, Vantilatör `vantilator` hedefinde. Önceki eşlemeler/havuzlar
  referansa göre güncellendi veya soft-delete edildi; kalıcı silme yapılmadı.
- `00 / Tozlu`, kaynakta olduğu gibi eşlenmemiş bırakıldı.
- Mevcut grup/tip/değer ID'leri korundu; ürün kartı, varyant ve stok tablosuna
  yazılmadı. Değerlerin ad/sıra/renk bilgileri referansa göre güncellendiğinden
  bunları gösteren ekranların metinleri değişebilir. Kaynakta boş olan ExtraData
  hedefin ERP metadata'sını silmedi.
- İlk provada 4 belirsiz ad ve 1 çakışan kaynak değer kimliği değiştirilmeden
  ayrıldı. Son ad normalizasyonu sonrası 12 renk adındaki 13 kaynak satırının hedef
  UUID karşılığı tekilleştirilemiyor; mevcut kayıtlar rastgele birleştirilmedi:
  Açık Kahve, Beyaz Beyaz, Beyaz Bordo, Beyaz Mavi, Beyaz Siyah, Gri Siyahlı,
  Gülkurusu, Koyu Lacivert, Koyu Vizon, Nar Çiçeği, Saksmavisi, Siyah Beyaz (2).
  Bu adlar hedefte var; kalan konu kimlik tekilleştirmesi. Grup şablonlarının
  varsayılan değerlerinde eksik/belirsiz bağlantı yok.
- Rollback provası, COMMIT sonrası kaynak-hedef SELECT karşılaştırması ve tekrar
  prova geçti. İkinci provada tüm INSERT/UPDATE sayaçları **0**.
- API01 geri dönüş yedeği (geçici transfer değil):
  `/opt/ECSProsAI/backups/reference-definitions-20260907T161430Z.dump`
  (500.966 bayt, izin 0600, pg_restore liste kontrolü başarılı).
  SHA-256: `8b791da4343889160bf84df2000cea957fecf9fddcbff6c0d146e8008966c014`.
  Yalnız ilgili yedi referans/eşleme tablosunu içerir; kör tam restore yapılmamalı.
- Scriptler: `tools/veri-bakim/align-reference-definitions.ps1` ve `.sql`.
  Varsayılan mod Rehearse/ROLLBACK; Apply öncesi hedef kimliği ve yedek zorunlu.
  Kaynak sayısı değişirse, veri tipi uyumsuzsa veya şablon bağı çözülmezse durur.
- Sunucu yayını/restart, worker aktivasyonu, migration ve GitHub push yapılmadı.

## Önceki inceleme — tarihsel kayıt

Kaynak .59 PostgreSQL yalnız okundu; hedef .241 ecommerce_db kimliği doğrulandı.
Kaynak 303 aktif ürün grubu, hedef 147. Kaynakta 203 aktif doğrudan ERP grup eşlemesi.
Kaynak grup kodlarından 157'si hedefte yok; bu kod sayısıdır, ad benzerliğiyle yeni grup kararı verilmedi.
ERP sözlüğünde kaynak aktif grup kodları açısından eksik yok.
45 eşleme aynı grup kodunda zaten mevcut. İki eksik eşleme eklendi ve yeniden okundu:

| ERP kodu | Hedef grup | Yeni eşleme ID |
|---|---|---|
| 279 | tlm_fondoten / Fondöten | 1e371c77-434f-435a-8784-e7eb91ed1760 |
| 60 | grp_9 / Bustiyer | d1721139-1293-43a3-b43d-739aa48a07fb |

SQL prova ROLLBACK, ardından COMMIT, ardından SELECT doğrulaması geçti.
İkinci çalıştırma mevcut eşlemeye rastlarsa fail-closed durur; tekrar ekleme/üzerine yazma yok.
Ürün grupları, özellik tipleri/değerleri, ürün kartları, servisler ve kaynak DB değiştirilmedi.
Karşılaştırmada yalnız grup kodu/ERP kodu kullanıldı; kaynak UUID'ler hedefe doğrudan taşınmadı.

## Korunan iki farklı hedef

| ERP | .59 hedefi | Bizdeki hedef |
|---|---|---|
| 21 | abiye / Abiye | abiye_elbise_2 |
| 35014 | vantilator / Vantilatör | grp_179 |

Mevcut eşlemeler ezilmedi. Kullanıcı kararı gerekli.

## Kaynakta eşlenmiş, hedef grup kodu eksik 154 kayıt

Bunların uygulanması yeni grup tanımları ve özellik/varsayılan değer/eksen şablonları için ayrıca kapsam onayı ve karşılaştırma gerektirir.
Şablonlar henüz bu işlem için birebir doğrulanmadı; yalnız grupları boş olarak kopyalamak önerilmez.
Ürün kartı geri doldurma ve worker aktivasyonu bu kapsamda yapılmadı.

| ERP kodu | Kaynak grup kodu | Grup adı |
|---|---|---|
| 1250 | agda | Ağda |
| 1525 | agda_bezi | Ağda Bezi |
| 1531 | agda_isitici | Ağda Isıtıcı |
| 1553 | agda_temizleme_yagi | Ağda Temizleme Yağı |
| 011 | alisveris_cantasi | Alışveriş Çantası |
| gy13 | anahtarlik | Anahtarlık |
| 245 | aplikator | Aplikatör |
| 150 | aseton | Aseton |
| 1007 | babet_corabi | Babet Çorabı |
| 4455 | bandana | Bandana |
| 1009 | banyo_urunleri | Banyo Ürünleri |
| 02 | basic_body | Basic Body |
| 997 | basic_spor_atlet | Basic Spor Atlet |
| 1423 | bebek_battaniye | Bebek Battaniye |
| 501 | bel_cantasi | Bel Çantası |
| 25 | bere | Bere |
| 992 | bikini | Bikini |
| 1223 | bileklik | Bileklik |
| 2956 | blazer_ceket | Blazer Ceket |
| BC | boxer | Boxer |
| 30 | boyunluk | Boyunluk |
| 260 | cimbiz | Cımbız |
| 233 | cilt_bakimi | Cilt Bakımı |
| 1000 | cilt_maskesi | Cilt Maskesi |
| 89 | cuzdan | Cüzdan |
| 130 | capraz_canta | Çapraz Çanta |
| 1005 | citcitli_atlet | Çıtçıtlı Atlet |
| 34 | corap | Çorap |
| 1978 | deniz_sort | Deniz Şort |
| 65 | deodorant | Deodorant |
| 119 | deri_ceket | Deri Ceket |
| 361 | desenli_sal | Desenli Şal |
| 960 | dezenfektan | Dezenfektan |
| 261 | dipliner | Dipliner |
| 1510 | dis_ipi | Diş İpi |
| 265 | dudak_kalemi | Dudak Kalemi |
| 1545 | dudak_makyaji | Dudak Makyajı |
| 315 | dudak_parlatici | Dudak Parlatıcı |
| 1032 | duz_sandalet | Düz Sandalet |
| 268 | epilasyon_yayi | Epilasyon Yayı |
| AM | esofman_takim | Eşofman Takım |
| 105 | ev_botu | Ev Botu |
| 20 | ev_terligi | Ev Terliği |
| 275 | eyeliner | Eyeliner |
| 405 | fantazi_corap | Fantazi Çorap |
| AN | fantazi_gecelik | Fantazi Gecelik |
| 1015 | fantazi_takim | Fantazi Takım |
| 278 | firca | Fırça |
| 999 | fitness_tayt | Fitness Tayt |
| 650 | futbol_ayakkabisi | Futbol Ayakkabısı |
| 78923 | gecelik | Gecelik |
| 1655 | goz_fari | Göz Farı |
| 280 | goz_kalemi | Göz Kalemi |
| AG | gunes_gozlugu | Güneş Gözlüğü |
| 133 | gunluk_canta | Günlük Çanta |
| 1040 | gunluk_portfoy_canta | Günlük Portföy Çanta |
| 1006 | hastahane_cikisi | Hastahane Çıkışı |
| 388 | highlighter | Highlighter |
| 5477 | ic_giyim_aksesuar | İç Giyim Aksesuar |
| 75 | ic_giyim_takim | İç Giyim Takım |
| 1053 | iclik | İçlik |
| 1002 | jartiyer | Jartiyer |
| 10 | jean_pantolon | Jean Pantolon |
| 1540 | kalem_tiras | Kalem Tıraş |
| 294 | kapatici | Kapatıcı |
| 276 | kas_fari | Kaş Farı |
| 756 | kas_kalemi | Kaş Kalemi |
| 297 | kas_makasi | Kaş Makası |
| 23 | kemer | Kemer |
| AH | kol_saati | Kol Saati |
| 002 | kolonya | Kolonya |
| 123 | kolye | Kolye |
| AP | korse | Korse |
| 054 | koruyucu_onluk | Koruyucu Önlük |
| 0017 | kot_gomlek | Kot Gömlek |
| AT | kulot | Külot |
| 1052 | kulotlu_corap | Külotlu Çorap |
| 1234 | kupe | Küpe |
| 7452 | makyaj_cantasi | Makyaj Çantası |
| 1550 | makyaj_fircasi | Makyaj Fırçası |
| 1551 | makyaj_pamugu | Makyaj Pamuğu |
| 303 | makyaj_sabitleyici | Makyaj Sabitleyici |
| 304 | makyaj_sungeri | Makyaj Süngeri |
| 789 | mama_onlugu | Mama Önlüğü |
| 1529 | manikur_pens | Manikür & Pens |
| 993 | mayo | Mayo |
| 2004 | mevsimlik_ceket | Mevsimlik Ceket |
| 1224 | mevsimlik_mont | Mevsimlik Mont |
| 47 | mouse | Mouse |
| 312 | oje | Oje |
| 70 | omuz_sali | Omuz Şalı |
| 201 | organizer | Organizer |
| 620 | ortam_kokusu | Ortam Kokusu |
| 1012 | pantolon_corabi | Pantolon Çorabı |
| 434 | pareo | Pareo |
| 64 | parfum | Parfüm |
| BA | patik | Patik |
| 113 | penye_pantolon | Penye Pantolon |
| 40 | pijama_alti | Pijama Altı |
| AL | pijama_takimi | Pijama Takımı |
| PT | pijama_tulum | Pijama Tulum |
| 5645 | plaj_cantasi | Plaj Çantası |
| 712 | powerbank | Powerbank |
| 319 | pudra | Pudra |
| 66 | roll_on | Roll On |
| 320 | ruj | Ruj |
| 550 | sabahlik | Sabahlık |
| sbm | sac_bakimi | Saç Bakımı |
| 996 | sac_boya_fircasi | Saç Boya Fırçası |
| 708 | sac_kurutma_makinesi | Saç Kurutma Makinesi |
| 27 | salopet | Salopet |
| 131 | sirt_cantasi | Sırt Çantası |
| AU | slip | Slip |
| 1013 | soket_corap | Soket Çorap |
| 998 | spor_bustiyer | Spor Büstiyer |
| 4654 | sapka | Şapka |
| 5230 | sarj_aleti | Şarj Aleti |
| 3942 | sarj_kablosu | Şarj Kablosu |
| 036 | semsiye | Şemsiye |
| 1111 | sisme_mont | Şişme Mont |
| 2400 | sisme_yelek | Şişme Yelek |
| 145 | sortlu_takim | Şortlu Takım |
| 334 | takma_kirpik | Takma Kirpik |
| 335 | takma_tirnak | Takma Tırnak |
| 12 | tayt | Tayt |
| 35 | telefon_kilifi | Telefon Kılıfı |
| 410 | tirnak_bakim_urunleri | Tırnak Bakım Ürünleri |
| 7898 | toka | Toka |
| 1025 | topuklu_bot | Topuklu Bot |
| 1028 | topuklu_cizme | Topuklu Çizme |
| 122 | topuklu_sandalet | Topuklu Sandalet |
| 1023 | topuklu_terlik | Topuklu Terlik |
| 348 | torpu | Törpü |
| 069 | trekking_ayakkabi | Trekking Ayakkabı |
| 1502 | tuy_dokucu_krem | Tüy Dökücü Krem |
| 721 | usb_hoparlor | Usb Hoparlör |
| 301 | vucut_corabi | Vücut Çorabı |
| 994 | wax | Wax |
| 147 | yuz_siperi | Yüz Siperi |
| 31 | esofman_alti | Eşofman Altı |
| AR | sutyen | Sütyen |
| 15 | triko_bluz | Triko Bluz |
| 101 | triko_ceket | Triko Ceket |
| 108 | triko_elbise | Triko Elbise |
| 5562 | triko_etek | Triko Etek |
| 32 | triko_hirka | Triko Hırka |
| 82 | triko_kazak | Triko Kazak |
| 1972 | triko_panco | Triko Panço |
| 200 | triko_pantolon | Triko Pantolon |
| 109 | triko_suveter | Triko Süveter |
| 199 | triko_takim | Triko Takım |
| 85 | triko_tulum | Triko Tulum |
| 28 | triko_tunik | Triko Tunik |
| 107 | triko_yelek | Triko Yelek |
