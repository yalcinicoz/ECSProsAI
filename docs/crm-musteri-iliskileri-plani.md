# Müşteri İlişkileri Yönetimi (CRM Talep/Şikayet) — Plan v2 (ONAYLI · T0-T3 UYGULANDI 2026-09-07 ⚠️ restart bekliyor)

> Eski ECS panelindeki `/crm/musteri-iliskileri-yonetimi` sayfasının bu projeye taşınması.
> Keşif kaynağı: eski panelin kaynak kodu yerelde yok, eski sunucuya SSH tanımı yok → keşif eski MySQL
> veritabanı (`juludedb`, `cm_*` tabloları) üzerinden yapıldı (2026-09-07). Ekran davranışları veri
> modelinden ÇIKARIMDIR; §5'teki sorular onaylanmadan uygulamaya geçilmez.

## 1. Eski sistemde ne var (veri keşfi)

| Tablo | Satır | Anlamı |
|---|---|---|
| `cm_crm` | 43.184 | **Kayıt (talep/şikayet)**: TakipNo (10 hane), Tür (Şikayet/Talep), Arayan ad-telefon, Müşteri id-ad-telefon, SiparişID, Durum, Konu Başlığı, İçerik (HTML, ürün tablosu/görsel gömülü — 6.838 kayıtta `<img>`), oluşturan/güncelleyen personel, Gizle (silme, 3 kayıt) |
| `cm_crm_yapilan_islemler` | 125.314 | **İşlem/yanıt zaman çizelgesi**: HTML içerik, DurumID, DurumDegisikligi (yeni durum), EtiketlenenPersonelID (bir personele "etiketle"), Gizle=1 → açılış kaydı |
| `cm_crm_etiket` | 8.383 | İşlemde etiketlenen personel (kime atandı/haber verildi) |
| `cm_crm_kontrol_edenler` | 368.101 | **Okuma kaydı**: hangi personel hangi işlemi ne zaman gördü |
| `cm_crm_durum` | 6 | Beklemede · İlgili Birimde · Çözülemedi (gizli) · Çözüldü · Hatalı Kayıt · Tazmin Sürecinde |
| `cm_crm_tur` | 2 | Şikayet · Talep |
| `cm_crm_konu_basliklari` | 22 | Konu başlıkları, türe bağlı, sıralı (Kargo Teslimatı 13.435, Kredi Kartı ve Bakiye Ödemeleri 8.102, Ürün İadesi Girilmemiş 4.546, Kargo Geri Çekim 4.252, Sipariş Aciliyet 3.072 …) |
| `cm_crm_alanlar` + `_konu_basliklari_alanlar` | 5 / 88 | **Konu başına form alanları**: Sipariş ID, Resim, İçerik, Arayan Ad Soyad, Arayan Telefon — hangi konuda hangi alan görünür (örn. Kargo Teslimatı'nda Resim yok; Müşteri Temsilcisi Şikayet'te Sipariş ID yok) |
| `cm_crm_not` | 153.591 | **Üye/sipariş işlem günlüğü** (CRM kaydından bağımsız): Tip 1 üye (kara liste, bilgi/şifre güncelleme, üyelik iptali, sepet silme), Tip 2 (53.801), Tip 3 sipariş (arama yapıldı/ulaşılamadı, eksik ürün, ürün değişikliği, satış yapan güncellendi), Tip 4 toplu fiyat, Tip 5 kargo tercihi, Tip 7 stok kartı güncelleme; JsonData snapshot |
| `cm_bildirim` | 113.837 | Panel bildirimleri (etiketleme → bildirim) |
| `cm_sorunlusiparisler` | 44.435 | Sorunlu sipariş listesi (ayrı ekran olabilir — §5 K1) |
| `cm_mt_kalite_*` | 17/9/49/6/374 | Müşteri temsilcisi kalite değerlendirme (soru/kategori/puan) — ayrı özellik (§5 K1) |

