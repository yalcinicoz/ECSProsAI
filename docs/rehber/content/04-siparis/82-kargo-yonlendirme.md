---
title: Kargo Yönlendirme
route: /fulfillment/cargo-reroute
group: Sipariş Yönetimi
order: 82
summary: Paketlerin kargo bildirim kuyruğunu taşıyıcı bazında izleme ve henüz gönderilmemiş bildirimleri toplu/tekil olarak başka taşıyıcıya yönlendirme.
---

## Ne işe yarar
Paket kapanınca (Hızlı Hat ya da Masa'da) paket için kargo gönderi kaydı açılır ve taşıyıcıya yapılacak bildirim bir
**kuyruğa** düşer. Kargo Yönlendirme bu kuyruğu taşıyıcı bazında gruplu gösterir; bekleyen veya hatalı bildirimleri
seçip **başka bir taşıyıcıya** yönlendirmenizi sağlar (örn. taşıyıcı o gün hizmet veremiyorsa). Gönderilmiş kayıtlar
yalnız izlenir. Sipariş anında seçilen kargo şirketi sipariş kaydında kesinleşir; yönlendirme bu seçimi paket bazında değiştirir.

## Ekran yerleşimi
![Kargo Yönlendirme — sekmeler, hedef taşıyıcı şeridi, taşıyıcı gruplu liste](img/fulfillment-cargo-reroute.webp)
1. **Başlık** — "Kargo Yönlendirme", "Kargo bildirim kuyruğu — N kayıt".
2. **Durum sekmeleri** — Bekleyen / Hatalı / Gönderilen.
3. **Seçim şeridi** (Bekleyen ve Hatalı sekmelerinde) — seçili paket sayısı, **Hedef Taşıyıcı** kutusu, **Seçilenleri Yönlendir**.
4. **Taşıyıcı grupları** — her taşıyıcı için başlık ("🚚 Taşıyıcı adı (N paket)") + "Tümünü Seç" ve paket satırları.
5. **Bilgi notu** — otomatik gönderimin durumu.

Liste 30 saniyede bir kendiliğinden yenilenir.

## Liste ve filtreler
| Sekme | Ne gösterir |
|---|---|
| Bekleyen | Taşıyıcıya henüz bildirilmemiş (`pending`) kayıtlar — seçilip yönlendirilebilir. |
| Hatalı | Gönderim denemesi başarısız (`failed`) kayıtlar — seçilip yönlendirilebilir; son hata kırmızı yazar. |
| Gönderilen | Taşıyıcıya iletilmiş (`sent`) kayıtlar — yalnız izlenir, seçim yoktur. |

| Satır öğesi | Anlamı |
|---|---|
| (onay kutusu) | Paketi yönlendirme için seçer (Bekleyen/Hatalı). |
| Paket no | Paket numarası. |
| Sipariş (bağlantı) | İlgili sipariş detayını açar. |
| Deneme: N | Taşıyıcıya gönderim deneme sayısı. |
| Son hata | Son gönderim hatası (hatalı kayıtlarda kırmızı). |
| Tarih | Bekleyen/Hatalı'da kaydın oluşma zamanı, Gönderilen'de "Gönderim: …" zamanı. |
| Grup başlığı | Taşıyıcı adı ve paket sayısı; taşıyıcısı olmayan kayıtlar "Taşıyıcı atanmamış" grubunda. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Sekme değiştirme | Üst | Listeyi filtreler; seçim ve mesajlar sıfırlanır. | — |
| Tümünü Seç | Grup başlığı | Gruptaki tüm paketleri seçer / seçimi kaldırır. | Bekleyen/Hatalı sekmesi. |
| Hedef Taşıyıcı | Seçim şeridi | Aktif kargo entegrasyonlarından hedef seçilir ("Firma — Taşıyıcı" biçiminde). | — |
| Seçilenleri Yönlendir | Seçim şeridi | Onay penceresi: "N paket … taşıyıcısına yönlendirilecek. Devam edilsin mi?" | En az 1 paket seçili + hedef seçili. |
| Yönlendir | Onay penceresi | Seçili bildirimler hedef taşıyıcıya taşınır; ilgili siparişlerin kargo şirketi de güncellenir; "N paket … yönlendirildi." mesajı. | Seçilenler gönderilmemiş olmalı; gönderilmiş kayıt varsa "… kayıt taşıyıcıya zaten gönderilmiş — iptal+yeniden gönderim gerçek API entegrasyonuyla gelecek." hatası. |
| Vazgeç | Onay penceresi | Pencere kapanır. | — |
| Sipariş | Satır | Sipariş detayına gider. | — |

## Durumlar ve iş kuralları
| Durum | Anlamı |
|---|---|
| `pending` | Kuyrukta, gönderilmedi. |
| `failed` | Gönderim denendi, hata alındı. |
| `sent` | Taşıyıcıya iletildi. |
| `cancelled` | İptal edildi (listede sekme yok). |

- Kargo bildirimi **varsayılan olarak paket gerçekten oluştuğunda** kuyruğa düşer (paket kapanışı). Kanal politikasıyla
  "sipariş anında" seçeneği ileride gelecektir.
- **Otomatik gönderim kuyruğu varsayılan olarak kapalıdır:** gerçek taşıyıcı entegrasyonları devreye alınana kadar
  kayıtlar kuyrukta birikir; sayfanın altındaki bilgi notu bunu hatırlatır. Kapalıyken kayıtlar `Bekleyen`'de kalır; bu
  bir hata değildir.
- Yönlendirme yalnız **gönderilmemiş** (bekleyen/hatalı) kayıtlarda yapılır. Gönderilmiş paketin taşıyıcısını değiştirme
  (eski taşıyıcıda iptal + yeniden gönderim) gerçek entegrasyonla birlikte açılacaktır.
- Yönlendirme siparişin istenen kargo şirketini de günceller; paket etiketi/kargo kodu geçmişi korunur. İşlem operasyon geçmişine yazılır.

## Adım adım
**Bekleyen paketleri başka taşıyıcıya yönlendirme**
1. Sol menüden **Sipariş Yönetimi → Kargo Yönlendirme**'yi açın, **Bekleyen** sekmesinde kalın.
2. İlgili taşıyıcı grubunda **Tümünü Seç** ya da tek tek onay kutularını işaretleyin.
3. **Hedef Taşıyıcı** kutusundan yeni taşıyıcıyı seçin, **Seçilenleri Yönlendir → Yönlendir**.
4. Yeşil "N paket … yönlendirildi." mesajını görün; paketler yeni taşıyıcının grubuna geçer.

**Hatalı bildirimleri gözden geçirme**
1. **Hatalı** sekmesine geçin; satırdaki kırmızı son hata metnini okuyun (üzerine gelince tamamı görünür).
2. Sorun taşıyıcıdaysa aynı adımlarla başka taşıyıcıya yönlendirin.

## İpuçları ve sık karşılaşılan durumlar
> **Not:** Bekleyen sekmesi boşsa ("Bekleyen bildirim yok.") henüz paket kapanmamıştır; kuyruk paket oluştukça dolar.

> **Dikkat:** Gönderilen sekmesindeki kayıtlar seçilemez; gönderilmiş paket için yönlendirme denemesi hata verir.

> **İpucu:** Hedef Taşıyıcı listesi firmaların aktif kargo entegrasyonlarından gelir; aradığınız taşıyıcı yoksa firma entegrasyon tanımlarını kontrol edin.

## İlgili sayfalar
- [Hızlı Hat](/rehber/siparis/hizli-hat/)
- [Masa ve Paketleme](/rehber/siparis/masa-ve-paketleme/)
- [Siparişler](/rehber/siparis/siparisler/)
- [Sipariş Detayı — Operasyon Geçmişi](/rehber/siparis/siparis-detay/)
