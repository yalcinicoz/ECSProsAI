# Kargo Entegrasyonu Planı — Gerçek Taşıyıcı API Bağlantısı

> Yazım: 2026-07-29 · Güncelleme: 2026-07-29 (kaynak inceleme + kullanıcı kararları işlendi)
> Durum: **KG1 BAŞLIYOR — PTT + DHL/MNG hazır** (2026-07-29: PTT kimlik+barkod aralığı
> [278358735860–278358799999] ✓, DHL kimlik+müşteri no+legacy kod+enum tabloları ✓;
> enum kaynağı `docs/APIDocs/DHLMNGEnums.txt` — smsPreference 0-3, deliveryType 1=adres/
> 2=şube, paymentType 1=gönderici/2=alıcı/3=platform, kapıda tahsilat isCOD+codAmount ile.
> ⚠️ DHL API'de kapıda tahsilat TİPİ [nakit/kart] alanı görünmüyor — sözleşme düzeyinde
> olabilir, KG1 testinde netleşecek. Eksikler: DHL cancelOrder+Standard Query sayfaları;
> PTT kimliklerinin test ortamı geçerliliği; Sürat beyaz liste; HepsiJet resmi doküman)
> Kapsam: aktif kullanılan 4 taşıyıcı — **PTT Kargo, HepsiJet, DHL (eski MNG — kullanıcı
> kararı 2026-07-29: MNG yerine DHL markasıyla anılacak, API'si MNG APIZone), Sürat Kargo**
> İlk aşama hedefi: paket bilgisi gönderme, güncelleme/iptal (taşıyıcı değişimi) ve takip.

---

## 1. Kullanıcının Tarif Ettiği Süreç → Faz Eşlemesi

| # | Süreç adımı | Durum | Faz |
|---|-------------|-------|-----|
| 1 | Sipariş anında adrese uygun kargo seçimi | ✅ VAR (Kargo Bölgeleri + checkout tercihi `RequestedCargoIntegrationId`) | — |
| 2 | Faturası hazır paketin API ile taşıyıcıya otomatik gönderimi | YOK | **KG1** |
| 3 | Taşıyıcı değişikliği: eskiden sil (iptal), yeniye gönder | YOK | **KG1** (motor) + **KG2** (panel butonu) |
| 4 | Panelden paket izleme + fiziki teslim takibi + gecikme uyarısı | KISMİ (paket listesi var, taşıyıcı verisi yok) | **KG2** |
| 5 | Müşteriye SMS/e-posta bilgilendirme + şablon tasarımı | ALTYAPI VAR (GES SMS + SMTP), şablon yönetimi YOK | **KG3** |
| 6 | Müşterinin site üzerinden kargo takibi | KISMİ (takip URL linki var, canlı olay akışı yok) | **KG4** |
| 7 | Ana sayfa "Kargo Takibi": üyeye direkt, üyeliksize SMS OTP ile | KISMİ (`/uyeliksiz-kargo-takip` DEMO sayfası var) | **KG4** |

Sonraki genişlemeler (bu planın DIŞINDA, talep gelince): taşıyıcıyla mali mutabakat,
bölgesel hizmet kalitesi skorları ve personel yönlendirme.

## 2. Mevcut Durum (2026-07-29 tespiti)

- `ICargoAdapter` arayüzü (CreateShipment/Track/Cancel) + `AdapterResolver` var;
  tek adapter Yurtiçi ve STUB (sahte takip no). 4 hedef taşıyıcının adapter'ı yok.
- "Kargoya Ver" akışı taşıyıcıya HİÇBİR ŞEY göndermiyor — yalnız yerel kargo kod motoru
  (`ICargoCodeService`; PTT=range tahsisli barkod, diğerleri free/pattern) çalışıyor.
- Servis şemaları (SettingsSchema) 4 taşıyıcı için dolduruldu (2026-07-29) — kimlikler
  şifreli Credentials'ta; adapter'lar bu anahtarları BİREBİR okuyacak, taşıyıcı dokümanı
  ile çelişen alan olursa şema o fazda düzeltilir (seed dolu şemayı ezmez).
- SMS: `GesTelekomSmsService` ÇALIŞIYOR (OTP+bilgilendirme, ayarlar DB'den).
  E-posta: `IEmailService`/`SmtpEmailService` ÇALIŞIYOR (DB→config→log).
  Bildirim ŞABLON yönetimi yok — KG3'te kurulacak.

## 3. Mimari Kararlar (kurgu onayına sunulan)

1. **Gönderim kaydı**: paket başına `cargo_shipments` benzeri durum kaydı
   (Integration modülü): paket, taşıyıcı sözleşmesi (FirmPlatformIntegration),
   bizim kargo kodumuz, taşıyıcı takip no, durum makinesi
   `pending → sent → accepted (fiziki teslim) → in_transit → delivered`
   + `cancelled` (taşıyıcı değişiminde) + `failed` (gönderim hatası).
   Taşıyıcı değişiminde eski kayıt `cancelled` kalır, yeni kayıt açılır — geçmiş korunur;
   kargo kodu F1-F5 kuralı gereği havuza geri dönmez, yeni taşıyıcı stratejisine göre
   yeni kod üretilir.
2. **Tetik (kullanıcı kararı 2026-07-29)**: sipariş ONAYLANDIĞINDA paket bilgisi
   taşıyıcıya otomatik gönderilir (pazaryeri mantığı; ayrı onay adımı YOK — fatura
   kesim zamanı esnek kalır). Küçük/temkinli firmalar için opsiyonel "gönderim onayı"
   bayrağı config'te tutulur, varsayılan KAPALI. Panelde elle "Taşıyıcıya Gönder"
   butonu yeniden deneme/fallback olarak kalır. Otomatik gönderim arka plan
   worker'ıyla (pazaryeri `MarketplaceBatchWorker` kalıbı): kuyruk, backoff'lu yeniden
   deneme, kalıcı hatada sorun kaydı — canlı akışı bloke etmez. Not: onay anında paket
   henüz toplanmadıysa kayıt alıcı+barkod bilgisiyle açılır; paketleme aşamasında
   bölünme/ek paket olursa ek kayıtlar o anda gönderilir.
3. **Takip verisi + fiziki teslim kontrolü (kullanıcı kararı 2026-07-29)**: worker
   taşıyıcıdan periyodik takip sorgusu çeker (accepted/delivered olayları), olaylar
   gönderim kaydına işlenir. Fiziki teslim izleme saati, paketin SON KONTROL aşaması
   tamamlandığında (son ürün pakete sorunsuz eklendiğinde) başlar. Her gün **21:00'da**
   kontrol koşusu: gün içinde son kontrolü tamamlanıp taşıyıcıya bildirilen paketlerden
   taşıyıcı sisteminde hâlâ fiziki kabul (accepted) görünmeyenler sorun kuyruğuna düşer
   + panelde sayaçlı görünür (pazaryeri issues kalıbı). Kural: gün biterken kabul
   görünmüyorsa sıkıntı var demektir.
4. **Bildirimler (KG3)**: `notification_templates` — olay tipi (kargoya verildi /
   taşıyıcı teslim aldı / dağıtımda / teslim edildi), kanal (sms|email), i18n gövde +
   yer tutucular (`{ad} {siparisNo} {takipNo} {takipLink} {tasiyici}`); admin'de şablon
   düzenleme ekranı; gönderim kargo olaylarından tetiklenir, üye iletişim tercihi sayılır.
5. **Site (KG4)**: üye "Kargo Takibi" → aktif siparişlerin gönderi durumları (bizim DB'deki
   olay akışı; taşıyıcıya canlı sorgu değil — hız + taşıyıcı limiti). Üyeliksiz akış:
   telefon → GES SMS OTP → doğrulanan telefona ait aktif sipariş gönderileri.
   Mevcut `/uyeliksiz-kargo-takip` demo sayfası gerçeğe bağlanır.
6. **Adapter sınırı**: taşıyıcıya özel her şey (auth, payload, hata çözümü) adapter'da;
   Order/Fulfillment tarafı yalnız gönderim kaydı + olaylarla konuşur. Adapter'lar
   taşıyıcının RESMİ dokümanına göre yazılır — doküman gelmeden kod yazılmaz
   (2026-07-29 dersi: parametreler tahminle kesinleştirilmez).
7. **Tahsilat boyutu (2026-07-29 kullanıcı gereksinimi)**: kargo seçimi bölge × ödeme
   şekli matrisidir. Üç ödeme kapsamı: `nonCod` (tahsilatsız), `codCash` (kapıda nakit),
   `codCard` (kapıda kart).
   - **Sözleşme kapsamı**: aynı firmada aynı taşıyıcıya BİRDEN ÇOK sözleşme açılabilir
     (model zaten izinli — örn. Sürat tahsilatsız + Sürat tahsilatlı ayrı cari). Her
     sözleşmeye kapsam işaretleri eklenir (settings: nonCod/codCash/codCard boolean;
     şemaya alan olarak girer). Bir sözleşme yalnız tahsilatlı ya da yalnız tahsilatsız
     kargoları kapsayabilir; tahsilatlıda yalnız nakit ya da yalnız kart kabul edebilir.
   - **Bölge kuralları**: CargoRule'a ödeme kapsamı boyutu eklenir — bölge atamaları
     kapsam bazında ayrı öncelik listeleri tutabilir (örn. X mahallesinde tahsilatlılar
     A kargoya, tahsilatsızlar B kargoya). Kargo Bölgeleri ekranı kapsam sekmeleri/
     seçicisiyle genişler (ekran kurgusu K16 gereği ayrıca konuşulur).
   - **Seçim motoru**: sipariş ödeme şekli netleştiğinde (mahalle, ödemeKapsamı) →
     kapsamı destekleyen aktif sözleşmeler → bölge önceliği → sözleşme seçilir; müşteri
     tarafında taşıyıcı MARKA olarak tek görünür, sözleşme ayrımı gizlidir.
8. **Gönderi başına ek hizmet seçenekleri (2026-07-29 incelemesi)**: taşıyıcılar paket
   bildirirken sözleşmeye bağlı seçimlik hizmet kodları/bayrakları bekliyor. Model:
   sözleşme (FirmPlatformIntegration.Settings) VARSAYILANLARI tutar (örn. SMS
   bilgilendirme açık, sigorta kapalı, teslim şekli adrese), gönderim kaydı paket
   bazında OVERRIDE edebilir; taşıyıcı kodlarına çeviriyi adapter yapar.
   Doküman tespitleri:
   - **PTT**: `aliciSms` (kabul/teslim SMS'i — anlaşmada varsa ZORUNLU), gönderici
     SMS/telefon (biri zorunlu), `ekhizmet` bileşik kod alanı (örn. DK=değerli/sigortalı
     [sigorta bedeli alanıyla], UA=ücreti alıcıdan, OS=ödeme şartlı/kapıda tahsilat
     [tutar alanıyla]) — sözleşmedeki geçerli liste `PttBilgi.ekHizmetSorgula`
     servisinden DİNAMİK çekilebilir (dokümandaki genel kimlikle); `OdemeSekli`
     (MH/N/UA); iade adresi override alanları (`iadeA*` — teslim edilemezse farklı
     adrese iade).
   - **Sürat**: `EkHizmetler` kod listesi (değerler sözleşmeye bağlı — Sürat'tan liste
     istenecek); `TeslimSekli` (adrese/şubede/PUDO/hızlı/bugün teslim), `TasimaSekli`
     (Ekonomi/Ekspres), `GonderiSekli` (Standart/Pudo/BuKoli/EasyPoint/Hepsimat vb.
     teslim noktası ağları), `KapidanOdemeTahsilatTipi` (Nakit/TekÇekim/Taksitli —
     tahsilat-tipi gereksinimiyle birebir), SMS bayrakları (`SmsGonderme`,
     `SonrakiGunSmsGitsin`, `UcretsizSmsKullansin`, `GonderenSmsUnvani`), `AlimSaati`,
     `SevkAdresi`.
   - **HepsiJet** (topluluk kaynaklı, resmi dokümanla teyit edilecek): `productCode`
     (HX_STD standart; slot/hızlı teslimat ürünleri), `deliveryType` (RETAIL/
     MARKETPLACE), `deliverySlot` (zaman aralıklı teslimat).
   - **DHL(MNG)**: şemalar APIZone hesabı gerektiriyor — SMS/teslimat tipi alanları
     muhtemel, doküman gelmeden kesinleştirilmeyecek.
   - **Checkout sırası (KARAR 2026-07-29)**: kargo seçimi teslimat adımında, ödeme
     yöntemi SONRAKİ adımda. Teslimat adımında her kargo satırında desteklediği ödeme
     seçenekleri BİLGİ olarak gösterilir (örn. "Kapıda ödeme: nakit + kart" /
     "Kapıda ödeme yok"). Ödeme adımında çelişki çıkarsa (seçilen kargo seçilen ödeme
     şeklini desteklemiyor) müşterinin ÖDEME tercihi esas alınır: motor bölge
     önceliğine göre uygun kargoya otomatik geçer, teslimat özetinde kısa bilgi notu
     gösterilir — müşteri akıştan koparılmaz.

## 4. Fazlar

- **KG1 — Gönderim motoru + 4 gerçek adapter** (backend): gönderim kaydı/durum modeli,
  otomatik gönderim worker'ı, iptal + yeniden gönderim (taşıyıcı değişimi) komutları,
  PTT/HepsiJet/MNG/Sürat adapter'ları taşıyıcı test ortamlarında doğrulanır.
  Çıkış kriteri: test ortamında 4 taşıyıcıda gönderi aç → takip no al → iptal et döngüsü.
- **KG2 — Panel izleme** (admin): paket/gönderi izleme ekranı (durum, taşıyıcı, takip no,
  olay geçmişi), "Taşıyıcı Değiştir" (iptal+yeni gönderim) ve "Taşıyıcıya Gönder" aksiyonları,
  fiziki-teslim gecikme uyarı kuyruğu. (Ekran kurgusu K16 gereği ayrıca konuşulur.)
- **KG3 — Bildirim şablonları** (admin + backend): şablon tablosu + admin editörü +
  kargo olaylarına bağlı SMS/e-posta gönderimi.
- **KG4 — Site kargo takibi** (web): üye takip ekranı, ana sayfa "Kargo Takibi" linki,
  üyeliksiz SMS OTP akışı.

Sıra: KG1 → KG2 → KG3 → KG4. K19 gereği her faz kendi alanının oturumunda yürür
(KG1 ortak çekirdek/backend, KG2-KG3 admin panel, KG4 web sitesi).

## 5. Taşıyıcı Kaynak Durumu (2026-07-29 incelemesi)

| Taşıyıcı | Doküman | Sunucu erişimi (51.178.208.59'dan) | Eksik |
|----------|---------|-------------------------------------|-------|
| **PTT** | ✅ `docs/APIDocs/PTTKArgoAPIDocs/` incelendi — SOAP: yükleme `PttVeriYukleme(Test)/services/Sorgu` (`kabulEkle2`: musteriId+dosyaAdi+kullanici="PttWs"+sifre + alıcı döngüsü, `barkodNo` tahsisli 12 hane + check-digit [algoritma dokümanda]), silme `referansVeriSil`/`barkodVeriSil` (YALNIZ fiziki kabul öncesi — taşıyıcı değişimi buna oturur), takip `GonderiHareketV2` (`gonderiHareketBarkodSorgu` → kabul tarihi/merkezi = fiziki teslim sinyali) + `GonderiTakipV2`; il/ilçe kodları `PttBilgi` servisi | ✅ pttws.ptt.gov.tr TEST WSDL 200 | musteriId + şifre + tahsisli barkod aralığı (test+prod) |
| **DHL (MNG)** | ✅ Eski projenin ÇALIŞAN kodu alındı (`docs/mng-legacy-entegrasyon-ornegi.cs`, 2026-07-29): token (`POST /mngapi/api/token`, X-IBM-Client-Id/Secret + customerNumber/password/identityType → jwt) + createOrder (`POST /mngapi/api/standardcmdapi/createOrder` — isCOD/codAmount, smsPreference1-3, paymentType, deliveryType, orderPieceList desi, alıcı; barcode BİZİM kod, MNG kendi gönderi no üretir) + eski SOAP takip (KargoTakipByReferans — REST Query'ye geçilecek). Kimlikler + müşteri no GİRİLDİ (2026-07-29) | ✅ api.mngkargo.com.tr yanıt veriyor | APIZone'dan: cancelOrder + Standard Query uçları + enum değer tabloları (paymentType/smsPreference — kapıda NAKİT vs KART ayrımı); sandbox anahtarı (varsa) |
| **Sürat** | ✅ WSDL alındı ve haritalandı (`docs/APIDocs/SuratWSDL.xml`, 2026-07-29) — SOAP: gönderim `GonderiyiKargoyaGonderYeni` (KullaniciAdi+Sifre+GonderiModel; `OzelKargoTakipNo` BİZİM kodumuz → kod motoru free stratejisi, ReferansNo, desi/kg/adet, kapıda ödeme alanları), güncelleme `KargoBarkoduSiparisGuncelle` (CariKodu+WebPassword), silme `GonderiSil` (CariKodu+WebPassword+ozelKargoTakipNo), geri çekme `GonderiGeriCek` (iptal nedeni ile), takip `KargoTakipHareketDetayi`/`KargomNeredeDetay`/`BarkoddanGelenKargoDetayi`. Şema WSDL'e göre düzeltildi: +cariKodu +webPassword (2026-07-29 canlı DB) | ❌ **webservices.suratkargo.com.tr bu sunucudan hâlâ ERİŞİLEMİYOR** (muhtemel yurt dışı IP engeli; OVH Fransa) — adapter yazılabilir ama test/canlı çağrı için beyaz liste ŞART | Sürat'tan sunucu IP'sinin (51.178.208.59) beyaz listeye alınması + cari kodu/kullanıcı adı/şifreler |
| **HepsiJet** | 🟡 Yüzey haritalandı (2026-07-29, resmi portal captcha'lı — açık kaynak entegrasyonlar + arama üzerinden): Basic auth (kullanıcı/şifre) → `POST /auth/getToken` → token'lı çağrılar; gönderi `POST /rest/advance/sendDeliveryAdvance/v2` (orderId + alıcı + desi + gönderici firma; deliveryType=RETAIL, productCode=HX_STD) ve `POST /rest/delivery/sendDeliveryOrderEnhanced` (`customerDeliveryNo` BİZİM numaramız); silme `/rest/delivery/deleteDeliveryOrder/{customerDeliveryNo}` (deleteReason ile) — taşıyıcı değişimine uygun; takip `/rest/deliveryTransaction/getDeliveryTracking(V2)`; il/ilçe eşleme için settlement servisleri. Test ortamı: `integration-apitest.hepsijet.com` | ✅ integration.hepsijet.com yanıt veriyor | Resmi dokümanın portaldan indirilip `docs/APIDocs/HepsijetAPIDocs/`e konması (topluluk kaynağı doğrulanmalı) + test hesabı (kullanıcı/şifre) + gönderici firma kodları (companyAbbreviationCode/addressId/dock) |

**KG1 başlama sırası (kaynak hazırlığına göre): PTT → DHL(MNG) → HepsiJet → Sürat.**

## 6. Kullanıcıdan Beklenenler (güncel)

1. **PTT**: musteriId + şifre + tahsisli barkod aralığı (test ve prod için).
2. **DHL (MNG)**: APIZone portal hesabı açılması (Client ID/Secret), müşteri numarası,
   sandbox anahtarları.
3. **Sürat**: sunucu IP'si `51.178.208.59` için beyaz liste talebi (WSDL ✅ alındı);
   cari kodu + kullanıcı adı + şifre/web şifresi.
4. **HepsiJet**: resmi doküman dışa aktarımı (portal sunucudan captcha'lı —
   tarayıcıdan PDF/HTML kaydedilip `docs/APIDocs/HepsijetAPIDocs/`e konmalı) +
   test hesabı (Basic auth kullanıcı/şifre) + gönderici firma kodları
   (companyAbbreviationCode, companyAddressId, currentDockAbbreviationCode).
5. **Ortak**: gönderici bilgileri — çıkış adresi/şube kodları, sözleşme/ödeme tipi.
6. **Ek hizmet listeleri**: Sürat sözleşmenizde geçerli `EkHizmetler` kod listesi
   (müşteri temsilcisinden); PTT için gerek yok (ekHizmetSorgula'dan çekilecek);
   HepsiJet ürün kodları (productCode listesi) resmi dokümanla birlikte.

## 7. Alınan Kararlar (2026-07-29, kullanıcı)

- Fiziki teslim kontrolü: günlük **21:00** koşusu; gün içinde bildirilen paket gün
  bitmeden taşıyıcıda fiziki kabul görünmeli, görünmüyorsa personel uyarılır.
- İzleme saati paketin **son kontrol** (son ürün pakete eklendi) anından başlar.
- Otomatik gönderim tetiği: **sipariş onaylandığında** (pazaryeri mantığı; ayrı onay
  adımı yok, fatura zamanlaması esnek). Opsiyonel onay adımı config bayrağı olarak
  küçük firmalara sunulabilir, varsayılan kapalı.
- MNG yerine **DHL** markası kullanılacak (API aynı — MNG APIZone); servis kodu `mng`
  kalır (mevcut sözleşme/kural kayıtları kırılmaz), görünen ad "DHL Kargo (MNG)" yapılır.
- **Tahsilat çelişkisi çözümü**: müşterinin ödeme tercihi esas — kargo bölge önceliğine
  göre otomatik değişir + bilgi notu; teslimat adımında kargo listesinde her taşıyıcının
  desteklediği ödeme seçenekleri gösterilir (bkz. §3.7).
