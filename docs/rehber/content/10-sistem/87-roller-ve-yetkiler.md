---
title: Roller ve Yetkiler
route: /settings/roles
group: Sistem
order: 87
summary: Tanımlı rollerin (Süper Admin, Platform Admin, Firma Admin ve özel roller) listelendiği salt okunur ekran; izin kodları ve anlamları.
---

## Ne işe yarar
Rol, bir kullanıcıya tek seferde verilen izin paketidir. Bu ekran sistemde hangi rollerin tanımlı olduğunu, kodlarını,
sistem rolü mü özel rol mü olduğunu ve aktif olup olmadığını gösterir. Kullanıcıya rol atama bu ekranda değil,
**Kullanıcılar** ekranındaki düzenleme penceresinden yapılır. Ekranın alt başlığı bunu hatırlatır:
"N kayıt — kullanıcıya rol ataması Kullanıcılar ekranından yapılır".

Sayfa sol menüde doğrudan listelenmez; adres çubuğuna `/admin/settings/roles` yazarak ya da Kullanıcılar sayfasından
geçerek (adres) açılır. Ayrı bir izin gerekmez.

## Ekran yerleşimi
![Roller listesi — kod, ad, tip ve durum sütunları](img/settings-roles.webp)
1. **Başlık satırı** — "Roller" ve toplam kayıt sayısı.
2. **Tablo** — her satır bir rol. Satır tıklaması ve düzenleme yoktur (salt okunur).

## Liste ve filtreler
| Sütun | Anlamı |
|---|---|
| KOD | Rolün sistemdeki kısa kodu (ör. `super_admin`). Kullanıcılar listesinin ROLLER sütununda bu kod görünür. |
| AD | Rolün görünen adı (Türkçe; yoksa ilk tanımlı dil). |
| TİP | `Sistem` (mavi) — kurulumla gelen, silinemeyen rol; `Özel` (gri) — sonradan tanımlanmış rol. |
| DURUM | `Aktif` (yeşil) / `Pasif` (gri). |

- Filtre ya da arama kutusu yoktur; roller koda göre alfabetik sıralanır ve tek sayfada listelenir.
- Satıra tıklamak bir şey yapmaz.

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| — | — | Bu ekranda buton yoktur. Rol oluşturma, düzenleme ve izin değiştirme panelden yapılamaz; sistem yöneticinizden istenir. | — |

Kullanıcıya rol vermek için: **Kullanıcılar** → satıra tıkla → **Rol Ata** listesinden rolü seç → **Ata**
(bkz. [Kullanıcılar](/rehber/sistem/kullanicilar/)).

## Durumlar ve iş kuralları
### Kurulumla gelen roller
| Kod | Ad | Tip | Kimler için | İzinleri |
|---|---|---|---|---|
| `super_admin` | Süper Admin | Sistem | Platformu işleten geliştirici ekip | Tüm izinler |
| `platform_admin` | Platform Admin | Sistem | Platform yönetimi | Tüm izinler |
| `firm_admin` | Firma Admin | Özel | Mağaza/firma yöneticisi | Katalog ürün/kategori/görsel/ayar yönetimi + stok yönetimi (aşağıdaki tabloda "Firma Admin" sütunu) |

Varsayılan yönetici hesabı `super_admin` rolüyle gelir.

### İzin kodları ve anlamları
Sayfalar ve işlemler izin koduna bağlıdır. Sol menüde izniniz olmayan sayfalar görünmez; doğrudan adresle açmaya
çalışırsanız işlem reddedilir. İzinler iki katmanlıdır:

**Platform yönetimi izinleri** — yalnız `super_admin` ve `platform_admin` rollerinde bulunur; firma kullanıcılarına
verilmez.

| İzin kodu | Anlamı | Süper/Platform Admin | Firma Admin |
|---|---|---|---|
| `catalog.platform.manage` | Özellik tipleri, özellik değerleri, ürün grupları ve grup yapılandırmasını yönetme (katalogun iskeleti). | ✓ | — |
| `definition.manage` | Tanım verisi yönetimi — örn. **Sistem > Servis Kataloğu** (dış servis tanımları). Bu sayfa yalnız bu izne sahip kullanıcıda sol menüde görünür. | ✓ | — |

**Firma işlem izinleri** — günlük mağaza operasyonu.

| İzin kodu | Anlamı | Süper/Platform Admin | Firma Admin |
|---|---|---|---|
| `catalog.products.manage` | Ürün kartı oluşturma/düzenleme. | ✓ | ✓ |
| `catalog.categories.manage` | Kategori oluşturma/düzenleme. | ✓ | ✓ |
| `catalog.images.manage` | Ürün/varyant görsellerini yönetme. | ✓ | ✓ |
| `catalog.settings.manage` | Katalog ayarlarını yönetme. | ✓ | ✓ |
| `inventory.manage` | Depo, stok ve transfer yönetimi. | ✓ | ✓ |
| `order.packages.merge` | Paket birleştirme / tek fatura istisnası. Normal akış paket başına faturadır; bu izin bilinçli istisna içindir ve Firma Admin'e varsayılan olarak **verilmez**. | ✓ | — |

- Bir kullanıcının birden çok rolü olabilir; yetkisi rollerinin izinlerinin birleşimidir.
- İzin gerektirmeyen sayfalar (Kullanıcılar, Roller, Denetim Logları, POS, Entegrasyon Logları, Tedarikçi
  Faturaları vb.) panele girebilen her kullanıcıya açıktır.

## Adım adım
**Bir kullanıcının hangi yetkilere sahip olduğunu anlama**
1. **Kullanıcılar** ekranında ilgili satırın ROLLER sütunundaki rol kodlarını okuyun.
2. Bu sayfada rolün tipini/durumunu doğrulayın (pasif rol yetki vermez).
3. Yukarıdaki izin tablosundan rolün hangi izinleri içerdiğini bulun.

## İpuçları ve sık karşılaşılan durumlar
> **İpucu:** Sol menüde bir sayfa görünmüyorsa (örn. Servis Kataloğu) kullanıcının rolünde o sayfanın izni yoktur;
> yeni rol atamasının etkili olması için kullanıcının çıkış yapıp yeniden girmesi gerekir.

> **Dikkat:** Sistem rolleri (`super_admin`, `platform_admin`) tüm izinleri taşır; bu rolleri yalnız gerçekten
> ihtiyaç duyan kişilere atayın. Mağaza personeline `firm_admin` ya da özel roller yeterlidir.

> **Not:** Yeni bir özel rol ya da izin değişikliği gerekiyorsa panelden yapılamaz; sistem yöneticinize talep iletin.
> Tanımlanan rol bu listede `Özel` tipiyle görünür ve hemen Kullanıcılar ekranındaki Rol Ata listesine düşer.

## İlgili sayfalar
- [Kullanıcılar](/rehber/sistem/kullanicilar/)
- [Denetim Logları](/rehber/sistem/denetim-loglari/)
- [Giriş ve Panel Yapısı](/rehber/genel/panel-yapisi/)
