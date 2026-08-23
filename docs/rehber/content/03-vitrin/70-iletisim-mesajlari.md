---
title: İletişim Mesajları
route: /storefront/contact-messages
group: Vitrin
order: 70
summary: Sitedeki iletişim formundan gelen mesajların gelen kutusu; yeni/okundu takibi, platform ve arama filtresi, mesaj detayı.
---

## Ne işe yarar
Sitenin **İletişim** formundan gönderilen mesajlar burada bir gelen kutusu gibi toplanır. Müşteri ilişkileri
sorumlusu yeni mesajları okur, gönderene e-posta/telefonla döner ve mesajı okundu olarak bırakır. Sayfa sol menüde
**Müşteriler** bölümünün altında yer alır (rehberde Vitrin bölümünde anlatılır çünkü mesajlar site vitrininden
gelir). Panelden yanıt gönderme özelliği yoktur; iletişim e-posta bağlantısı üzerinden yapılır.

## Ekran yerleşimi
![İletişim Mesajları — sekmeler, arama ve mesaj tablosu](img/storefront-contact-messages.webp)
1. **Başlık** — toplam kayıt sayısı; sağda **platform (kanal) filtresi** açılır listesi.
2. **Sekmeler** — Yeni · Okundu · Tümü (açılışta Yeni).
3. **Arama kutusu** + **Ara**.
4. **Mesaj tablosu**; altta `← Önceki` / `N / M` / `Sonraki →` sayfalama (20 satır/sayfa).
5. **Mesaj penceresi** — satıra tıklayınca açılır.

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| TARİH | Gönderim tarih-saati. |
| GÖNDEREN | Formdaki ad; yeni mesajlarda kalın. |
| E-POSTA | Gönderenin e-postası. |
| KONU | Konu; boşsa mesajın ilk 60 karakteri gri gösterilir. |
| PLATFORM | Mesajın geldiği kanal (site). |
| DURUM | `Yeni` (sarı) / `Okundu` (gri). |

| Filtre | Ne yapar |
|---|---|
| Sekmeler (Yeni / Okundu / Tümü) | Duruma göre süzer. |
| Platform listesi | `Tüm platformlar` ya da tek kanal. |
| Ad, e-posta veya konu ara… | Yazıp **Ara** ya da Enter. |

Satıra tıklayınca mesaj penceresi açılır; **Yeni** mesaj açılır açılmaz otomatik `Okundu` olur. Boş liste:
"Okunmamış mesaj yok." (Yeni sekmesi) ya da "Mesaj yok.".

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Satır tıklama | Tablo | Mesaj penceresi açılır; yeni mesaj okundu işaretlenir. | — |
| Okundu işaretle / Yeni olarak işaretle | Mesaj penceresi, sol alt | Durumu `Okundu` ↔ `Yeni` çevirir (yanlışlıkla açılan mesajı tekrar "Yeni" yapmak için). | — |
| Kapat | Mesaj penceresi | Pencereyi kapatır. | — |
| E-posta bağlantısı | Mesaj penceresi | Gönderene e-posta yazmak için posta programınızı açar. | — |
| ← Önceki / Sonraki → | Tablo altı | Sayfalar arası geçiş. | Birden çok sayfa varsa. |

## Mesaj penceresi alanları
| Alan | Açıklama |
|---|---|
| Başlık | Konu; konu yoksa "İletişim Mesajı". |
| Gönderen | Formdaki ad. |
| Tarih | Gönderim tarih-saati. |
| E-posta | Tıklanabilir `mailto:` bağlantısı. |
| Telefon | Varsa; yoksa `—`. |
| Platform | Mesajın geldiği kanal. |
| Üye | Formu dolduran giriş yapmış üyeyse kısa üye kimliği, değilse `Misafir`. |
| Mesaj | Tam metin (satır sonları korunur). |

## Durumlar ve iş kuralları
- İki durum vardır: `Yeni` → `Okundu`; geri `Yeni` yapılabilir. Başka durum (yanıtlandı vb.) yoktur.
- Mesajı açmak otomatik okundu yapar; okunmamış takibi için listeyi açmadan önce sekmelere bakın.
- Mesajlar silinmez; `Tümü` sekmesinde arşiv gibi kalır.

## Adım adım
**Günlük gelen kutusu kontrolü**
1. **Yeni** sekmesinde satırları sırayla açın; gerekirse E-posta bağlantısıyla yanıt verin.
2. Daha sonra dönmek istediğiniz mesajda **Yeni olarak işaretle** deyin.
3. Belirli bir kanalın mesajları için sağ üstten platformu seçin.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Arama ad, e-posta ve konuda çalışır; mesaj metninde aramaz.

> **Not:** Toplam sayaç başlıkta seçili sekme+filtreye göre değişir ("N kayıt — site iletişim formundan gelen mesajlar").

## İlgili sayfalar
- [Üyeler](/rehber/musteriler/uyeler/)
- [Vitrin Yönetimi](/rehber/vitrin/vitrin-yonetimi/)
