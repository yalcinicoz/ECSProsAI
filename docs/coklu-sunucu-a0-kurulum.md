# FAZ 10 / A0 — İkinci Düğüm Kurulum Runbook'u (Kademe A "HA-lite")

**Tarih:** 2026-08-30 · **Ön koşul:** FAZ 10 A1-A10 canlıda (bkz. `docs/acik-isler-yol-haritasi.md`)
**Amaç:** İkinci bir API düğümü ekleyip "bir API sunucusu kapanınca site/panel/mobil çalışmaya devam eder"
hedefine ulaşmak. DB + Redis düğüm 1'de kalır (onların HA'sı Kademe B — ertelendi).

Uygulama kodu hazır; bu doküman **yalnız altyapı/ayar** adımlarıdır. Sıra önemlidir.

---

## 1. İkinci VM (düğüm 2)

- Ubuntu (düğüm 1 ile aynı sürüm önerilir), `yalcin` kullanıcısı, düğüm 1 ile **özel ağ** (örn. 10.0.0.x).
- ASP.NET Core 8 runtime: `sudo apt install aspnetcore-runtime-8.0`
- Dizin: `mkdir -p /opt/ECSProsAI/publish` (kod rsync ile gelecek — bkz. §6).
- systemd birimi: düğüm 1'deki `/etc/systemd/system/ecspros.service` kopyalanır; `[Service]` bölümüne
  düğüm kimliği ve rolü eklenir:
  ```ini
  Environment=Node__Id=node2
  Environment=Node__Role=Api
  Environment=Node__MigrateOnStartup=false
  ```
  (Worker rolü düğüm 1'de kalır — `Role=Both` varsayılanı. Düğüm 1 uzun süre kapalı kalacaksa
  düğüm 2 geçici `Node__Role=Both` yapılıp restart edilerek arka plan işleri elle devralınır.)
- GeoLite2 `.mmdb` dosyası düğüm 1'deki yola kopyalanır (yoksa özellik kendiliğinden kapalı — hata değil).
- `~/.ecspros/dp-keys` **GEREKMEZ** — Data Protection anahtarları artık DB'de (A1).

## 2. Bağlantı dizeleri (tek ortak config)

`appsettings.Production.json` her iki düğümde **birebir aynı** olmalı (drift E4). Bu yüzden:
- `ConnectionStrings:DefaultConnection` ve `Redis`'te `localhost` yerine **düğüm 1'in özel ağ IP'si**
  yazılır (örn. `Host=10.0.0.1`, `10.0.0.1:6379,...`). Düğüm 1 de kendi servislerine bu IP'den erişir —
  önce düğüm 1'de bu değişiklikle tek başına çalıştığı doğrulanır.
- `MarketplaceRef` bağlantı dizesi (varsa) aynı işleme tabi.

## 3. Güvenlik duvarı (P1'in birinci savunma hattı — teyit sözü verilmişti)

Düğüm 1'de: 5000 (API), 5432 (PostgreSQL), 6379 (Redis) **dışarıya kapalı**, yalnız düğüm 2'nin
özel IP'sine açık. Düğüm 2'de: 5000 yalnız düğüm 1 (nginx) IP'sine açık. 5055 (staging) bilinçli
istisna ise not düşün. Örnek (ufw): `sudo ufw allow from 10.0.0.2 to any port 5432 proto tcp` vb.

## 4. Paylaşımlı dizin `/srv/ecspros-shared` (NFS — düğüm 1 sunucu ya da ayrı NAS)

- Alt dizinler: `media/`, `feeds/`. İki düğümde de aynı yola mount edilir (fstab + `_netdev`).
- Mevcut veriler taşınır: `rsync -a /opt/ECSProsAI/media/ /srv/ecspros-shared/media/` (feed'ler ilk
  üretimde kendiliğinden dolar, taşıma şart değil).
