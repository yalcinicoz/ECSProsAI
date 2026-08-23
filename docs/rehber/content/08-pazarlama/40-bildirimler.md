---
title: Bildirimler
route: /storefront/notifications
group: Pazarlama
order: 40
summary: Müşterilerin sitede bıraktığı "stok gelince haber ver" alarmlarının ve bildirimli kayıtlı aramaların izlendiği, kayıtlı arama taramasının elle tetiklenebildiği ekran.
---

## Ne işe yarar
Müşteriler sitede iki tür otomatik bildirim kaydı bırakır: tükenen bir ürün/varyant için **stok alarmı** ("Stok gelince
haber ver") ve Hesabım → Favori Aramalarım'dan **kayıtlı arama** (bildirim açıksa sorguya uyan yeni ürün eklenince
e-posta). Gönderimler otomatik koşar; bu ekran operasyon ekibinin kayıtları, gönderim durumlarını ve zamanlarını
izlemesi, gerekirse kayıtlı arama taramasını hemen çalıştırması içindir. Kayıt ekleme/düzenleme yapılmaz.

## Ekran yerleşimi
![Bildirimler — Stok Alarmları sekmesi](img/storefront-notifications.webp)
1. **Başlık** — "Bildirimler" ve "Stok alarmı ve kayıtlı arama bildirimlerinin izlemesi — gönderimler otomatik koşar".
2. **Sağ üst araçlar** — tarama sonucu mesajı, **Şimdi Tara** butonu, platform seçici ("Tüm platformlar").
3. **Sekmeler** — `Stok Alarmları` / `Kayıtlı Aramalar`.
4. **Filtre şeridi** — durum seçici + arama kutusu + **Ara** + sağda kayıt sayısı.
5. **Tablo** ve sayfalama (20 kayıt/sayfa). Satırlar tıklanmaz.

## Sekmeler

### Stok Alarmları
| Sütun | Anlamı |
|---|---|
| TARİH | Müşterinin alarmı bıraktığı zaman. |
| ÜRÜN KODU | Alarm bırakılan ürünün kodu. |
| VARYANT | Renk/beden bilgisi. |
| E-POSTA | Bildirim gidecek adres. |
| DURUM | `Bekliyor` (sarı) · `Bildirildi` (yeşil) · `İptal` (gri). |
| BİLDİRİM ZAMANI | E-postanın gönderildiği zaman; gönderilmediyse `—`. |

| Filtre | Ne yapar |
|---|---|
| Durum seçici | `Bekleyenler` (varsayılan) / `Bildirilenler` / `İptal Edilenler` / `Tümü`. |
| E-posta veya ürün kodu ara… + Ara | E-posta ya da ürün koduna göre arar. |
| Platform seçici (sağ üst) | Yalnız seçilen kanalın kayıtları. |

### Kayıtlı Aramalar
| Sütun | Anlamı |
|---|---|
| TARİH | Aramanın kaydedildiği zaman. |
| AD | Müşterinin aramaya verdiği ad. |
| SORGU | Arama ifadesi. |
| BİLDİRİM | `Açık` (yeşil) / `Kapalı` (gri) — müşterinin e-posta tercihi. |
| SON BİLDİRİM | Son e-posta zamanı; hiç gönderilmediyse "Henüz gönderilmedi". |

| Filtre | Ne yapar |
|---|---|
| Bildirim seçici | `Bildirim Açık` (varsayılan) / `Bildirim Kapalı` / `Tümü`. |
| Arama sorgusu veya ad ara… + Ara | Sorgu metni ya da ada göre arar. |
| Platform seçici (sağ üst) | Yalnız seçilen kanalın kayıtları. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Şimdi Tara | Sağ üst | Kayıtlı arama taraması beklemeden çalışır; bitince "Tarama tamamlandı — N e-posta gönderildi." (hata: "Tarama başarısız oldu.") mesajı görünür. Günde-1 sınırı korunur, yinelenen e-posta üretmez. | Panele giriş yeterli. |
| Platform seçici | Sağ üst | Her iki sekmeyi kanala göre süzer. | — |
| Ara | Filtre şeridi | Yazılan metni uygular (Enter da çalışır). | — |

## Durumlar ve iş kuralları
**Stok alarmı**
- Müşteri tükenen üründe "Stok gelince haber ver" bırakınca kayıt `Bekliyor` açılır.
- İlgili varyanta stok girişi olduğunda sistem bekleyen kayıtlara **bir kez** "Stokta! Ürün adı" e-postası gönderir ve
  durumu `Bildirildi` yapar. Gönderim başarısızsa kayıt `Bekliyor` kalır, sonraki stok hareketinde yeniden denenir.
- E-posta adresi olmayan kayıt gönderim sırasında `İptal` edilir.
- Sol menüdeki Favoriler panelinde "Stok Uyarıları" rozeti bekleyen alarm sayısını gösterir.

**Kayıtlı arama**
- Tarama açılıştan kısa süre sonra ve ardından periyodik (varsayılan 6 saatte bir) otomatik koşar.
- Yalnız BİLDİRİM `Açık` kayıtlar taranır; son bildirimden (hiç yoksa son 24 saatten) bu yana sorguya uyan **yeni ürün**
  eklendiyse "… aramanıza yeni ürünler eklendi" e-postası gider ve SON BİLDİRİM güncellenir.
- Aynı kayda **günde en fazla bir** e-posta gider; bu yüzden **Şimdi Tara** tekrar tekrar basılsa da mükerrer gönderim
  olmaz. Gönderim başarısızsa SON BİLDİRİM ilerlemez, sonraki turda yeniden denenir.
- Müşteri bildirimi Hesabım → Favori Aramalarım'dan kapatabilir; kapalı kayıtlar `Bildirim Kapalı` süzgecinde görünür.

## Adım adım
**Bir müşterinin stok alarmı neden gitmedi?**
1. `Stok Alarmları` → durum `Tümü`, e-postayı aratın.
2. DURUM `Bekliyor` ise ürüne henüz stok girişi olmamıştır; `İptal` ise kayıtta e-posta yoktu.
3. `Bildirildi` ise BİLDİRİM ZAMANI'nı müşteriye iletin (spam klasörü kontrolü).

**Yeni ürün yüklemesinden sonra kayıtlı arama e-postalarını hemen göndermek**
1. Ürünleri yayınladıktan sonra **Şimdi Tara**'ya basın; mesajda gönderilen e-posta sayısı görünür.

## İpuçları ve sık karşılaşılan durumlar
> **Not:** "Tarama tamamlandı — 0 e-posta gönderildi." normaldir: ya yeni ürün yoktur ya da tüm kayıtlara son 24 saatte
> zaten gönderilmiştir.

> **İpucu:** Platform seçici "Tüm platformlar" iken sayaçlar tüm kanalların toplamıdır; kanal bazlı rapor için önce
> kanalı seçin.

## İlgili sayfalar
- [Bülten Aboneleri](/rehber/pazarlama/bulten-aboneleri/)
- [Üyeler](/rehber/musteriler/uyeler/)
