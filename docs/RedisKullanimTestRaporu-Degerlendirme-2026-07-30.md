# RedisKullanimTestRaporu.txt Değerlendirmesi — 2026-07-30

`docs/RedisKullanimTestRaporu.txt` içindeki iddialar, raporun kendi önerdiği doğrulama
yöntemiyle **bu sunucuda (51.178.208.59, ECSPros)** yeniden test edildi. Sonuç: raporun
bulguları bu sisteme AİT DEĞİL ve bu sistemde GEÇERLİ DEĞİL.

## 1. Rapor hangi sistemi anlatıyor?

Rapordaki container adı `ecscommerce-redis-1`; bu sunucudaki container `ecommerce-redis`.
Bu sunucuda ikinci bir Redis yok (tek dinleyici `127.0.0.1:6379`) ve `/opt` altında
ECSCommerce diye bir proje yok. Rapordaki `docker exec ecscommerce-redis-1 ...` komutları
bu sunucuda çalıştırılamaz ("no such container" verir). Rapor, projenin **başka bir
kopyasında** (ECSCommerce, `D:/NewProje` — SiteYavaslikDegerlendirme.txt ile aynı ortam)
üretilmiştir. O kopyada Redis entegrasyonu gerçekten yok olabilir; bu değerlendirme yalnız
canlı ECSPros sistemini kapsar.

## 2. İddia → Bu sunucudaki gerçek durum

| Rapor iddiası | ECSPros gerçeği (2026-07-30 canlı ölçüm) |
|---|---|
| `StackExchange.Redis` / `AddStackExchangeRedisCache` / `RedisCacheService` kodda yok | VAR — `Shared.Infrastructure/DependencyInjection.cs` (paket + kayıt), `RedisCacheService` (hata-yutan + 2 dk devre kesici), açılışta yaz-oku doğrulaması: `journalctl` → `Redis cache: AKTİF ✓` |
| Cache key/TTL tanımı yok | VAR — `DBSIZE=18`; örnek keyler: `ECSPros:channelcat:products:v5:*`, `ECSPros:channelcat:facets:v8:*`, `ECSPros:page:homepage:*` (TTL'li, `cmdstat_expire calls=400`) |
| `keyspace_hits=0, misses=0` — cache okuması yok | hits=585 / misses=402 ve 5 sn'lik pencerede İKİSİ DE ARTIYOR (hit+1, miss+1, 8 komut) |
| `CLIENT LIST`te API bağlantısı yok | VAR — 2 kalıcı bağlantı: `name=ecsproshop(SE.Redis-v2.7.27)` (API'nin StackExchange.Redis çoklayıcısı) |
| GET/SET komutları hiç işlenmemiş | Okumalar `EVALSHA`/HMGET üzerinden (IDistributedCache'in normal deseni); `expire=400`, ping/auth sayaçları docker healthcheck'ten (10 sn'de bir `redis-cli ping`) |

## 3. Haklılık payı

- **"Redis kurulu olması ≠ kullanılıyor olması" metodolojik uyarısı doğrudur** ve zaten
  bu projenin işletme pratiğine 2026-07-07'de gömülmüştür: açılıştaki yaz-oku denemesi
  tek satır durum loglar (`AKTİF ✓ / ERİŞİLEMİYOR / YAPILANDIRILMAMIŞ`) — yani "kurulu
  ama bağlı değil" durumu bu sistemde sessiz kalamaz (CLAUDE.md → Redis Cache Kuralları).
- **Cache bilinçli olarak dar kapsamlıdır**: yalnız storefront sıcak yolları (kanal
  kategori ürün/facet listeleri, sayfa kompozisyonu) Redis'tedir; kısa TTL süreç-içi
  ihtiyaçlar `IMemoryCache`tedir ve cache tamamen kapalıyken de site doğru çalışır
  (hata-güvenli tasarım). Bu, raporun aradığı "her yerde GET/SET izi" görüntüsünü
  vermez; kusur değil tasarım kararıdır.
- Bunlar dışında rapordan bu repoya taşınacak düzeltme çıkmadı: kod düzeltmesi
  gerektiren tek bir geçerli bulgu yok.

## 4. Tekrarlanabilir doğrulama

redis-cli host'ta kurulu değil; kanıt toplama betiği RESP üzerinden çalışır:
scratchpad `redis_check.py` (INFO stats ×2, DBSIZE, CLIENT LIST, commandstats, SCAN).
Şifre `appsettings.Production.json` → `ConnectionStrings:Redis` içinden okunur,
rapora/loga yazılmaz.
