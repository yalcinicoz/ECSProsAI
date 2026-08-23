---
title: Ürün Toplama
route: /fulfillment/my-picking
group: Sipariş Yönetimi
order: 72
summary: Depo personelinin kendisine atanan toplama satırlarını raf sırasıyla görüp barkod okuyucuyla topladığı mobil ekran.
---

## Ne işe yarar
Ürün Toplama, depo personelinin telefon/tablet tarayıcısında kullandığı kişisel toplama ekranıdır. Size atanmış
toplama görevleri listelenir; bir görevi seçince toplanacak satırlar **raf sırasıyla** (depodaki yürüyüş rotası)
tek tek gelir. Bluetooth barkod okuyucu (klavye modunda çalışan okuyucu) ile ürün barkodunu okutursunuz; ekran
büyük yazı, büyük dokunma alanları ve sesli geri bildirimle çalışır. Satırların kime atandığı
[Toplama Planlama](/rehber/siparis/toplama-planlama/) ekranında belirlenir.

## Ekran yerleşimi
![Ürün Toplama — görev listesi ve toplama ekranı](img/fulfillment-my-picking.webp)
1. **Görev listesi** — "Sana atanmış toplama görevleri" kartları: plan numarası ve kalan satır sayısı. 30 saniyede bir yenilenir.
2. **Toplama ekranı (görev seçilince)** — üst şerit (geri oku, plan no, kalan satır, "Okutmaya hazır ●" rozeti), hata/başarı şeridi, **sıradaki satır** büyük kartı, soluk **Sıradakiler** listesi.
3. **Görünmez okutma alanı** — ekranın odağı sürekli barkod alanında tutulur; okuyucu okuduğunu buraya yazar ve Enter ile gönderir. Ekrana dokunmanız gerekmez.

## Liste ve filtreler
| Öğe | Anlamı |
|---|---|
| Görev kartı | Plan numarası (örn. `PICK-20260809-A1B2C3`) ve "kalan N satır" ya da "tüm satırlar bitti ✓". |
| Boş durum | "Sana atanmış görev yok" — yeni görev atandığında 30 sn içinde listede belirir. |

Listede yalnız `Bekliyor` ve `Toplanıyor` durumundaki görevlerde size atanmış satırı olanlar görünür. Filtre yoktur.

## Toplama ekranı alanları
| Alan | Anlamı |
|---|---|
| Raf | Sıradaki satırın önerilen raf kodu — ekranın en büyük yazısı (örn. `KAT-1K3-0718`). |
| Ürün adı + SKU | Toplanacak ürün ve varyant bilgisi. |
| Sipariş | Satırın ait olduğu sipariş numarası. |
| toplanan/istenen adet | Örn. `0/2 adet`; her başarılı okutma 1 artırır. |
| Sıradakiler | Sonraki 4 satır (raf, ürün, adet) soluk olarak. |
| Kalan N satır | Üst şeritte, size atanmış tamamlanmamış satır sayısı. |
| ✓ yeşil şerit | Son başarılı okutma: ürün adı ve `toplanan/istenen`. |
| ⚠ kırmızı şerit | Son hata mesajı. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Görev kartına dokun | Görev listesi | Toplama ekranı açılır. | — |
| ← (geri) | Toplama ekranı üst şerit | Görev listesine döner, liste yenilenir. | — |
| Barkod okutma (ürün) | Her an | Barkod, size atanan ve bu ürünle eşleşen en düşük rota sıralı satıra +1 yazar. Başarıda kısa tiz bip + yeşil şerit; satır tamamlanınca sonraki satıra geçilir. | Satır size atanmış olmalı; aksi halde "Bu barkod size atanan toplanacak ürünlerle eşleşmedi." + hata sesi. |
| Farklı raftan aldım (onay kutusu) | Sıradaki satır kartı | İşaretlenince önce **RAF** barkodu, sonra **ÜRÜN** barkodu okutulur; toplanan raf olarak okutulan raf kaydedilir. Raf okunduğunda bip çalar ve "Raf okundu: … — şimdi ÜRÜN barkodunu okut." yazar. | Raf barkodu tanımlı olmalı; aksi halde "Raf barkodu tanınmadı." |
| Bulunamadı | Sıradaki satır kartı | Onay kutusu açılır: "Bu satır 'bulunamadı' olarak işaretlenecek. Emin misin?" | — |
| Evet, Bulunamadı ⚠️ | Onay kutusu | Satır `Eksik` (short) olur ve listeden düşer; geri alınamaz. | Satır size atanmış ve açık olmalı. |
| Vazgeç | Onay kutusu | Onay kapanır. | — |
| Görev Listesine Dön | Kutlama ekranı | Tüm satırlar bitince görünen ekrandan listeye döner. | — |

