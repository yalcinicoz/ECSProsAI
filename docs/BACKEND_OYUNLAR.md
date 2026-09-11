# Şans Oyunları — Backend Sözleşmesi (Çarkıfelek · Salla Kazan · Kazı Kazan)

Hazırlayan: mobil ekip · Tarih: 2026-09-11 (rev. 2) · Durum: **mobil taraf hazır, backend uçları bekleniyor**

Mobil uygulamada üç oyunun tasarımı ve akışı bitti. Uygulama şu anda backend uçları
404 döndüğü için (yalnız debug derlemede) `assets/dev/games.json` örneğini kullanıyor;
uçlar aşağıdaki biçimde açıldığı an örnek devre dışı kalır, hiçbir mobil değişiklik
gerekmez. **Sonucu her zaman sunucu belirler.** Mobil ödül seçmez, olasılık bilmez,
kupon üretmez; yalnız sunucunun söylediği ödüle doğru animasyon oynatır.

## 0. Kurgu (mağaza sahibinin kararı)

- Oyunlar **hub sayfası değil, günlük kampanya**: panelde bugün çark, yarın kazı kazan aktif edilir.
  Mobilde **ana sayfada taşınabilir yuvarlak bir ikon** çıkar (sürüklenir, kenara yapışır, ✕ ile
  kapatılır); dokununca o günün oyunu açılır. Aktif oyun yoksa ikon yok.
- **Sonuç tamamen sunucu kurgusudur.** Çarkta hangi dilimde duracağı, kutudan ne çıkacağı, kazı
  kazanda 6 kutucuğun içi — hepsi `play` yanıtında hazır gelir. Mobil ödül seçmez, olasılık bilmez.
- **Kazı kazan 6 kutucuk (3×2):** her kutucukta bir değer; **3 kutucuk aynı değeri taşıyorsa** o
  ödül kazanılmıştır. Sunucu 6 hücreyi ve `won/prize`'ı gönderir; mobil eşleşen 3'ü vurgular.
- Aynı anda birden fazla oyun aktifse **her oyun için ayrı ikon** çıkar; her ikon kendi başına taşınır (konumu oyun koduna göre saklanır), her birinin kendi ✕'i var. `/oyunlar` deep link'i ilkini açar.
- ✕ ile kapatma o oyun `id`'si için cihazda saklanır; **yeni oyun (yeni id) gelince ikon yine çıkar**.
  Oynanıp hak bitince (`status` artık `available` değilse) ikon kendiliğinden kaybolur.

## 1. Uçlar

| Uç | Amaç |
|---|---|
| `GET /api/store/games?firmPlatformId=…` | Bugün aktif oyun(lar) + bu kullanıcı/cihaz için durum (genelde 1 kayıt) |
| `POST /api/store/games/{code}/play` | Oyna: sunucu ödülü seçer, kuponu üretir, hakkı düşer |
| `GET /api/store/account/games/history` *(isteğe bağlı)* | Üyenin kazandıkları (Kuponlarım zaten kuponları gösteriyor; bu uç olmadan da çalışır) |

Kimlik: standart cihaz token'ı (misafir) / üye token'ı. Üye/misafir ayrımını sunucu
token'dan yapar; misafire açık olmayan oyun için `status: "login_required"` döner.

Zarf: standart `{success, data}`; hata `{success:false, error}` (400/409). Mobil
`error` metnini olduğu gibi kullanıcıya gösterir (ör. "Bugünkü hakkını kullandın").

## 2. `GET /games` — yanıt

```json
{
  "success": true,
  "data": [
    {
      "id": "g-wheel-1",
      "code": "cark",
      "type": "wheel",
      "title": "Çarkıfelek",
      "subtitle": "Çevir, indirim kuponunu kap!",
      "description": "Her gün 1 çevirme hakkın var. Kazandığın kupon Kuponlarım'a eklenir.",
      "imageUrl": null,
      "themeColor": "#5B21B6",
      "accentColor": "#F59E0B",
      "requiresLogin": true,
      "alwaysWin": false,
      "status": "available",
      "statusLabel": "Bugün 1 hakkın var",
      "remainingPlays": 1,
      "nextPlayAt": null,
      "startsAt": "2026-09-01T00:00:00Z",
      "endsAt": "2026-09-30T20:59:59Z",
      "ctaLabel": "Çarkı Çevir",
      "rulesText": "• Günde 1 çevirme hakkı.\n• Kuponlar 7 gün geçerlidir.",
      "prizes": [
        {"id": "p1", "label": "%10 İndirim", "shortLabel": "%10", "kind": "coupon", "color": "#7C3AED"},
        {"id": "p2", "label": "Bir dahaki sefere", "shortLabel": "Pas", "kind": "none", "color": "#1F2937"},
        {"id": "p3", "label": "50 TL İndirim", "shortLabel": "50 TL", "kind": "coupon", "color": "#DB2777"}
      ]
    }
  ]
}
```

