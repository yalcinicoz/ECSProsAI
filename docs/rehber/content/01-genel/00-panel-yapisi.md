---
title: Giriş ve Panel Yapısı
route: /
group: Genel
order: 0
summary: Panele giriş, ekranın genel bölümleri (sol menü, üst çubuk, içerik alanı), ortak davranışlar ve yetkiler.
---

## Ne işe yarar
Bu sayfa panelin tamamında geçerli ortak davranışları anlatır: giriş, ekran bölümleri, listelerde ortak kullanılan
filtre/sayfalama/satır tıklama kalıpları, çok dilli alanlar ve yetkiler. Diğer sayfalar bu kalıpları tekrar anlatmaz.

## Ekran yerleşimi
![Panel genel görünümü — sol menü, üst çubuk ve içerik alanı](img/dashboard.webp)
1. **Sol menü** — bölümler (Genel, Katalog, Sipariş Yönetimi, Cari, Müşteriler, Stok, Pazarlama, İçerik, Sistem) ve sayfalar. Yetkiniz olmayan sayfalar görünmez.
2. **Üst çubuk** — sayfa başlığı ve sayfaya özel araçlar. Oturumu kapatma düğmesi sol menünün en altındadır (kullanıcı adının yanındaki çıkış simgesi).
3. **İçerik alanı** — seçilen sayfanın listesi/formu.

## Ortak kalıplar
| Kalıp | Davranış |
|---|---|
| Liste satırı | Satıra tıklayınca o kaydın detayı açılır (ayrı sayfa ya da yan panel). |
| Arama kutusu | Yazdıkça filtreler; çoğu listede ad/kod üzerinde çalışır. |
| Sekmeler (`.stab`) | Durum bazlı hızlı filtre (Tümü / Bekleyen / …). |
| Sayfalama | Listenin altında; sayfa boyutu sabittir (çoğu listede 20-50). |
| Çok dilli alanlar | `TR` / `EN` bayraklı sekmeler; kaynak dil zorunlu, diğerleri boş bırakılabilir. |
| Zorunlu alan | Etiketin yanında kırmızı `*`; boş bırakılırsa form kaydedilmez ve hata mesajı görünür. |

## Giriş
Panele `/admin` adresinden, size tanımlanan kullanıcı adı ve şifreyle girilir. Oturum süresi dolunca otomatik yenilenir; yenilenemezse giriş sayfasına yönlendirilirsiniz.

> **Not:** Şifreniz yönetici tarafından (*Sistem → Ayarlar → Kullanıcılar → Şifre sıfırla*) değiştirilir; panelde ayrı bir şifre değiştirme ekranı yoktur.

## İlgili sayfalar
- [Giriş sayfası](/rehber/genel/giris/)
- [Kullanıcılar](/rehber/sistem/kullanicilar/) · [Roller ve Yetkiler](/rehber/sistem/roller-ve-yetkiler/)
