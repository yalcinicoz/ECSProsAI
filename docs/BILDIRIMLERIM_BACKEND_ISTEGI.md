# Uygulama İçi "Bildirimlerim" — Backend İsteği

Tarih: 2026-09-08 · Kapsam: mobil uygulama (Android/iOS) · Bağlı belge: `PUSH_BILDIRIM_ENTEGRASYONU.md`

Bu belge tek başına yeterlidir; push entegrasyon belgesindeki tablo ve uçların
üzerine kurulur. Amaç: kullanıcıya gönderilen bildirimlerin uygulama içinde
(ana sayfa app bar'ındaki zil ikonu) listelenmesi, okundu/silindi yönetimi ve
zil rozetinde okunmamış sayısı.

Temel ilke: **listenin ve durumların tek sahibi backend'dir.** Mobil hiçbir
bildirimi yerelde saklamaz, üretmez, türden etiket/ikon türetmez; ne gelirse
onu gösterir. Cihazın bildirim çekmecesi kaynak DEĞİLDİR (kapatılınca gider,
izin yoksa hiç oluşmaz, cihaz değişince kaybolur).

---

## 1. Veri modeli

`storefront.push_notifications` (push belgesi §7) her gönderimde zaten satır
alıyor. Eklenecek sütunlar:

```sql
ALTER TABLE storefront.push_notifications
  ADD COLUMN IF NOT EXISTS inbox boolean NOT NULL DEFAULT true,   -- uygulama içi listede görünsün mü
  ADD COLUMN IF NOT EXISTS read_at timestamptz NULL,               -- kullanıcı okudu
  ADD COLUMN IF NOT EXISTS deleted_at timestamptz NULL,            -- kullanıcı sildi (soft delete)
  ADD COLUMN IF NOT EXISTS expires_at timestamptz NOT NULL DEFAULT now() + interval '90 days',
  ADD COLUMN IF NOT EXISTS image_url text NULL,                    -- ürün görseli vb. (tam URL)
  ADD COLUMN IF NOT EXISTS icon text NOT NULL DEFAULT 'info',      -- §5 ikon anahtarı
  ADD COLUMN IF NOT EXISTS dismiss_on_open boolean NOT NULL DEFAULT false; -- açılınca listeden düşsün mü

CREATE INDEX IF NOT EXISTS ix_push_notifications_inbox
  ON storefront.push_notifications (member_id, created_at DESC)
  WHERE inbox AND deleted_at IS NULL;
```

Kurallar:
- **Satır, FCM sonucundan bağımsız oluşur.** Üyeye hedeflenen her bildirim
  (`member_id` dolu) FCM'e gidemese bile (`status='skipped'`: token yok / izin
  yok / sessiz saat) `inbox=true` ile listede görünür. Böylece push iznini
  kapatmış kullanıcı da "kargon yola çıktı"yı uygulama içinde görür.
- `inbox=false` yalnız panelde "yalnız push, listede gösterme" seçilen
  şablonlar için (ör. anlık flaş kampanya).
- `expires_at` şablon/sınıf bazlı, panelden ayarlanır. Varsayılan öneri:
  `transactional` 90 gün, `marketing` 30 gün. Süresi dolan satır listede
  görünmez (silinmesi şart değil).
- `dismiss_on_open` şablon bazlı panel ayarı. Varsayılan `false` (dokununca
  okundu olur, listede kalır). `true` ise dokununca liste satırı okundu +
  silindi sayılır (mobil `deleted_at` için ayrıca istek atmaz; backend
  `read` çağrısında `dismiss_on_open` ise `deleted_at` de set eder).
- `member_id` boş (misafir cihaza giden kampanya) satırlar listede
  gösterilmez; liste yalnız üyeler içindir (§3).

Mevcut `opened_at` ile ilişki: push çekmecesinden açılma
(`POST /push-devices/opened {dedupId}`) hem `opened_at` hem `read_at` set eder.

---

## 2. Uçlar

Hepsi **üye JWT** ister. Cihaz token'ıyla gelen istek `401`. `firmPlatformId`
diğer uçlardaki gibi query ya da `X-Firm-Platform` başlığı. Zarf standart:
`{success, data, error, code}`. Tarihler ISO-8601 UTC.

### 2.1 Liste
```
GET /api/store/account/notifications?page=1&pageSize=20
```
- Filtre: `member_id = ben`, `inbox = true`, `deleted_at IS NULL`,
  `expires_at > now()`. Sıra: `created_at DESC`.
- `pageSize` en fazla 50; sayfalama diğer listelerle aynı zarf.
- **Her sayfada `unreadCount` gelir** (tüm liste için, sayfaya bağlı değil) —
  zil rozeti bundan beslenir; ayrı sayaç ucu gerekmez.