- **Ayar değişiklikleri (A5 kararı — kod işi yok):**
  - `appsettings.Production.json`: `Store:MediaRootPath = /srv/ecspros-shared/media`,
    `Feeds:OutputPath = /srv/ecspros-shared/feeds`
  - CatalogSettings (DB, admin Katalog Ayarları): `ImageServer.LocalSavePath` → paylaşımlı yol
    (yalnız ürün görsel/video YÜKLEMESİ bunu kullanır; boşsa publish/uploads'a düşer — mount sonrası doldurun).
  - `docker-compose.yml` nginx volume: `./media:...` → `/srv/ecspros-shared/media:/usr/share/nginx/html/media:ro`
    → `sudo docker compose up -d nginx` (restart değil — volume değişikliği up ister).

## 5. nginx upstream (düğüm 1)

`docker/nginx/conf.d/upstream-ecspros.conf.example` içindeki adımlar:
kopyala → düğüm 2 IP'sini düzelt → üç dosyada `host.docker.internal:5000` hedeflerini `ecspros_api`
yap (**5050'li satırlara dokunma**) → `sudo docker compose restart nginx`.
Yapışkanlık CF-Connecting-IP hash'iyle sağlanır (şablondaki açıklama).

## 6. Deploy ve sıralı restart

- `tools/deploy/nodes.conf.example` → `nodes.conf` (düğüm 2 satırı açılır; SSH anahtarı kurulmuş olmalı).
- `bash tools/deploy/deploy.sh` → publish + rsync; betiğin YAZDIRDIĞI sıralı restart komutları
  uygulanır (her düğümde `/ready` 200 görülmeden diğerine geçilmez).
- Migration'lar yalnız düğüm 1'den (`--migrate` ya da mevcut alışkanlıkla elle) — düğüm 2'de
  `Node__MigrateOnStartup=false` bunu güvenceye alır.

## 7. A-T kabul testleri

1. **Otomatik:** `NODE_A=http://10.0.0.1:5000 NODE_B=http://10.0.0.2:5000 HOSTH=www.misharitalia.com bash tools/deploy/at-kabul-testleri.sh`
   (T1 düğüm kimlikleri · T2 /ready+DP her iki düğümde · T3 challenge A→B · T4 login kilidi A→B).
2. **Elle:**
   - Admin panelden bir ÜRÜN GÖRSELİ yükle (düğüm hangisiyse) → sitede görünmeli (paylaşımlı disk).
   - Panel "Şimdi üret" (feed) → üretimi worker düğümü yapmalı (`integration.feed_status.NodeId` = node1),
     panel "✓ tamamlandı" göstermeli — panelin hangi düğüme bağlı olduğundan bağımsız.
   - Kanal Ürünleri'nde kapsam değişikliği → sitede 60 sn cache beklemeden yansımalı (cache bust A→B).
   - Entegrasyon kimlik "Göster" her iki düğüm üzerinden çalışmalı (DP kanıtının kullanıcı yüzü).
3. **Kesinti:** `SITE=https://www.misharitalia.com bash tools/deploy/at-kabul-testleri.sh --kesinti`
   çalışırken düğüm 1'de `sudo systemctl stop ecspros` → 60 yoklamada en fazla birkaç kısa hata; site
   ayakta kalmalı. Sonra `start` + `/ready` 200. Aynısı düğüm 2 için tekrarlanır.
   (İsteğe bağlı: KabulTestKiti ile tam site taraması — UX düzeyinde ikinci doğrulama.)

## 8. Geri dönüş (rollback)

Upstream'i tek düğüme döndürmek yeterli: `sed -i 's|http://ecspros_api|http://host.docker.internal:5000|g'`
(üç dosyada) + `upstream-ecspros.conf` kaldır + `docker compose restart nginx`. Uygulama ayarlarını geri
almak GEREKMEZ — tek düğümde de aynı kod/ayarlar çalışır (mount erişilebilir kaldığı sürece).

## Bilinen sınırlar (Kademe A kabulleri — plan §3)

SignalR bildirimi üretildiği düğüme bağlı adminlere gider (sticky sayesinde pratik etkisi düşük) ·
worker düğümü kapanırsa arka plan işleri DURUR ama KAYBOLMAZ (Role=Both ile elle devralınır) ·
DB/Redis host'u tek nokta (Kademe B — Sentinel/Patroni/S3 kullanıcı kararıyla ertelendi) ·
NFS düşerse görsel yükleme/feed yazımı hata verir (okuma nginx cache'inden kısmen sürer).
