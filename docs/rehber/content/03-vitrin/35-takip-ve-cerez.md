---
title: Takip & Çerez
route: /storefront/tracking-consent
group: Vitrin
order: 35
summary: Sitedeki çerez bandı metinlerinin, satın alma ölçüm anının ve izin istatistiğinin kanal bazında yönetildiği; KVKK/GDPR aydınlatma metni şablonunun alındığı ekran.
---

## Ne işe yarar
Sitede ziyaretçiye gösterilen **çerez bandının** başlık/açıklama/politika linkini kanal bazında düzenler, reklam ve
analitik platformlarına "satın alma" olayının hangi anda gönderileceğini belirler ve son 30 günün izin
tercihlerinin dağılımını gösterir. Ayrıca gizlilik/aydınlatma sayfanıza eklenecek hazır bir ek madde şablonu
sunar. Pazarlama ve hukuk/uyum sorumlusu kullanır. Entegrasyonların (Google Analytics, Meta vb.) bağlantı
durumu ise Pazarlama → Takip & Reklam sayfasındadır.

## Ekran yerleşimi
![Takip & Çerez — band metinleri, izin istatistiği ve KVKK şablonu](img/storefront-tracking-consent.webp)
1. **Başlık şeridi** — açıklama, Takip & Reklam sayfasına bağlantı ve sağda **kanal seçici** (açılır liste,
   "Firma — Kanal"; ilk kanal otomatik seçilir).
2. **Bilgi rozetleri** — bandın durumu ve varsayılan politika (değiştirilemez bilgi).
3. **Band metinleri** kartı (sol, geniş) — form alanları ve **Kaydet**.
4. **İzin istatistiği (son 30 gün)** kartı (sağ).
5. **KVKK / GDPR aydınlatma metni — ek madde şablonu** kartı (alt) — metin ve **Kopyala**.

## Liste ve filtreler
| Rozet | Anlamı |
|---|---|
| `Çerez bandı: AÇIK (kapatılamaz — EU/KVKK kararı)` | Band her kanalda her zaman gösterilir; bu ekrandan kapatılamaz, yalnız metinleri düzenlenir. |
| `Varsayılan: tüm kategoriler REDDEDİLMİŞ (Consent Mode v2)` | Ziyaretçi seçim yapana kadar analitik/reklam/kişiselleştirme çerezleri çalışmaz. |
| `Kategoriler: Analitik · Reklam · Kişiselleştirme (+ zorunlu)` | Bandda sunulan izin kategorileri. |

**İzin istatistiği (son 30 gün)** kartı — kayıt yoksa "Henüz kayıt yok — band gösterildikçe tercihler burada birikir."
| Satır | Anlamı |
|---|---|
| Toplam tercih | Kaydedilen tercih sayısı. |
| Tümünü kabul / Tümünü red | Adet ve yüzde. |
| Kısmi | Bazı kategorileri kabul edenler. |
| Analitik izni / Reklam izni | İlgili kategoriye izin verenlerin yüzdesi. |
| Üye eşleşmeli | Tercihi bir üye hesabıyla eşleşen kayıt sayısı. |

Kart altındaki not: tercih günlüğü ispat amacıyla 12 ay saklanır; IP adresi yalnız geri döndürülemez biçimde tutulur.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kaydet | Band metinleri kartı | Metinleri ve satın alma anını kanala yazar; "Kaydedildi — vitrinde en geç 2 dk içinde etkili olur." mesajı görünür. Hata olursa nedeni aynı yerde yazılır. | Kanal seçili. |
| Kopyala | KVKK şablonu kartı | Şablon metnini panoya kopyalar. | — |
| Pazarlama → Takip & Reklam bağlantısı | Başlık altı | Entegrasyon durumu sayfasına gider. | — |
| Kanal seçici | Sağ üst | Seçili kanalın ayarları ve istatistiği yüklenir; seçim oturumda hatırlanır. | — |

## Form alanları
| Alan | Zorunlu | Açıklama / kurallar / örnek |
|---|---|---|
| Başlık | Hayır | Band başlığı; boş bırakılırsa `Çerez tercihleriniz`. |
| Açıklama | Hayır | Band metni (çok satırlı); boşsa varsayılan metin ("Alışveriş deneyiminizi iyileştirmek, site trafiğini analiz etmek ve size uygun reklamlar göstermek için çerezler kullanıyoruz…"). |
| Politika sayfası linki | Hayır | Banddaki politika bağlantısının adresi; boşsa `/gizlilik-ve-guvenlik`. |
| Link metni | Hayır | Bağlantının yazısı; örn. `Gizlilik ve Çerez Politikası`. |
| Sunucu taraflı "satın alma" event anı | Evet | `Sipariş onaylandığında (varsayılan — ödeme alındı/onaylandı)` ya da `Sipariş oluşturulduğunda`. Reklam/analitik platformlarına satın alma olayının gönderileceği an. |

## Durumlar ve iş kuralları
- Band her zaman açıktır ve varsayılan "reddedilmiş"tir; ziyaretçi izin vermeden analitik/reklam çerezleri
  çalışmaz, veri aktarımı yapılmaz. Ziyaretçi tercihini sayfa altındaki "Çerez Tercihleri" bağlantısından
  sonradan değiştirebilir.
- Ayarlar kanal bazlıdır; kaydedilen metin sitede en geç 2 dakika içinde görünür.
- İzin tercihleri 12 ay saklanır (ispat günlüğü); istatistik son 30 günü özetler.
- Satın alma anı `Sipariş onaylandığında` seçiliyse ödeme alınmamış/onaylanmamış siparişler platformlara
  "satın alma" olarak gitmez.

## Adım adım
**Band metnini özelleştirme**
1. Sağ üstten kanalı seçin.
2. Başlık, Açıklama, Politika sayfası linki ve Link metni alanlarını doldurun.
3. **Kaydet**; "Kaydedildi" mesajını bekleyin ve 2 dakika sonra sitede kontrol edin.

**Aydınlatma metnine çerez maddesi ekleme**
1. KVKK / GDPR kartında **Kopyala**'ya basın.
2. Metni hukuk onayından geçirin; İçerik → Sayfalar'daki gizlilik/aydınlatma sayfasına yapıştırın.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** Bandı kapatma seçeneği bilinçli olarak yoktur; mevzuat gereği her ziyaretçiye gösterilir.

> **Not:** "Henüz kayıt yok" istatistik kartında normaldir — site yayına girip band gösterildikçe dolar.

> **İpucu:** Reklam/analitik hesaplarının bağlanması ve test edilmesi için Pazarlama → Takip & Reklam sayfasını
> kullanın; bu sayfa yalnız band ve izin tarafını yönetir.

## İlgili sayfalar
- [Takip & Reklam](/rehber/pazarlama/takip-ve-reklam/)
- [Sayfalar (İçerik)](/rehber/icerik/sayfalar/)
- [Ürün Kartı](/rehber/vitrin/urun-karti/)