### Alanlar

| Alan | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `id` | string | ✓ | |
| `code` | string | ✓ | URL/push'ta kullanılır: `/oyunlar/{code}` |
| `type` | `wheel` \| `shake` \| `scratch` | ✓ | Mobil tanımadığı tipte "uygulamayı güncelle" gösterir |
| `title` | string | ✓ | |
| `subtitle` | string | | Kart ve oyun ekranı alt başlığı |
| `description` | string | | Kurallar panelinin girişi |
| `imageUrl` | string | | **Ana sayfadaki yuvarlak ikonun görseli** (kare/yuvarlak, ≥128px); yoksa tipin ikonu |
| `themeColor` | `#RRGGBB` | | Oyun ekranı zemini (yoksa tema birincil rengi) |
| `accentColor` | `#RRGGBB` | | Vurgu: ok, rozet, ödül rengi (yoksa tema ikincil rengi) |
| `requiresLogin` | bool | ✓ | Misafir kartı görür, oynamak için girişe yönlenir |
| `alwaysWin` | bool | ✓ | Herkes kazanır modu (§3a): `true` ise `prizes[]`'da `none` yok ve `play` asla `won:false` dönmez |
| `status` | enum | ✓ | `available` · `cooldown` · `exhausted` · `login_required` · `ended` |
| `statusLabel` | string | ✓ | Kullanıcıya gösterilen durum metni. **Mobil metin üretmez**; "Yarın tekrar gel", "Bu hafta 2 hakkın var", "Kampanya bitti" hepsi buradan |
| `remainingPlays` | int | | Varsa kartta "Kalan hak: n" |
| `nextPlayAt` | ISO | | Bilgi amaçlı |
| `startsAt` / `endsAt` | ISO | | Bilgi amaçlı; süresi dolanı listeye koymayın ya da `ended` verin |
| `ctaLabel` | string | | Ana düğme metni (yoksa config: "Oyna"/"Çevir"/"Kartı Kazı") |
| `rulesText` | string | | Satır sonlu düz metin; varsa app bar'da (i) ile açılır |
| `prizes[]` | liste | ✓ | Aşağıda |

### `prizes[]`

| Alan | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `id` | string | ✓ | `play.prizeId` bununla eşleşir |
| `label` | string | ✓ | "50 TL İndirim" |
| `shortLabel` | string | | **Çark dilimi** için kısa metin ("50 TL", "%10", "Pas"); yoksa `label` |
| `kind` | enum | ✓ | `coupon` · `free_shipping` · `points` · `product` · `none` |
| `color` | `#RRGGBB` | | Çark dilim rengi; yoksa mobil tema paletinden sırayla boyar |
| `iconUrl` | string | | Gelecekte dilim/ödül ikonu (bugün çizilmiyor) |
| `description` | string | | Kısa koşul ("300 TL üzeri") |

- **Çarkta `prizes[]` sırası = dilim sırası** (saat yönünde, üstten başlar). 6–12 dilim önerilir; 12'den fazlasında etiketler küçülür.
- Salla Kazan'da liste bilgi amaçlıdır. Kazı Kazan'da `prizes[]` = kutucuklarda çıkabilecek değerlerin tanımı (`shortLabel` kutucuk metni, `color` kutucuk rengi); hücrelerin kendisi `play.cells[]` ile gelir.
- "Pas" dilimi için `kind: "none"`; mobil bu dilime düşünce kaybetti ekranı gösterir.

## 3. `POST /games/{code}/play` — istek / yanıt

