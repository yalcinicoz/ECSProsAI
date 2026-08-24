---
title: Satın Almalar
route: /procurement/purchase-orders
group: Tedarik
order: 10
summary: Tedarikçilere verilen satın alma listelerinin (model, renk, beden, adet, fiyat) kaydedildiği hafif ekran; mal kabul ve ayrıştırma bu kayıtlar olmadan da yürür, kapanış elle verilir.
---

## Ne işe yarar
Tedarikçiye verdiğiniz satın alma listesini kaydeder: hangi model, hangi renk/beden, kaç adet, hangi fiyattan.
**Kayıt katmanıdır** — teslim alma ve ayrıştırma bu kayıt olmadan da yürür; buradaki bilgiler dönemlik tedarik
raporunda "satın alınan ↔ ayrıştırılan" karşılaştırmasına girdi olur. Satıcı fazla/eksik gönderebilir; kesin
eşleşme aranmaz, kapanış **elle** verilir.

## Liste ve filtreler
| Sütun / Filtre | Anlamı |
|---|---|
| KOD | `SA-YYYYAAGG-0001` — otomatik. |
| TEDARİKÇİ / TARİH / BEKLENEN | Cari kart, sipariş tarihi, beklenen teslim (opsiyonel). |
| KALEM · ADET · TUTAR | Kalem sayısı, toplam adet, toplam tutar (kalemlerden hesaplanır). |
| DURUM | `Taslak` → `Sipariş Verildi` → `Teslim Alınıyor` → `Kapandı`; Taslak/Sipariş → `İptal`. |
| Ara | Kod ya da kalem model/renk metninde arar. |

Satıra tıklayınca detay açılır.

## Detay sayfası
- **Başlık**: kod, durum rozeti, tedarikçi, tarihler, toplamlar; durum aksiyon butonları (yanlış kapatılan
  `Geri Aç` ile açılabilir).
- **Kalemler**: model / renk / beden / adet / birim fiyat / tutar / not. Alttaki satırla tek tek eklenir;
  kalemde **adet zorunlu**, model-renk-beden'den en az biri dolu olmalı. Varyant bağlamak zorunlu değildir —
  katalogda henüz olmayan ürün serbest metinle yazılır.
- **Excel'den Yapıştır**: Excel'de sütunları kopyalayın → pencereye yapıştırın → her sütunun ne olduğunu
  (Model/Renk/Beden/Adet/Fiyat/Not/Yoksay) başlıktan eşleyin → önizlemeyi kontrol edip **N kalemi ekle**.
  Türkçe sayı biçimi (virgül) tanınır.
- Kapalı/iptal satın almada kalem eklenemez, silinemez, başlık düzenlenemez.

## Durumlar ve iş kuralları
- Hiçbir durum başka bir akışı kilitlemez; `Kapandı` yalnız "bu listeyle işimiz bitti" beyanıdır.
- Toplamlar kalemlerden anlık hesaplanır; kalem silme geri alınamaz (yeniden eklenir).
- Yetki: **Tedarik Yönetimi** (`procurement.manage`) — menüde bu yetkiyle görünür.

## İlgili sayfalar
- Mal Kabul ve Ayrıştırma ekranları bu bölüme sonraki sürümlerde eklenecektir (tedarik iş akışı T2-T6).
