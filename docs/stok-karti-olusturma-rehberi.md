# Stok Kartı Oluşturma Rehberi

Bir ürün kartı oluşturmadan önce sistemde bazı verilerin hazır olması gerekiyor.
Aşağıdaki sırayı takip edersen sorun yaşamazsın.

---

## 1. Özellik Tipleri ve Değerleri

**Katalog → Özellik Tipleri** menüsüne gir.

Varyant oluşturmak istiyorsan önce hangi özellik üzerinden varyant yapacağını
belirlemelisin. Örneğin renk ve beden üzerinden varyant yapacaksan bu iki tipin
sistemde tanımlı olması gerekiyor.

- "Yeni Özellik Tipi" butonuna tıkla.
- Kod (örn. `renk`), Türkçe ve İngilizce ad gir.
- Kaydet.

Kaydetttikten sonra o özellik tipinin detay sayfasına geç.
Oradan özellik **değerlerini** ekle:
- Renk için → Kırmızı, Mavi, Siyah, Beyaz, Turuncu-Beyaz-Siyah...
- Beden için → S, M, L, XL...

---

## 2. Ürün Grubu

**Katalog → Ürün Grupları** menüsüne gir.

Ürün grubu, aynı tip ürünlerin (örn. tüm t-shirtler) ortak özellik şablonunu
tanımlar. Her ürün bir gruba bağlı olmalı.

- "Yeni Grup" butonuna tıkla.
- Kod ve ad gir (örn. `tshirt` / T-Shirt).
- Kaydet ve grubun detay sayfasını aç.

Detay sayfasında **Özellikler** sekmesine geç:
- Az önce oluşturduğun özellik tiplerini (Renk, Beden) buraya ekle.
- Her özellik için "Varyant Ekseni mi?" seçeneğini işaretle —
  bu seçeneği işaretlediğin özellikler ürünün varyant boyutlarını oluşturur.

**Eksenler** sekmesine geç:
- Hangi eksen "birincil" olacak? Birincil eksen listede ve varyant tablosunda
  öne çıkarılır. Genellikle renk birincil eksen olur.

---

## 3. Kategori (isteğe bağlı)

**Katalog → Kategoriler** menüsüne gir.

Ürününü kategori bazlı listeleme yapmak istiyorsan önce kategorinin var olması
gerekiyor. Yoksa bu adımı şimdilik atlayabilirsin.

- "Yeni Kategori" butonuna tıkla.
- Kod, ad ve eğer üst kategoriye bağlayacaksan onu da seç.
- Kaydet.

---

## 4. Tedarikçi (isteğe bağlı)

**Cari → Cari Hesaplar** menüsüne gir.

Ürünün bir tedarikçisi varsa ve bunu ürün kartında görmek istiyorsan,
tedarikçinin sistemde cari hesap olarak kayıtlı olması gerekiyor.

- "Yeni Cari Hesap" butonuna tıkla.
- Hesap tipini "Tedarikçi" olarak seç.
- Kaydet.

Tedarikçi zorunlu değil; ürün kartını tedarikçisiz de oluşturabilirsin.

---

## 5. Depo

**Envanter → Depolar** menüsüne gir.

Stok hareketi girebilmek için en az bir deponun tanımlı olması gerekiyor.

- "Yeni Depo" butonuna tıkla.
- Kod (örn. `merkez`), ad ve tip (Merkez / Şube) gir.
- Kaydet.

---

## 6. Firma ve Satış Kanalı

**Ayarlar → Firmalar** menüsüne gir.

"Satış Kanalları" sekmesindeki fiyatları kullanmak istiyorsan firmanın ve
en az bir satış kanalının tanımlı olması gerekiyor.

- Firma yoksa "Yeni Firma" ile oluştur.
- Firma detayına gir, "Satış Kanalları" sekmesinden kanal ekle
  (Web Sitesi, Trendyol, Hepsiburada...).

Platform fiyatlaması kullanmayacaksan bu adımı atlayabilirsin.

---

## 7. Stok Kartını Oluştur

Artık her şey hazır. **Katalog → Ürün Kartları** menüsüne git.

"Yeni Ürün" butonuna tıkla ve temel bilgileri gir:
- Ürün adı (Türkçe ve varsa İngilizce)
- Ürün grubu — az önce oluşturduğun grubu seç
- Satış fiyatı ve KDV oranı
- Kategori (isteğe bağlı)

Kaydet. Ürün kartı açılır ve 8 sekme görürsün:

| Sekme | Ne yaparsın? |
|-------|-------------|
| **Genel** | Fiyat, tedarikçi, aktif/pasif durumu |
| **Özellikler** | Gruba bağlı non-varyant özellikleri gir (Materyal, Sezon...) |
| **Varyantlar** | Varyant kombinasyonlarını oluştur, SKU ve barkod düzenle |
| **Stok** | Depo bazlı mevcut stoku gör, stok hareketi sayfasına link |
| **Satış Kanalları** | Platform fiyatlarını ayarla |
| **Görseller** | Ürün ve varyant görsellerini yükle |
| **Etiketler** | Arama ve filtreleme etiketleri |
| **SEO** | Slug, meta başlık, meta açıklama |

### Varyant oluşturma

**Varyantlar** sekmesine geç. Eğer gruba varyant eksenleri (Renk, Beden)
eklediysen "+ Varyant Ekle" butonu aktif olacak.

- Butona tıkla, hangi renk ve bedenlerin kombinasyonlarını oluşturmak
  istediğini seç.
- "Ekle" dedikten sonra sistem otomatik SKU atar.
- Her varyant satırında fiyatı ve barkodu düzenleyebilirsin.
- Fiyatı değiştirince alandan çıktığında otomatik kaydedilir.

### Stok girişi

Varyantlar oluştuktan sonra **Envanter → Stok** menüsünden stok hareketi
girişi yapabilirsin. "Stok Düzenle" seçeneğinde ürünü, varyantı ve depoyu
seçip giriş miktarını gir.

---

## Özet: Doğru sıra

```
Özellik Tipleri
    └─ Özellik Değerleri
Ürün Grubu (özellik ata + eksen belirle)
Kategori (isteğe bağlı)
Tedarikçi (isteğe bağlı)
Depo
Firma + Satış Kanalı (isteğe bağlı)
    └─ Stok Kartı Oluştur
            └─ Varyant Ekle
                    └─ Stok Girişi
```

Eğer demo verilerini seed ettiysen (sunucuyu ilk başlattığında otomatik yapılır),
yukarıdaki adımların büyük kısmı zaten hazır gelir. Doğrudan "Stok Kartı Oluştur"
adımından başlayabilirsin.