Rakamlar: 2020'den bu yana 43K kayıt (yıllık 8K → 1.2K'ya düştü), 53 personel; %98,5 kayıt sipariş+üye bağlı;
%45'inde arayan ≠ müşteri (yakını arıyor). Durum dağılımı: Çözüldü 42.700, Hatalı Kayıt 396, Tazmin 43,
İlgili Birimde 30, Beklemede 15. Personel `dfpersonel` (53) → yeni IAM kullanıcılarına eşleme gerekir.

## 2. Bu projede karşılığı (öneri)

Yeni modül **`Crm` altında `crm.tickets`** (ayrı modül açmak yerine mevcut Crm'e ekleme; Requests modülünün
`ProjectRequest/RequestActivity` deseni birebir örnek — iç proje talepleri oradadır, müşteri talepleri buraya).

### 2.1 Veri modeli (`crm` şeması)
- `crm_tickets`: Code (seri, K4), Type (complaint|request), SubjectId, Status, FirmPlatformId (kanal),
  MemberId? (+LegacyMemberId eşlemesi), CustomerName/Phone snapshot, CallerName/Phone, OrderId?/OrderNumber
  (eski `ORD-…` ve eski numaralar da metin olarak saklanır), BodyHtml (temizlenmiş HTML), CreatedBy/AssignedTo,
  ClosedAt, LastActivityAt, IsHidden.
- `crm_ticket_activities`: TicketId, Type (comment|status_change|assign|tag|created), BodyHtml, OldStatus/NewStatus,
  TaggedUserId, Attachments[] (`/media/crm/…`), UserId/UserName, CreatedAt.
- `crm_ticket_reads`: TicketId, ActivityId, UserId, ReadAt (eski "kontrol edenler" — okundu bilgisi).
- `crm_ticket_subjects`: Name, Type, SortOrder, IsActive, **FieldRules** (json: hangi alanlar görünür/zorunlu:
  orderNumber, images, body, callerName, callerPhone) — panelden yönetilir (definition değil; firma verisi).
- Durumlar: lookup (`core` lookup type `crm_ticket_status`) — 6 eski değer seed; panelden düzenlenebilir.
- Üye/sipariş işlem günlüğü (`cm_crm_not`): **ayrı** `crm_member_notes` / mevcut sipariş `InternalNotes`
  genişletmesi — K1'e bağlı.

### 2.2 Ekranlar (admin, sol menü **Müşteriler › Müşteri İlişkileri**)
1. **Liste** `/crm/tickets`: filtreler (durum, tür, konu, personel, tarih aralığı, takip no, müşteri ad/telefon,
   sipariş no, "bana etiketlenenler", "okumadıklarım"), sütunlar (takip no, tür, konu, müşteri, sipariş, durum,
   son işlem, oluşturan, tarih), satır tıklama → detay (K-list kuralı), sayaç kutucukları (Beklemede / İlgili
   Birimde / Tazmin).
2. **Yeni kayıt** modal/sayfa: tür → konu (türe göre) → **konuya göre alanlar**; müşteri arama (ad/telefon/üye no)
   → seçilince telefon/ad dolar; sipariş arama (müşterinin siparişleri) → seçilince sipariş kalemleri tablosu
   gövdeye eklenebilir (eski sistemdeki ürün tablosu davranışı); zengin metin + görsel yükleme.
3. **Detay**: başlık kartı (müşteri/arayan/sipariş linkleri), zaman çizelgesi (yanıt yaz, durum değiştir,
   personel etiketle — tek formda), okundu bilgisi (kim ne zaman gördü), sağda müye özeti (son siparişler, önceki
   kayıtları, kara liste durumu).
4. **Ayarlar**: Konu başlıkları + alan kuralları, durumlar (sıra/gizli).
5. **Bildirim**: etiketlenen personele panel bildirimi (mevcut panel SignalR bağlantısı — QuestionAlerts'e eklenir,
   ikinci bağlantı açılmaz) + isteğe bağlı e-posta (K2).
