# Stok kartı grup eşleme etkisi — yazmasız kontrol

## Sonuç — onaylı uygulama tamamlandı

- API01, API02 ve ERP worker: `20260907T210000Z_panel_group_defaults` aktif,
  panel grup eşlemesi açık; startup migrate/seed ve push gönderimi kapalı.
- Önce gerçek ROLLBACK provası, ardından onaylı COMMIT: **13.433** ürün grubu
  güncellendi, **29.088** boş Ürün Grubu özelliği mevcut varsayılanla dolduruldu.
- İkinci Rehearse: **0 grup değişikliği / 0 özellik dolumu**, ROLLBACK.
- Son salt-okunur karşılaştırma: **29.088 kartta grup ve özellik aynı**.
  **57 Tozlu/00 + 4 V3 grup bağı olmayan kart** değiştirilmedi.
- Örnekler: 9İ60142.0001 → sutyen / İç Giyim;
  P-00000084 → kaban / Kaban; P-00000092 → esofman_alti / Eşofman.
- ERP ilk normal katalog ve fiyat turları OK; servisler active/running,
  NRestarts=0, log hata sayısı 0. İki API health/ana sayfa/örnek ürün 200.
  Aktif channel_scopes kaydı yok; yeniden materyalize edilecek kapsam çıkmadı.
- Bakım SQL'i fiyat/stok/görsel/açıklama/definition yazmadı. Ayrı açık onayla
  etkinleştirilen ERP worker normal katalog/fiyat/özellik işine devam ediyor.
  .59 ve MySQL'e yazı yok; yeni migration/seed/backup veya GitHub push yok.

## Uygulama öncesi tarihsel rapor

Kaynak: V3 Eldi_V3, prItemAttribute ItemTypeCode=1 / AttributeTypeCode=2.
Grup kodu V3'ün cdItemAttributeDesc TR sözlüğünden kendi SQL eşitliğiyle
kanonikleştirildi. Hedef: .241/ecommerce_db; API01 üzerinden loopback SSH
tüneli. PostgreSQL repeatable-read + READ ONLY; tüm sorgular SELECT.

| Sonuç | Kart |
|---|---:|
| Aktif kart | 29.149 |
| Tekil doğrudan ERP eşlemesi ve geçerli grup varsayılanı bulunan | 29.088 |
| Grubu değişecek | 13.433 |
| Grubu zaten doğru | 15.655 |
| Boş Ürün Grubu özelliği doldurulabilecek | 29.088 |
| 00 / Tozlu nedeniyle eşlenmeyen | 57 |
| V3 tip 2 grup bağı bulunmayan | 4 |

Bu sayılar uygulama sonucu değil, okuma anındaki aday envanteridir.
Aktif mevcut Ürün Grubu özelliğini ezmeyi gerektiren aday çıkmadı. Silinmiş
özellik geçmişi veya başka özellikler için bu rapordan sonuç çıkarılmaz.
Ürün grubu değişikliği kategori kapsamlarını etkileyebilir; uygulama sonrası
kanal kapsamı/cache ve ürün sayfası kabulü gerekir.

## Örnek değişimler

| Ürün | ERP kodu | Mevcut grup | Eşlenen grup |
|---|---|---|---|
| 9İ60142.0001 | AR | grp_118 | sutyen |
| P-00000084 | 2955 | grp_73 | kaban |
| P-00000089 | 18 | grp_73 | grp_262 |
| P-00000092 | 31 | grp_47 | esofman_alti |

V3 grup bağı olmayan dört kart: P-00012282, P-00013209, PRD-E657D084,
TEST-PANEL-001. Bunlar silinmez veya tahminle eşlenmez.
00/Tozlu örnekleri: P-00000507, P-00008538, P-00008539.

## Önemli doğrulama

İlk ham kod karşılaştırması 594 eşleşmeyen gösterdi. Bunların 537'si
tkm/TKM, ah/AH, al/AL, an/AN, am/AM, at/AT, ar/AR, bc/BC, bu/BU farkıydı.
Rastgele lower-case fallback yapılmadı: V3'ün sözlük JOIN'iyle gerçek kayıt
bağı doğrulandı. Nihai eşlenmeyen grup sayısı 57 karttır (yalnız 00).
Çakışan grup veya varsayılan değer görülmedi.

## Sınırlar ve sonraki adım

- Ürün/grup/özellik verisi yazılmadı; yeni tanım, migration veya seed yok.
- Bu araç yalnız doğrudan eşlemeleri değerlendirir; koşullu/havuz eşlemelerini
  tahmin etmez. Uygulamadan önce verinin yeniden okunması gerekir.
- Eski worker ayarlarıyla bu kartları güncellemek geri ezilme riski taşır.
  Kalıcı panel eşleme akışı ve yeni kart grup varsayılanı davranışı birlikte
  güvenceye alınmadan toplu yazım yapılmamalı.
- Derleme 0 hata/0 uyarı; gerçek salt-okunur çalıştırma başarılı; tünel kapandı.
- V3 sürücüsü TLS 1.0 uyarısı verdi. Bu işte sunucu TLS ayarı değiştirilmedi;
  altyapıda ayrıca ele alınmalı. Bağlantı bilgileri rapora kaydedilmedi.