## Sesler
| Ses | Ne zaman |
|---|---|
| Kısa tiz bip | Başarılı ürün okutması; "Farklı raftan aldım" modunda raf barkodu okunduğunda. |
| Çift pes bip (uzun, belirgin) | Hatalı okutma (eşleşmeyen barkod, tanınmayan raf) ve başarısız "bulunamadı" işlemi. |
| Sesli "Görev tamamlandı" | Size atanan son satır da toplandığında. |

> **Not:** Tarayıcılar sesi ilk dokunma/tuş vuruşundan sonra açar; ekrana bir kez dokunmak yeterlidir.

## Durumlar ve iş kuralları
| Satır durumu | Anlamı |
|---|---|
| `Bekliyor` / `Atandı` | Toplanacak (açık) satır — ekranda sırayla gelir. |
| `Toplandı` | İstenen adet okutuldu. |
| `Eksik` | "Bulunamadı" işaretlendi. |

- Satırlar rota (raf) sırasıyla gelir; aynı ürün birden çok satırda varsa okutma **en düşük rota sıralı** açık satıra işlenir.
- Her okutma 1 adet sayar; 2 adetlik satır için ürünü iki kez okutursunuz.
- Okutulan satır için **fiilen toplanan raf** kaydedilir: "Farklı raftan aldım" işaretlenmediyse önerilen raf, işaretlendiyse okutulan raf. Stok, fiilen toplanan raftan düşer ve stok hareketi olarak kaydedilir.
- Görevin tamamlandığını ("Görevin bitti!") görürsünüz; görev kaydının kapatılması (Tamamla) Toplama Planlama ekranından yapılır.
- Her okutma ve "bulunamadı" işlemi sipariş operasyon geçmişine personel ve zamanla yazılır.

## Adım adım
**Bir görevi toplama**
1. Sol menüden **Sipariş Yönetimi → Ürün Toplama**'yı açın; görev kartına dokunun.
2. Ekrandaki **Raf** koduna gidin, ürünü alın ve barkodunu okutun. Yeşil şerit ve bip geldiyse sıradaki satıra geçin.
3. Ürünü başka raftan aldıysanız önce **Farklı raftan aldım**'ı işaretleyin, raf barkodunu, sonra ürün barkodunu okutun.
4. Ürün rafta yoksa **Bulunamadı → Evet, Bulunamadı** ile satırı eksik işaretleyin.
5. Son satır bitince "Görevin bitti!" ekranından **Görev Listesine Dön**.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Okuyucu yazmıyorsa ekrana bir kez dokunun; odak otomatik olarak okutma alanına döner. Tarayıcıda başka sekme açıksa bu sekmeye dönün.

> **Dikkat:** "Bu barkod size atanan toplanacak ürünlerle eşleşmedi." — ürün başka personele atanmış, satır zaten tamamlanmış ya da yanlış ürün okutulmuş olabilir. Ekrandaki SKU ile karşılaştırın.

> **Dikkat:** "Bulunamadı" geri alınamaz; satır `Eksik` olur ve siparişin o kalemi eksik kalır. Sorun raftaki yanlış yerleşimse önce çevre rafları kontrol edin.

## İlgili sayfalar
- [Toplama Planlama](/rehber/siparis/toplama-planlama/)
- [Hızlı Hat](/rehber/siparis/hizli-hat/)
- [Ara Ayrıştırma ve Koli Duvarı](/rehber/siparis/ara-ayristirma/)