İstek gövdesi: `{}` (kimlik token'da). İleride `{"sessionId": "…"}` eklenebilir; mobil boş nesne gönderir.

```json
{
  "success": true,
  "data": {
    "playId": "d1",
    "prizeId": "p3",
    "won": true,
    "prize": {
      "id": "p3",
      "label": "50 TL İndirim",
      "kind": "coupon",
      "description": "300 TL ve üzeri alışverişlerde geçerli",
      "couponCode": "CARK50-7K2M",
      "validUntil": "2026-09-18T20:59:59Z",
      "amountText": "50 TL"
    },
    "message": "Tebrikler!",
    "subMessage": "50 TL indirim kuponu kazandın. Kuponlarım'a eklendi.",
    "remainingPlays": 0,
    "nextPlayAt": "2026-09-12T00:00:00Z",
    "status": "cooldown",
    "statusLabel": "Yarın tekrar gel"
  }
}
```

| Alan | Zorunlu | Açıklama |
|---|---|---|
| `prizeId` | ✓ | **`prizes[]` içindeki bir id.** Çark bu dilime döner; listede yoksa ilk dilime döner (hata değil, ama yanlış görüntü) |
| `won` | ✓ | `false` + `prize.kind:"none"` = pas |
| `prize.couponCode` | | Kupon ödülünde kod; mobil "Kopyala" kutusu çizer. Kupon **aynı anda üyenin Kuponlarım listesine de eklenmeli** (`GET /account/coupons`) |
| `prize.validUntil` | | "Son kullanma: 18.09.2026" |
| `prize.amountText` | | Biçimlendirilmiş tutar; mobil hesaplamaz |
| `message` / `subMessage` | ✓ / | Sonuç paneli başlığı ve açıklaması; tamamen backend metni |
| `status` / `statusLabel` / `remainingPlays` / `nextPlayAt` | ✓ | Oyunun **oynandıktan sonraki** durumu; mobil ikinci istek atmadan bununla günceller (ikon kaybolur/kalır) |
| `cells[]` | kazı kazanda ✓ | **Tam 6 eleman**, sırayla 3×2 yerleşir: `{prizeId, label, color?}`. Kazanan kurguda 3 hücre `prizeId == data.prizeId`; kaybeden kurguda hiçbir değer 3 kez geçmemeli |

Kazı kazan örneği (kazanan): `"cells":[{"prizeId":"k2","label":"200 TL","color":"#16A34A"},{"prizeId":"k1","label":"%10"},{"prizeId":"k2","label":"200 TL"},{"prizeId":"k3","label":"Kargo"},{"prizeId":"k2","label":"200 TL"},{"prizeId":"k1","label":"%10"}]` → `prizeId:"k2", won:true`.

Hata durumları (400/409, `{success:false, error:"…"}`): hak yok, giriş gerekli, oyun bitti,
çok sık istek. Mobil `error` metnini snackbar'da gösterir ve oyunu oynatmaz.

**Kazı Kazan özel notu:** ilk kutucuğa dokunulduğu an `play` çağrılır (6 hücre folyonun
altına yazılır; her kutucuk yarısı kazınınca açılır, 6'sı açılınca sonuç paneli). Yani `play` =
hak kullanıldı; kullanıcı yarım bıraksa da ödül verilmiş sayılır. Kupon zaten Kuponlarım'a düşer.

## 3a. Kazanma kararı — ZORUNLU kurallar (mağaza sahibinin talebi, 2026-09-11)

Mobil hiçbir koşulda "kazandı mı?" sorusuna kendisi cevap vermez. Bu yüzden:

1. **`won` ve `prize` her `play` yanıtında zorunludur.** `won:true` ise `prize.kind != "none"` ve ödül
   (kupon/kargo/puan) **yanıt dönmeden önce** kullanıcının hesabına işlenmiş olmalı. `won:false` ise
   `prize.kind:"none"` gelir. Belirsiz/eksik yanıt = mobil hata gösterir, oyun oynatılmaz.
2. **Kupon ve kod kesin gelsin:** `prize.kind:"coupon"` ve `"free_shipping"` için `prize.couponCode`
   dolu olmalı ve aynı kupon `GET /account/coupons` listesinde görünmeli. Kod yoksa kullanıcı "kazandım
   ama elimde bir şey yok" durumuna düşer — kabul edilmez.
3. **Kazanabilir / kazanamaz durumu panelden yönetilir ve yanıtta kesinleşmiş gelir.** Mağaza sahibi
   oyunu tanımlarken şu iki moddan birini seçer:
   - **`alwaysWin: true` (herkes kazanır):** `prizes[]` içinde `kind:"none"` dilim/kutucuk OLMAZ;
     `play` asla `won:false` dönmez. Çarkta "Pas" dilimi çizilmez, kazı kazanda her kartta 3 aynı çıkar.
   - **`alwaysWin: false`:** "Pas" tanımlıdır; kaybetme oranı/kuralı panelde. Yine de sonuç yanıtta
     kesindir (`won`), mobil oran bilmez.
   `GET /games` yanıtına `alwaysWin` (bool) alanı eklensin — mobil bugün bunu okumuyor ama panel
   tarafında kuralın var olduğunun teyidi ve ileride "Kesin kazan!" rozeti için.
4. **Tutarlılık (backend'in garanti etmesi gereken):**
   - `prizeId` her zaman o oyunun `prizes[]` listesinde var.
   - Kazı kazanda `won:true` ⇔ `cells[]` içinde tam 3 hücre `prizeId == data.prizeId`;
     `won:false` ⇔ hiçbir değer 3 kez geçmez. (Mobil bunu hesaplamaz, sadece gösterir — uyumsuz gelirse
     ekranda "3 aynı çıktı ama kaybettin" gibi anlamsız görüntü olur.)
   - Çarkta `prizeId` bir "Pas" dilimiyse `won:false`, değilse `won:true`.
5. **Hak düşümü ve idempotency:** `play` çağrısı hak düşürür; ağ koparsa mobil aynı isteği tekrar
   ATMAZ (kullanıcı tekrar dokunur). Aynı gün/hak içinde ikinci `play` gelirse backend ya 409
   `{error:"Hakkını kullandın"}` dönsün ya da **aynı `playId` ile aynı sonucu** tekrar dönsün — asla
   ikinci bir ödül üretmesin.
6. **Kupon içeriği net olsun:** `prize.label` ("200 TL İndirim"), `prize.description` (koşul:
   "1000 TL üzeri"), `prize.validUntil`, `prize.amountText`. Mobil hesap yapmaz; kullanıcı ne
   kazandığını panelde yazıldığı gibi görür.

## 4. Giriş noktaları (mobilde hazır)

- **Ana sayfa yuvarlak ikon** (`flags.gamesEnabled` ile config'den kapatılabilir): aktif oyunu açar.
- Deep link / push `link`: `/oyunlar` (aktif oyun), `/oyunlar/{code}` (belirli oyun). Eş anlamlı: `/sans-oyunlari[/code]`.
- Ana sayfa **banner bloğu** `linkUrl: "/oyunlar"` ile de açılabilir — yeni blok tipi gerekmez.

## 5. Push senaryoları (sizin tarafta, mobil değişikliği yok)

Hak yenilenince ("Çarkın hazır!"), yeni oyun açılınca, kazanılan kupon bitmek üzereyken.
`data.link: "/oyunlar/cark"` verilmesi yeterli; mobil bildirime dokununca oyunu açar.

## 6. Yanıt vermenizi beklediğimiz kararlar

1. Misafir oynayabilir mi? (Öneri: hayır — `requiresLogin:true`; kupon üyeye bağlanır.)
2. Hak kuralı oyun başına panelden mi? (günde 1 / haftada n / sipariş başına 1) — `statusLabel` metinlerini de panelde yazılabilir yapın.
3. Kazanılan kupon `GET /account/coupons`'ta `couponType/discountText/endsAt` ile aynı biçimde geliyor mu? (Mobil kupon ekranı o sözleşmeyi kullanıyor.)
4. `prizes[].color` panelden mi? Boş bırakılırsa mobil paletle boyar; marka rengi isteniyorsa doldurun.
5. Kazı kazanda kaybeden kurgu nasıl olacak? (Öneri: 6 hücrede her değer en fazla 2 kez; `won:false`, `prize.kind:"none"`.) `alwaysWin` oyunlarda bu kurgu hiç üretilmeyecek.
6. İkon görseli (`imageUrl`) panelden yüklenecek mi, yoksa tip ikonu yeterli mi?
