# Mobil Test Staging Instance

Mobil geliştiricinin `/api/store/*` yüzeyini gerçek backend'e karşı test etmesi için
ayrı instance (2026-07-23). Prod'a dokunmaz: ayrı port (5055), ayrı binary
(`/opt/ECSProsAI/publish-staging`), ayrı systemd birimi (`ecspros-staging`), ortak DB.

**DevBypass açık**: `attest` ucuna secret gönderilince gerçek device token üretilir —
attestation dışı her şey (imza/nonce/replay/kapı/üye akışı) prod'la aynı. Secret
`/etc/ecspros-staging.env` içinde, **git'te değil**; geliştiriciye ayrı kanaldan verilir.
Play Integrity canlanınca bu instance kapatılıp secret imha edilir.

## İlk kurulum (bir kez, sudo gerekir)

```bash
# 1) Ortam dosyası ve unit (Claude bunları scratchpad'e hazırladı)
sudo cp <scratchpad>/ecspros-staging.env     /etc/ecspros-staging.env
sudo cp <scratchpad>/ecspros-staging.service /etc/systemd/system/ecspros-staging.service
sudo chmod 600 /etc/ecspros-staging.env      # secret'ı koru

# 2) Servisi başlat
sudo systemctl daemon-reload
sudo systemctl enable --now ecspros-staging
sudo systemctl status ecspros-staging

# 3) Dışarıdan erişim için 5055 portunu aç (mobil cihaz/emülatör için)
sudo ufw allow 5055/tcp     # ufw kullanılıyorsa
```

Erişim: `http://51.178.208.59:5055`. Android debug build cleartext (`android:usesCleartextTraffic`)
gerektirir; HTTPS istenirse nginx'e ayrı subdomain proxy'si eklenebilir (ayrı iş).

## Kod değişince staging'i güncelle (redeploy)

```bash
cd /opt/ECSProsAI
dotnet publish src/ECSPros.Api/ECSPros.Api.csproj -c Release -o /opt/ECSProsAI/publish-staging --no-restore
cp /opt/ECSProsAI/publish/appsettings.Production.json /opt/ECSProsAI/publish-staging/
sudo systemctl restart ecspros-staging
```

## Geliştiricinin kullanımı

```bash
BASE=http://51.178.208.59:5055 BYPASS=<secret> \
  EMAIL=mobil.test@ecspros.com PASSWORD='MobilTest2026!' \
  node tools/mobile/reference-client.mjs
```

İmza mantığı `reference-client.mjs` içindedir; uygulamada Kotlin/Swift'e çevrilir.

## Kapatma / secret rotasyonu

```bash
sudo systemctl disable --now ecspros-staging     # instance'ı durdur
sudo rm /etc/ecspros-staging.env                 # secret'ı imha et
# Rotasyon: openssl rand -hex 24 → env dosyasına yaz → restart
```
