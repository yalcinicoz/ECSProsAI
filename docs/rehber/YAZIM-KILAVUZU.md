# ECSPros Admin Paneli Kullanım Rehberi — Yazım Kılavuzu (içerik üreticileri için)

Rehber kaynağı: `docs/rehber/content/<NN-bolum-slug>/<sayfa-slug>.md` (Markdown + ön bilgi bloğu).
Derleme: `cd docs/rehber/tools && npm run build` → çıktı `docker/nginx/html/rehber/` (tüm admin host'larında `/rehber`).
Görseller: `docs/rehber/img/<slug>.webp` (derleme kopyalar). Rehber TÜM firmaların admin panelleri için ORTAKTIR —
Telemania/Misharitalia gibi firma adı geçmez ("mağazanız", "kanalınız" denir).

## Dosya başı (ön bilgi) — zorunlu
```
---
title: Ürün Kartları
route: /catalog/products
group: Katalog
order: 10
summary: Stok kartlarının (ürün + varyant) listelendiği, arandığı ve oluşturulduğu ekran.
---
```
`group` = sol menüdeki bölüm adı (Genel, Katalog, Sipariş Yönetimi, Cari, Müşteriler, Stok, Pazarlama, İçerik, Sistem).
Detay sayfaları (`/catalog/products/:code` gibi) ayrı dosya olabilir (`urun-karti-detay.md`) ya da liste sayfasının
altında `## Detay sayfası` bölümü — 300+ satırlık detay ekranları AYRI dosya olsun.

## Sayfa şablonu (bu başlıklar ve sıra)
1. `## Ne işe yarar` — 2-4 cümle; kim, ne zaman kullanır; iş akışındaki yeri.
2. `## Ekran yerleşimi` — önce tam ekran görseli: `![Ürün Kartları listesi](img/catalog-products.webp)`; sonra
   numaralı liste ile ekranın bölgeleri (üst araç çubuğu, filtre şeridi, tablo, sağ panel, sekmeler…).
3. `## Liste ve filtreler` (liste sayfalarında) — tablo: **Sütun | Anlamı**; filtre/arama kutuları: **Filtre | Ne yapar**;
   sıralama, sayfalama, satır tıklama davranışı (satır tıklanınca detay açılır).
4. `## Butonlar ve aksiyonlar` — tablo: **Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki**. Onay diyalogları,
   geri alınamayan işlemler ⚠️ ile.
5. `## Form alanları` (oluştur/düzenle varsa) — tablo: **Alan | Zorunlu | Açıklama / kurallar / örnek**. Çok dilli alanlar
   (TR/EN sekmesi), zorunlu işaretleri, doğrulama mesajları.
6. `## Sekmeler` (varsa) — her sekme ayrı `###` alt başlık, içinde yukarıdaki tablolar.
7. `## Durumlar ve iş kuralları` — durum rozetleri ve geçişler (ör. Sipariş: pending → confirmed → …), otomatik
   etkiler (stok rezervasyonu, bildirim, senkron).
8. `## Adım adım` — en yaygın 1-3 görev numaralı adımlarla (ör. "Yeni ürün oluşturma").
9. `## İpuçları ve sık karşılaşılan durumlar` — `> **İpucu:**` / `> **Dikkat:**` blokları; hata mesajları ve çözümleri.
10. `## İlgili sayfalar` — `[Ürün Grupları](/rehber/katalog/urun-gruplari/)` biçiminde bağlantılar.

## Görsel yer tutucuları (ekran görüntüleri sonradan çekilir)
- Tam sayfa: `img/<rota-slug>.webp` — rota slug'ı: baştaki `/` atılır, `/` → `-`, `:param` → `detay`.
  Örn. `/catalog/products` → `catalog-products.webp`; `/catalog/products/:code` → `catalog-products-detay.webp`;
  `/` (Dashboard) → `dashboard.webp`.
- Ek durumlar: `img/<rota-slug>--<durum>.webp` (örn. `catalog-products--yeni-modal.webp`, `orders-detay--paketler-sekmesi.webp`).
  Her görsel için alt metin ekranın ne gösterdiğini söylesin.
- Görselin altına gerekirse `*(1) Araç çubuğu · (2) Filtre şeridi · (3) Tablo*` gibi numaralı açıklama satırı.

## Üslup
- Kullanıcıya hitap ("…butonuna tıklayın"), kısa cümleler, teknik iç adlar yerine ekrandaki etiketler (gerekirse
  parantezle iç ad). Geliştirici jargonu yok (DTO, handler, cache vb. YAZILMAZ). Kod yalnız kullanıcı girdisi örneklerinde.
- Her sayfa KENDİ BAŞINA anlaşılır olmalı; tekrar gereken kısa bilgiler tekrarlanır, uzun konular için bağlantı.
- Bilinmeyen/uydurma özellik YAZILMAZ: kaynak React sayfası (`admin/src/pages/**`) + iş kuralları (PROGRESS.md,
  docs/*.md, CLAUDE.md). Ekranda olmayan bir şeyi anlatma.
- Yetki/erişim: sayfa bir izne bağlıysa belirt (ör. Servis Kataloğu yalnız `definition.manage`).

## Özel bloklar (derleyici tanır)
- `> **İpucu:** …`, `> **Dikkat:** …`, `> **Not:** …` → renkli kutu.
- Durum rozetleri: `` `pending` `` gibi kod biçimi yeterli.
