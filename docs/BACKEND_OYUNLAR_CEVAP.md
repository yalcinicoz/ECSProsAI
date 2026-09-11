# Şans Oyunları — Backend cevabı (docs/BACKEND_OYUNLAR.md rev.2 için) · 2026-09-11 · UYGULANDI

Uçlar sözleşmedeki biçimde açıldı; alan adları birebir. Uç referansı: `docs/mobil-api-referansi.md` §17. Panel: Pazarlama › Şans Oyunları.

## §6 kararları
1. **Misafir oynayabilir mi?** Hayır. Tüm oyunlar `requiresLogin: true`; misafir (cihaz token'ı) listeyi görür, `status: "login_required"` alır, `play` 401 `{success:false, error:"Oynamak için giriş yapmalısınız."}` döner. Gerekçe: kupon üyeye bağlanır; cihaz token'ı kısa ömürlü ve anonim, hak takibi yapılamaz.
2. **Hak kuralı panelden mi?** Evet, oyun başına: dönem `day` (İstanbul günü) / `week` (ISO hafta) / `total` + hak sayısı. `statusLabel` metinlerinin hepsi panelde ({n}, {next}, {prize} yer tutucuları). "Sipariş başına 1" bu turda yok.
3. **Kupon biçimi:** kazanılan kupon `GET /account/coupons`'ta aynı sözleşmeyle (`couponType`, `discountText`, `endsAt`, `minimumCartTotal`) görünür; kişiye özel (`MemberId`), tek kullanımlık. `play.prize.couponCode` ile aynı kod.
4. **`prizes[].color`:** panelden (renk seçici); boş bırakılırsa mobil palet.
5. **Kazı kazan kaybeden kurgu:** öneri kabul edildi — 6 hücrede her değer en fazla 2 kez, `won:false`, `prize.kind:"none"`. Kazanan kurguda kazanan tam 3 hücre, kalan 3 hücre başka ödüllerden ve hiçbiri 3 kez geçmez. Panel kaydında en az 3 farklı kazandıran ödül zorunlu. `alwaysWin` oyunlarda kaybeden kurgu hiç üretilmez.
6. **`imageUrl`:** panelden URL alanı (dosya yükleme bu turda yok); boşsa tip ikonu.

## §3a garantileri
- `won` ve `prize` her yanıtta var; `won:true` ⇒ kupon/puan **yanıt dönmeden önce** işlenmiş; `won:false` ⇒ `prize.kind:"none"`.
- `prizeId` her zaman o oyunun `prizes[]` listesinde.
- **İdempotency:** hak bitmişken gelen ikinci `play`, o dönemin son oynanışını **aynı `playId` ve sonuçla** döner; ikinci ödül asla üretilmez.
- Ödül türleri v1: `coupon` (percentage/fixed), `points`, `none`. `free_shipping` ve `product` panelde seçilemez (kupon motoru desteklemiyor) — istenirse sonraki tur.
- `alwaysWin` alanı `GET /games` yanıtında.

## Bu turda yapılmayanlar
- Push senaryoları ("Çarkın hazır!", yeni oyun) — bildirim şablonu + `data.link` ile ayrı tur.
- Ana sayfa banner bloğu bağlantısı `linkUrl:"/oyunlar"` — mobil tarafında; backend değişikliği gerekmez.
