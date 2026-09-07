# Müşteri İlişkileri Yönetimi (CRM Talep/Şikayet) — Plan v1 (TASLAK, onay bekliyor)

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

## 3. Fazlar (onay sonrası)
- **T0** Veri modeli + migration + seed (durumlar, 22 konu + alan kuralları) + API (liste/detay/oluştur/işlem/okundu).
- **T1** Admin: liste + yeni kayıt + detay (zaman çizelgesi, durum, etiketleme, okundu).
- **T2** Bildirim (panel çanı) + üye/sipariş detay entegrasyonları + ayarlar ekranı.
- **T3** Eski veri aktarımı (K3 evetse) + doğrulama raporu.
- **T4** (K7 evetse) Site/mobil "Taleplerim" yüzeyi.
- Rehber sayfası + PROGRESS.

## 4. Kararlar (K — onay bekliyor)
Bkz. §5; kararlar netleşince buraya taşınır.

## 5. Açık sorular
- **K1 Kapsam:** Eski sayfa yalnız talep/şikayet kayıtları mı; yoksa (a) üye/sipariş işlem günlüğü ("notlar":
  arama yapıldı, kara liste, ürün değişikliği), (b) "Sorunlu siparişler" listesi, (c) müşteri temsilcisi kalite
  puanlama da aynı sayfanın sekmeleri mi? Hangileri bu işe dahil?
- **K2 Bildirim:** Etiketlenen personel eski sistemde nasıl haber alıyordu (panel çanı / e-posta / SMS)? Yeni:
  panel çanı varsayılan; e-posta istenir mi?
- **K3 Eski veri:** 43K kayıt + 125K işlem aktarılsın mı (salt-okunur geçmiş), yoksa sıfırdan mı başlansın?
  Aktarılacaksa 53 eski personelin yeni kullanıcılara eşlemesi (ad listesi çıkarırım, siz eşlersiniz).
- **K4 Takip numarası:** Eski 10 haneli sayısal TakipNo yerine `CRM-2026-000123` gibi seri mi, yoksa eski biçim mi?
  Aktarılan eski kayıtlar eski numarasını korur.
- **K5 İçerik:** Zengin metin (kalın/liste/görsel) + görsel yükleme sürsün mü; sipariş kalemleri tablosunu gövdeye
  ekleme davranışı istenir mi?
- **K6 Durumlar/konular:** 6 durum ve 22 konu aynen seed edilsin mi? "Çözülemedi" eski sistemde gizli — kalksın mı?
  Konu→alan kuralları panelden yönetilebilir olsun mu?
- **K7 Müşteriye görünürlük:** Müşteri sitede/mobilde (Hesabım) kendi kayıtlarını ve durumunu görsün mü, yeni
  talep açabilsin mi? (Eski sistemde iç kullanım gibi görünüyor.)
- **K8 Yetki:** Kim görür/kaydeder/durum değiştirir? Öneri: `crm.tickets.view` ve `crm.tickets.manage`; ayrıca
  "yalnız etiketlendiğim kayıtlar" görünümü.
- **K9 Kanal:** Kayıtlar kanala (Mishar/Tozlu/Julude…) bağlı mı, ortak mı? Eski veri tek platform (tozlu ağırlıklı).
