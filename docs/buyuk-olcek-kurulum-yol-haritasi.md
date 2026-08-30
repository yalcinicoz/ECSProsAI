# Büyük Ölçekli Müşteri (Docker'sız, Fiziki Sunucular, Kesintisizlik Garantili) — Kurulum Yol Haritası

**Tarih:** 2026-08-30 · **Durum:** ONAY BEKLİYOR (taslak — kullanıcı kararlarıyla şekillenecek)
**İlgili:** `docs/coklu-sunucu-uyumluluk-degerlendirmesi.md` (Kademe A ✅ canlıda / Kademe B tanımlı),
`docs/coklu-sunucu-a0-kurulum.md` (2 düğüm runbook'u), `docs/acik-isler-yol-haritasi.md` FAZ 10

---

## 0. Net sonuç (tek paragraf)

Bugünkü sistem **tek geliştirme sunucusuna** kuruludur (PostgreSQL+Redis+nginx docker-compose, API systemd)
ve FAZ 10 Kademe A ile **uygulama kodu 2+ düğümde çalışmaya hazırdır**. Büyük ölçekli, kesintisizlik
garantisi isteyen bir müşteriye hizmet vermenin önündeki iş üç kümede toplanır:
**(1) Ürünleşme** — ECSPros'un "bizim sunucumuzdaki proje" olmaktan çıkıp docker'sız, tekrarlanabilir,
profilli bir KURULUM PAKETİ hâline gelmesi; **(2) Kademe B uygulama kalemleri** — worker dağıtık sahiplenme,
SignalR backplane, object storage, çoklu-host bağlantılar (ertelenen Sentinel/Patroni/S3 kararı BU müşteri
sınıfı için geri açılır — erteleme "ihtiyaç doğunca" şartlıydı, bu müşteri o ihtiyacın kendisidir);
**(3) Kurumsal işletim** — gözlemlenebilirlik, sır yönetimi, yedekleme/DR, yük testi/SLO, runbook ve
tatbikatlar. Kritik dürüst not: bugünkü ölçümümüz tek sunucuda ~50-60 istek/sn doygunluktur (D5, 2026-08-27);
büyük müşterinin hedef yükü ÖLÇÜLMEDEN taahhüt verilmez — F6 yük testi fazı bu yüzden pazarlıktan önce gelir.

---

## 1. Müşteri profilleri (ürünleşmenin temeli)

| Profil | Kim | Topoloji | Bugünkü durum |
|---|---|---|---|
| **S — Tek sunucu** | Tek firma, tek depo, düşük trafik | 1 sunucu: API+PG+Redis+nginx (compose YA DA docker'sız systemd) | ✅ bugün çalışan kurulum; docker'sız varyantı F1'de paketlenir |
| **M — HA-lite** | Kesinti istemeyen orta boy | 2 API + 1 DB/Redis host, paylaşımlı disk | ✅ kod hazır (FAZ 10 Kademe A); runbook var |
| **L — Tam aktif-aktif** | Bu dokümanın konusu | N API (rol ayrımlı) + PG kümesi + Redis HA + object storage + LB çifti, hepsi fiziki/systemd | Kod: Kademe B kalemleri eksik · Altyapı: referans kurulum hiç yapılmadı |

Aynı kod tabanı üç profili de sunar; fark yalnız **yapılandırma + altyapı**dır. (Node:Role, bağlantı
dizeleri, Storage kökleri, backplane aç/kapa — hepsi ayar.) Bu ilke korunmalı: profil başına kod dalı YOK.

## 2. Hedef mimari — Profil L (docker'sız referans)

```
                 [Donanım LB / VRRP VIP]
                   HAProxy+keepalived ×2        ← müşterinin F5/Netscaler'ı varsa o
                          │  (sticky: gerçek istemci IP hash; TLS sonlandırma)
        ┌───────────┬─────┴─────┬───────────┐
   API-1 (Api)  API-2 (Api)  API-3 (Api)  WRK-1/2 (Worker; aktif-pasif → B2 ile aktif-aktif)
        │ Kestrel+systemd, Node:Id/Role, /ready │
        └───────┬───────────────┬──────────────┘
        PgBouncer → PostgreSQL ×3 (Patroni+etcd, senkron replika seçenekli, pgBackRest+PITR)
        Redis master+replica + Sentinel ×3 (cache VE güvenlik state'i — A3/A4/A9 zaten Redis'te)
        Object storage: MinIO ×4 (erasure) YA DA kurumsal NAS/SAN → medya/feed/ekler (B3)
        Gözlem: Prometheus+Grafana+Alertmanager, merkezi log (Loki/ELK — NodeId etiketli), OpenTelemetry
```

Statik içerik (admin/dist, satici/dist, rehber, media) LB katmanındaki nginx'ten ya da object
storage+CDN'den; SSR zaten API'de. Migration tek noktadan (deploy adımı — A7/A10 disiplini aynen).

## 3. Fazlar

### F1 — Ürünleşme: docker'sız kurulum paketi (≈ 1,5-2 hafta)
- **Ansible** (ya da eşdeğeri) rolleri: `api`, `worker`, `lb`, `postgres`, `redis`, `minio`, `monitoring` —
  S/M/L profilleri envanter dosyasıyla seçilir. Bugünkü elle kurulmuş her şey (systemd üniteleri, nginx
  conf ağacı, sertifikalar, GeoLite, cron'lar) koda dökülür.
- **Sır yönetimi:** `appsettings.Production.json` içindeki sırlar environment/secret-store katmanına
  (asgari: systemd `EnvironmentFile` + 600; kurumsalda Vault entegrasyonu opsiyonu). Config şablonu +
  ortam değeri ayrımı → düğümler arası drift E4 kalıcı çözülür.
- **Sürümleme/paket:** `dotnet publish` çıktısının sürüm numaralı arşivi + değişiklik notu; deploy.sh'ın
  paket-tabanlı hâli (geri dönüş = önceki arşiv). Lisans/teslim modeli ticari karar (K1).
- Çıktı: "boş Ubuntu'dan çalışan Profil S/M" tek komutla; L profili F4'te doğrulanır.

### F2 — Kademe B uygulama kalemleri (≈ 2 hafta, altyapı GEREKTİRMEZ, bugün başlanabilir)
- **B2 (en kritik):** 11 worker'a dağıtık sahiplenme — `FOR UPDATE SKIP LOCKED` + `lease_owner/lease_until`
  + dış çağrı idempotency anahtarları (kargo, e-posta, SMS, hakediş). Worker katmanı aktif-aktif olur;
  "worker düğümü tek" sınırı kalkar. (Feed A6'da bu desene geçti — şablon hazır.)
- **B1:** SignalR Redis backplane (`AddStackExchangeRedis`, ChannelPrefix ortam bazlı) + DashboardMetrics
  liderlik kilidi → sticky zorunluluğu gevşer, çapraz düğüm bildirimi gelir.
- **B4/B5 (uygulama ucu):** Npgsql `BuildMultiHost` + `Target Session Attributes=primary`; Redis Sentinel
  bağlantı dizesi desteği; `/ready`'nin primary-PG kontrolü. (Altyapı yokken de zararsız — tek host'la çalışır.)
- **B6:** hesap bazlı limitler zaten Redis'te (A4); IP limitleri LB katmanına taşınır (HAProxy stick-table).
- Kabul: iki API + iki worker düğümlü staging'de kill-test — hiçbir sipariş/bildirim çift işlenmez, kaybolmaz.

### F3 — B3 Object storage (≈ 1 hafta + aktarım)
- `IFileStorage` sözleşmesi + S3/MinIO adapter (A5'te ertelenen sözleşme burada yazılır); mevcut üç kök
  (media/uploads/feeds) adapter'a alınır; 28,5K görselin aktarımı + `/media` geriye uyumlu yönlendirme + CDN.
- NAS/SAN tercih eden müşteri için paylaşımlı-disk adapter'ı zaten var (A5 config yolu) — S3 şart değil, seçenek.

### F4 — Referans HA altyapısının kurulması ve TATBİKATI (≈ 2-3 hafta, kendi donanımımızda/stajda)
- Patroni+etcd+PgBouncer kurulumu, pgBackRest+PITR, **failover tatbikatı** (primary kill → yazma kesintisi ölçümü).
- Sentinel kurulumu + failover tatbikatı (mobil attestation fail-closed penceresi ölçülür).
- MinIO kümesi; HAProxy+keepalived VIP geçiş tatbikatı; A-T betiğinin L-profil sürümü.
- Çıktı: **Profil L referans kurulum dokümanı** — hangi bileşen kaç sunucu, hangi ayar, nasıl test edilir.

### F5 — Kurumsal işletim katmanı (≈ 1 hafta, F4 ile paralel yürüyebilir)
- Prometheus + Grafana + Alertmanager; ASP.NET Core/OpenTelemetry metrikleri; merkezi log (NodeId'li);
  uptime/SLO panoları. (Yol haritasındaki "sürekli bakım: metrik/APM" kalemi burada zorunlulaşır.)
- Yedekleme sözleşmesi: DB (PITR) + object storage + config; **geri dönüş PROVASI** takvime bağlanır.
- DR kararı müşteriyle: ikinci DC/replika hedefi, RPO/RTO (K4).

### F6 — Yük testi, kapasite ve SLO (≈ 1 hafta)
- k6 senaryoları (B7): hedef trafiğin 2 katında karma senaryo (vitrin/arama/sepet/checkout/panel);
  düğüm-kill altında hata bütçesi ölçümü. Bugünkü referans: tek sunucu ~50-60 rps → L profili kapasitesi
  ÖLÇÜLÜP yazılır, varsayılmaz. Sonuç: sunucu sayısı önerisi + SLO taahhüt tablosu.

### F7 — Pilot müşteri kurulumu ve go-live
- Keşif (K anketi §4) → boyutlandırma → müşteri donanımında F1 paketiyle kurulum → veri aktarımı
  (MigrationTool deneyimi mevcut) → UAT (KabulTestKiti + A-T) → kademeli go-live → hypercare dönemi
  (ilk 2 hafta yoğun izleme) → işletim devri (runbook'lar + eğitim) ya da yönetilen hizmet (K2).

**Sıralama esnekliği:** F2 hemen başlayabilir (kod işi, müşteri beklemez). F1 ile F2 paralel yürür.
F4-F6 bizim donanımımızda bir kez yapılır, sonrası her müşteride tekrar kullanılır.

## 4. Onay/keşif soruları (K)

1. **K1 Teslim modeli:** lisans+müşteri işletir mi, biz yönetilen hizmet mi veririz, karma mı? (F5/F7 kapsamını belirler)
2. **K2 Kiracılık:** büyük müşteri = ayrı kurulum (önerim: EVET, dedicated). Küçükler için paylaşımlı SaaS ayrı karar.
3. **K3 Hedef yük:** eşzamanlı kullanıcı / sipariş/dk / katalog boyutu — SLO ve boyutlandırma bunsuz yapılamaz.
4. **K4 RPO/RTO ve DR:** veri kaybı toleransı, kabul edilebilir kesinti, ikinci DC var mı?
5. **K5 Müşteri standartları:** mevcut LB/storage/monitoring/secret altyapısı bize mi uyar, biz mi ona uyarız?
6. **K6 Ansible mi, başka araç mı?** (müşterinin konfigürasyon yönetimi standardı varsa ona uyulur)
7. **K7 Sıra onayı:** F2'ye (Kademe B kod kalemleri) şimdi başlansın mı? (Altyapı beklemez, her profile fayda.)

## 5. Riskler

| Risk | Önlem |
|---|---|
| Kapasite taahhüdü ölçümsüz verilir | F6 tamamlanmadan SLO imzalanmaz; sözleşmeye ölçüm şartı |
| Patroni/Sentinel işletim bilgisi ekipte taze değil | F4 tatbikatları + runbook; gerekirse dış danışmanlık ilk kurulumda |
| Tek kod tabanı / profil sapması | "Profil = yalnız yapılandırma" ilkesi; CI'da üç profil için de açılış testi |
| Müşteri donanımı/standartları sürpriz çıkarır | F7 öncesi K5 keşif anketi zorunlu; kurulum öncesi uyumluluk kontrol listesi |
| Geliştirme tek sunucuda sürerken üretim disiplinleri unutulur | F1'deki paket, geliştirme sunucusuna da kurulur (kendi ilacını iç) |