6. Üye detay sayfasına "Müşteri İlişkileri" sekmesi; sipariş detayına "Kayıtlar" bloğu (K16 site–panel senkronu
   gerekmez: müşteriye dönük yüzey K7'ye bağlı).

### 2.3 Eski veri aktarımı (K3)
MigrationTool yeni faz: `cm_crm` + `yapilan_islemler` + `etiket` + `kontrol_edenler` → yeni tablolar
(ID-koruyan, tekrar çalıştırılabilir), personel eşleme tablosu (dfpersonel → IAM user; eşleşmeyen → "Eski
personel: Ad" snapshot), sipariş eşleme (OrderNumber / LegacyOrderId), üye eşleme (LegacyMemberId). HTML içerik
temizliği (inline style/tablo kırpma, tozlu CDN görselleri korunur).

## 3. Fazlar — durum (2026-09-07)
- **T0 ✅** `crm.crm_ticket_statuses/_subjects/_tickets/_activities/_reads/_notifications/_legacy_staff` + `crm_ticket_tracking_seq` (migration `AddCrmTickets`, dev+demo), seed 6 durum + 22 konu, API `api/crm/tickets/*` (liste+sayaç, detay, open, order-lookup, create, activities, hidden, notifications seen/opened, settings, media).
- **T1 ✅** Admin: Müşteriler › Müşteri İlişkileri — liste (`/crm/tickets`), yeni kayıt (`/new`, sipariş sorgu + kanal seçimi), detay (`/:trackingNo` — Kayıt/Sipariş/Üye/Log sekmeleri, Yeni İşlem modalı, gizle), ayarlar (`/settings`).
- **T2 ✅** Bildirim: Header çanı (`TicketBell`, yanıp söner; gördü / kayda girdi ayrı), QuestionAlerts'e `TicketNotification` olayı + 60 sn poll, sidebar rozeti; üye detayı + sipariş detayı "Müşteri İlişkileri" bloğu.
- **T3 ✅** MigrationTool **Faz 30** (`dotnet run 30 [dry]`, `PG_CONN` env): 43.190 kayıt, 77.977 işlem, 137.764 okundu, 68.746 bildirim, 41 pasif eski personel IAM kullanıcısı (52 silinmiş personel ad-anlık görüntü). ⚠️ Eski işlem görselleri (`/media/crm/legacy/*`, 1.185 işlem + 2.501 kayıt) dosya kopyası eski sunucu erişimi bekliyor. Cutover'da faz yeniden çalıştırılır (LegacyId'li satırlar yenilenir, yeni kayıtlar korunur).
- **T4** yok (K7: müşteri görmez). Kalan: rehber sayfası.

### 3.0 Orijinal faz planı
- **T0** Veri modeli + migration + seed (durumlar, 22 konu + alan kuralları) + API (liste/detay/oluştur/işlem/okundu).
- **T1** Admin: liste + yeni kayıt + detay (zaman çizelgesi, durum, etiketleme, okundu).
- **T2** Bildirim (panel çanı) + üye/sipariş detay entegrasyonları + ayarlar ekranı.
- **T3** Eski veri aktarımı (K3 evetse) + doğrulama raporu.
- **T4** (K7 evetse) Site/mobil "Taleplerim" yüzeyi.
- Rehber sayfası + PROGRESS.

## 4. Kararlar (kullanıcı yanıtları, 2026-09-07 — kesin)

| # | Karar |
|---|---|
| K1 Kapsam | Yalnız talep/şikayet kayıtları: `cm_crm`, `_yapilan_islemler`, `_etiket`, `_kontrol_edenler`, `_durum`, `_tur`, `_konu_basliklari(_alanlar)`, `_alanlar`, `cm_bildirim`, `cm_crm_not` (detayda üye/sipariş notu olarak okunur). Sorunlu siparişler ve MT kalite puanlama DIŞARIDA. |
| K2 Bildirim | Yeni kayıt açılınca kimseye bildirim YOK. İşlem eklenince: kaydı açan, o kayıtta işlem yapmış olanlar, etiketlenen (ve etiketlenip hâlâ işlem yapmamış olanlar). Yalnız panel çanı; okunmamış varsa yanıp söner. **"Gördü" ve "kayda girdi" ayrı** (SeenAt/OpenedAt); gördü ama girmediyse bildirim yanmaya devam eder ve kayıtta "gördü, girmedi" olarak tarih-saat-kullanıcıyla yazılır; kayda girince söner ve "kayda girdi" işlenir. |
| K3 Eski veri | Aktarılır (43K kayıt + 121K işlem + 369K okundu + 115K bildirim). Eski personel kayıtları da taşınır: `dfpersonel` → IAM kullanıcısı (kullanıcı adı/e-posta eşleşirse mevcut; yoksa PASİF kullanıcı açılır, `MustChangePassword`), eşleme `crm_ticket_legacy_staff`. |
| K4 Takip no | Eski biçim sürer: `unix saniye + sıra` (10 hane; sıra `crm_ticket_tracking_seq` 50.000'den başlar → eski numaralarla çakışmaz; UNIQUE). Aktarılan kayıt eski numarasını korur. |
| K5 İçerik | Zengin metin (Quill) sürer; **görsel gövdeye gömülmez, ek olarak yüklenir** (`/media/crm/...`); konu "Resim" zorunlu alanı → en az bir ek. Eski kayıtlardaki `/upload/Images/cm_crm/*` görselleri ek listesine alınır (dosya kopyası eski sunucu erişimi gelince), ürün tablolarındaki CDN görselleri olduğu gibi kalır. Sipariş kalemleri gövdeye eklenmez (eski sistemde de yoktu; detayda Sipariş sekmesi). |
| K6 Durum/konu | 6 durum + 22 konu + konu→alan kuralları aynen. "Çözülemedi" GİZLİ durum: varsayılan listede görünmez, filtreyle görünür; yeni işlemde seçilemez. "Hatalı Kayıt" mükerrer kontrolünden muaf. Konu/alan/durum ayarları panelden yönetilir (tablo, definition değil). |
| K7 Müşteri | Görmez; iç sistem. Site/mobil yüzeyi YOK. |
| K8 Yetki | Şimdilik herkese açık (`[Authorize]`); panel geneli yetki modeli ayrı iş. |
| K9 Kanal | Kayıt kanala bağlı değil; sipariş numarası kanalı belirler. Aynı numara birden fazla siparişte (farklı kanal) bulunursa formda **kanal seçici** açılır. |
| Ek | Eski akış aynen: konu seçilir → tür konudan türer; sipariş no girilince müşteri ad/telefon siparişten kopyalanır; mükerrer kontrol = aynı sipariş + aynı konu açık kayıt; detay sayfası sekmeleri Kayıt / Sipariş / Üye / Log — sayfadan ayrılmadan müşteri ve sipariş bilgisi. Eski koddaki hatalar (çalışmayan "kayıt notu" formu, GET'te yan etki, transaction yokluğu) taşınmaz. |

## 5. Uygulama notları (eski kod keşfi — ECSGYE.Solution, 2026-09-07)
- Takip no: `DateTimeOffset.Now.ToUnixTimeSeconds() + CRMID` (PanelYordamlar.cs:1502,1644). Bildirim kalıpları: "Oluşturduğunuz / İşlem Yaptığınız / Etiketlendiniz / Etiketlendiğiniz … kaydına yeni işlem yapıldı" (1834-1924). Okundu: detay her açılışta eksik işlemler için `kontrol_edenler` (1376-1394); liste "Kontrol Edildi" = son işlem okunmuş mu. Etiketleme tek kişi, sahiplik değiştirmez. `cm_bildirim.Goruldu/Acildi` 1=hayır 2=evet.
- Eski tablo sayıları (2026-09-07): crm 43.187 (Gizle=1: 3), işlem 121.207 (Gizle=1 açılış satırı 43.206), etiket 8.407, okundu 368.824, bildirim 115.145, personel 53 aktif kullanıcı. Sipariş no biçimleri: 7-13 haneli eski numaralar + 21 `ORD-…`; görselli kayıt 6.839 (ürün tablosu CDN) + işlemde 1.410 (`/upload/Images/cm_crm/`).
