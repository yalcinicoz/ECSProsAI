# PayTR Ödeme Entegrasyonu (Direct API — TEST MODU)

**Durum:** Kod tamam ve derleniyor (2026-07-30). Yalnız **test modu**. Canlı ödeme için
işletmenin **PCI-DSS SAQ D** uyumu + **PayTR Direct API onayı** gereklidir; o tamamlanana
dek canlıya alınmaz. Uçtan uca gerçek test, kullanıcının PayTR test merchant bilgilerini
panelden girmesi + PayTR panelinde callback URL tanımlaması ile yapılır.

## Karar geçmişi
- Ürün: **Direct API** (kullanıcı kararı) — kart bizim sunucudan geçer. iFrame API PCI
  yükünü PayTR'da bırakırdı; kullanıcı tam form kontrolü için Direct'i seçti.
- Kart verisi: **yalnız maskeli PAN** (ilk6+son4) hukuki/itiraz amacıyla saklanır.
  **CVV ve tam kart numarası HİÇBİR yerde saklanmaz/loglanmaz/diske yazılmaz.**
- Mod: **yalnız test** (`test_mode=1` her koşulda zorlanır; provider ayardan bağımsız true döner).

## Mimari (kargo/SMS/SMTP entegrasyon deseniyle aynı)
- **Katalog:** `definition.integration_services` → `paytr` / serviceType `payment`.
  SettingsSchema: merchantId/merchantKey/merchantSalt (credentials, şifreli), testMode (settings).
  (DatabaseSeeder — platform/geliştirici firma doldurur; altın kural korunur.)
- **Kimlik:** firma PayTR bilgileri `core_firm_platform_integrations`'da **şifreli** (Data
  Protection). Admin formu şemadan otomatik üretilir (FirmDetailPage).
- **Provider:** `DbPaymentSettingsProvider` aktif payment entegrasyonunu okur+deşifre eder
  (2 dk cache). Yalnız MAĞAZA kimliğini döner — kart verisiyle ilgisi yok.
- **Servis:** `PayTrDirectService` — hash (adım1 + callback), maskeleme, /odeme POST.
  Hash'ler PayTR resmi PHP formülüyle **birebir doğrulandı** (5kgwFPIf… / xgGNnTxK…).
- **Endpoint'ler** (`PaymentController`):
  - `POST /api/store/payment/paytr/init` — sipariş+kart → PayTR /odeme → 3D HTML döner.
    Kart alanları yalnız burada; maskeli PAN siparişe yazılır (PayTrPaymentBaslatCommand).
  - `POST /api/store/payment/paytr/callback` — [AllowAnonymous], **vitrin kapısından muaf**
    (PayTR token taşımaz; güvence HASH). Hash doğrula → sipariş paid/failed → düz "OK".
  - `GET/POST /odeme-sonuc/basarili|basarisiz` — 3D dönüş sayfaları (bilgi; kesin sonuç callback'te).
- **Sipariş:** PayTrCallbackUygulaCommand `PaymentStatus` = paid/failed; maskeli PAN + durum
  `Order.CustomerNotes` jsonb "payment" anahtarında (kolon/migration YOK). Idempotent.
- **Akış (ödeme sayfası):** "Kart ile Öde" + Siparişi Tamamla → checkout siparişi oluşturur
  (pending) → init → PayTR 3D HTML sayfaya yazılır → kullanıcı bankada onaylar → PayTR
  callback'i siparişi paid yapar + kullanıcıyı /odeme-sonuc'a döndürür. **Kapıda ödeme
  PayTR'a GİTMEZ** (mevcut akış aynen). Ödeme çözülene dek sepet silinmez.

## Hash formülleri (referans)
- Adım1 token: `base64(HMAC-SHA256(merchant_id+user_ip+merchant_oid+email+payment_amount+
  payment_type+installment_count+currency+test_mode+non_3d + merchant_salt, merchant_key))`
- Callback: `base64(HMAC-SHA256(merchant_oid+merchant_salt+status+total_amount, merchant_key))` → "OK"
- payment_amount = **kuruş** (TL×100 tam sayı). merchant_oid = OrderNumber (alfanümerik).

## Kullanıcının yapması gerekenler (canlı test için)
1. PayTR Mağaza Paneli > Entegrasyon Bilgileri'nden merchant_id/key/salt al.
2. Admin panel > firma > entegrasyon ekle > "PayTR (Direct API)" — bilgileri gir, testMode açık, aktif et.
3. PayTR panelinde **Bildirim URL** = `https://<site>/api/store/payment/paytr/callback`,
   başarı/başarısız = `https://<site>/odeme-sonuc/basarili|basarisiz` tanımla.
4. PayTR'dan Direct API erişimi onayı iste (Direct API onay gerektirir).
5. Test kartıyla uçtan uca dene; sipariş PaymentStatus'unun paid olduğunu panelde gör.

## Bilinen sınırlar / TODO (canlı öncesi)
- Yalnız test modu; canlı için PCI-DSS SAQ D + PayTR onayı + `test_mode=0`'a geçiş kararı.
- Taksit (installment_count=0 sabit), sepet tek kalem olarak gönderiliyor (basit); gerekirse
  gerçek kalem dökümüne çıkarılır.
- Başarısız kart ödemesinde sipariş "failed/unpaid" pending kalır; kullanıcı yeni checkout
  ile yeniden dener (sepet korunur). Otomatik "ödemeyi yeniden dene" ekranı ayrı faz.
- Ödeme sayfası kart formu Direct API'ye uygun (kart no/CVV toplanıyor) — canlıya
  geçilecekse form alanlarının PCI kapsam analizi yapılmalı.