Yanıt:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "8c1e…",
        "dedupId": "order_shipped:MIS0000061",
        "type": "order_shipped",
        "class": "transactional",
        "title": "Siparişin kargoya verildi",
        "body": "MIS0000061 numaralı siparişin DHL/MNG Kargo ile yola çıktı.",
        "link": "/siparislerim/2f3a…-guid",
        "imageUrl": "https://cdn.misharitalia.com/img/640/85/….webp",
        "icon": "cargo",
        "createdAt": "2026-09-08T11:02:22Z",
        "readAt": null,
        "dismissOnOpen": false
      }
    ],
    "unreadCount": 3,
    "totalCount": 27,
    "page": 1,
    "pageSize": 20,
    "totalPages": 2,
    "hasNextPage": true
  }
}
```

Alanlar:
| Alan | Zorunlu | Açıklama |
|---|---|---|
| `id` | evet | Satır kimliği (okuma/silme bu id ile) |
| `dedupId` | evet | Push `data.dedupId` ile aynı (açılma eşleştirmesi) |
| `type` | evet | Push belgesi §4 tür adı; mobil bunu **göstermez**, yalnız analitik |
| `class` | evet | `transactional` / `marketing` |
| `title`, `body` | evet | Gönderilen metnin aynısı (Türkçe, hazır) |
| `link` | evet | Push belgesi §3 link kataloğundan; mobil aynı çözücüyle açar |
| `imageUrl` | hayır | Ürünle ilgiliyse ürün görseli (favori, stok, sepet, yorum, soru…) |
| `icon` | evet | §5 listesinden anahtar; görsel varsa ikon küçük rozet olarak görselin köşesinde |
| `createdAt` | evet | Gönderim/oluşturma zamanı |
| `readAt` | evet (null olabilir) | Okunduysa zaman |
| `dismissOnOpen` | evet | Dokununca listeden düşsün mü |

### 2.2 Okundu işaretle
```
POST /api/store/account/notifications/{id}/read
```
- `read_at = now()` (zaten doluysa dokunma). `dismiss_on_open` ise
  `deleted_at = now()` da set edilir.
- Yanıt: `{ "success": true, "data": { "unreadCount": 2 } }`
- Başkasının satırı ya da yok → `404`.
- Idempotent: ikinci çağrı yine 200.

### 2.3 Tümünü okundu işaretle
```
POST /api/store/account/notifications/read-all
```
- Üyenin listede görünen tüm okunmamışlarına `read_at`.
- Yanıt: `{ "success": true, "data": { "unreadCount": 0 } }`

### 2.4 Sil
```
DELETE /api/store/account/notifications/{id}
```
- `deleted_at = now()`. Geri alma yok. Yanıt `{ "success": true, "data": { "unreadCount": 2 } }`.
- Yok / başkasının → `404`. Zaten silinmiş → `200` (idempotent).

### 2.5 Tümünü sil
```
DELETE /api/store/account/notifications
```
- Listede görünen tüm satırlara `deleted_at`. Yanıt `{ "success": true, "data": { "unreadCount": 0 } }`.

### 2.6 Push'tan açılma (push belgesi §8, hatırlatma)
```
POST /api/store/push-devices/opened { "dedupId": "…" }
```
- Üye ya da cihaz token'ı. `opened_at` + `read_at` set eder; `dismiss_on_open`
  ise `deleted_at`.

---

## 3. Misafir

Liste yalnız üyeler içindir. Misafir zile basınca mobil giriş daveti gösterir;
backend'e istek gitmez. Misafirken alınan kampanya push'ları listeye girmez
(satırda `member_id` yok). Kullanıcı giriş yapınca **yalnız üyeye bağlı**
satırlar gelir; cihaz bazlı geçmiş üyeye taşınmaz.

---

## 4. Rozet (okunmamış sayısı)

- Kaynak: liste yanıtındaki `unreadCount`; okuma/silme yanıtları da güncel
  sayıyı döner, mobil ek istek atmadan rozeti günceller.
- Mobil tazeleme anları: uygulama açılışı, arka plandan dönüş, ön planda push
  gelmesi, liste ekranına giriş. Periyodik sorgulama yok.
- İsteğe bağlı hafif uç (mobil listeyi çekmeden rozet için):
  `GET /api/store/account/notifications/unread-count` → `{ "unreadCount": 3 }`.
  Liste yanıtı `unreadCount` taşıdığı sürece zorunlu değil.

---

## 5. İkon anahtarları (`icon`)

Mobil bu anahtarları sabit bir ikon setiyle çizer; bilinmeyen anahtar
`info`ya düşer. Backend şablon/tür bazında atar (panelden değiştirilebilir).

| Anahtar | Kullanım (push belgesi §4 türleri) |
|---|---|
| `order` | order_created, order_confirmed, order_cancelled |
| `cargo` | order_shipped, order_delivered |
| `payment` | payment_pending, wallet_credit |
| `return` | return_status |
| `favorite` | favorite_price_drop, favorite_back_in_stock, favorite_low_stock |
| `stock` | stock_alert (gelince haber ver) |
| `cart` | cart_reminder, cart_price_drop |
| `question` | question_answered |
| `review` | review_invite, review_approved, review_rejected |
| `coupon` | coupon_assigned, coupon_expiring |
| `campaign` | campaign, welcome, winback, viewed_reminder |
| `account` | hesap/üyelik ile ilgili |
| `info` | diğer |

`imageUrl` verildiğinde görsel ana öğe olur, ikon köşede küçük rozet.

---

## 6. Gönderim akışıyla bağ (backend içi)

1. Olay → şablon → `push_notifications` satırı: `title/body/link/data` +
   `inbox`, `icon`, `image_url`, `expires_at`, `dismiss_on_open` şablondan.
2. FCM gönderimi (push belgesi §2). Sonuç `status`'a yazılır; **satır her
   durumda kalır** (§1).
3. Aynı `dedupId` ikinci kez üretilirse yeni satır açılmaz (UNIQUE).
4. Kullanıcı push'a dokununca mobil `opened` yollar → `read_at`.
5. Kullanıcı listeden dokununca mobil `read` yollar; link mobilde açılır.

---

## 7. Test prosedürü (staging)

Test hesabı: `mobil.test@ecspros.com` (staging). Adımlar:
1. Bu üyeye 3 bildirim üret: bir `order_shipped` (imageUrl'lü, `cargo`), bir
   `favorite_price_drop` (imageUrl'lü, `favorite`), bir `campaign`
   (`dismissOnOpen:true`, `campaign`). Biri FCM'e gitmesin (`status='skipped'`)
   → yine listede olmalı.
2. `GET /account/notifications` → 3 satır, `unreadCount:3`, sıra yeniden eskiye.
3. `POST …/{id}/read` (order_shipped) → `unreadCount:2`; tekrar → 200, sayı aynı.
4. `POST …/{campaign-id}/read` → `unreadCount:1`, satır listeden düştü (dismissOnOpen).
5. `DELETE …/{favorite-id}` → `unreadCount:0`, listede 1 satır (order_shipped, okunmuş).
6. `POST …/read-all` ve `DELETE /account/notifications` → boş liste, `unreadCount:0`.
7. Cihaz token'ıyla `GET` → 401. Başka üyenin id'siyle `read`/`DELETE` → 404.
8. `expires_at` geçmiş bir satır → listede yok.
9. Push çekmecesinden açılan bildirim (`POST /push-devices/opened`) → listede okunmuş görünür.

---

## 8. Kabul kriterleri

- [ ] Liste ucu sayfalı, `unreadCount` her yanıtta.
- [ ] FCM'e gidemeyen üye bildirimleri de listede.
- [ ] `read`, `read-all`, `DELETE`, `DELETE all` idempotent ve güncel `unreadCount` döner.
- [ ] Yetki: yalnız kendi satırları; cihaz token'ı 401; yabancı id 404.
- [ ] `icon` her satırda §5 listesinden; ürünle ilgili türlerde `imageUrl` dolu.
- [ ] `link` push belgesi §3 kataloğuna uygun (mobil aynı çözücüyle açar).
- [ ] `dismissOnOpen` şablondan geliyor; `read` çağrısı ona göre `deleted_at` set ediyor.
- [ ] `expires_at` şablon bazlı; dolanlar listede yok.
- [ ] Swagger (`/swagger/mobile/swagger.json`) güncel.

---

## 9. Mobilde yapılacaklar (bu uçlar gelince)

- Bildirimler ekranı: zilden perdeyle açılır, sayfalı liste, "Bugün / Bu hafta /
  Daha önce" başlıkları (yalnız sunum), görsel/ikon, okunmamış vurgusu,
  sola kaydırıp silme, üstte "Tümünü okundu işaretle" ve "Tümünü sil" (onaylı),
  boş durum. Metinler config `notifications.*`.
- Dokunma: `read` isteği + link çözücü; `dismissOnOpen` ise satır düşer.
- Zil rozeti: `unreadCount`; §4'teki anlarda tazeleme.
- Ön planda push gelince liste ve rozet tazelenir (ön plan gösterimiyle birlikte).
- Misafir: zil → giriş daveti.
- App bar'daki zarf ikonu kaldırılır (bildirimler tek yerde).
