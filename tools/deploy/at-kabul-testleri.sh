#!/usr/bin/env bash
# FAZ 10 / A-T — çapraz düğüm kabul testleri (Kademe A "HA-lite").
#
# Kullanım (A0 kurulduktan sonra):
#   NODE_A=http://10.0.0.1:5000 NODE_B=http://10.0.0.2:5000 \
#   SITE=https://www.misharitalia.com HOSTH=www.misharitalia.com \
#   bash tools/deploy/at-kabul-testleri.sh
#
# Kesinti sürekliliği testi (operatör A düğümünü elle durdururken):
#   SITE=https://www.misharitalia.com bash tools/deploy/at-kabul-testleri.sh --kesinti
#
# Otomatik testler kimlik GEREKTİRMEZ. Kimlik isteyen üç senaryo (upload A→okuma B,
# feed tetik A→worker B, cache bust A→B) runbook'taki elle kontrol listesindedir:
# docs/coklu-sunucu-a0-kurulum.md
set -u
NODE_A="${NODE_A:-http://localhost:5000}"
NODE_B="${NODE_B:-}"
SITE="${SITE:-}"
HOSTH="${HOSTH:-www.misharitalia.com}"
GECTI=0; KALDI=0

sonuc() { # $1=ad $2=0/1 (0=geçti) $3=detay
  if [ "$2" = 0 ]; then GECTI=$((GECTI+1)); echo "✓ $1 — $3"
  else KALDI=$((KALDI+1)); echo "✗ $1 — $3"; fi
}

# ── Kesinti modu: operatör bir düğümü durdururken site 60 sn boyunca yoklanır ──
if [ "${1:-}" = "--kesinti" ]; then
  [ -n "$SITE" ] || { echo "SITE ortam değişkeni gerekli"; exit 2; }
  echo "60 sn boyunca $SITE saniyede bir yoklanacak — ŞİMDİ bir düğümü durdurun (sudo systemctl stop ecspros)."
  hata=0
  for i in $(seq 1 60); do
    kod=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$SITE/") || kod=000
    [ "$kod" = 200 ] || { hata=$((hata+1)); echo "  $i. sn: HTTP $kod"; }
    sleep 1
  done
  echo "Bitti: 60 yoklamada $hata hata. HA-lite hedefi: nginx passive failover ~birkaç istekte toparlar" \
       "(0-3 kısa hata kabul; sürekli hata = upstream yapılandırmasını kontrol edin)."
  exit 0
fi

[ -n "$NODE_B" ] || { echo "NODE_B ortam değişkeni gerekli (örn. http://10.0.0.2:5000)"; exit 2; }

echo "── A-T kabul testleri: A=$NODE_A  B=$NODE_B"

# T1: iki düğüm de hazır ve kimlikleri FARKLI. /health/detail AdminOnly'dir;
# anonim kabul betiği nodeId içeren LB-uyumlu /ready yanıtını kullanır.
ra=$(curl -s --max-time 10 "$NODE_A/ready"); rb=$(curl -s --max-time 10 "$NODE_B/ready")
na=$(echo "$ra" | grep -o '"nodeId":"[^"]*"' | head -1); nb=$(echo "$rb" | grep -o '"nodeId":"[^"]*"' | head -1)
sa=$(echo "$ra" | grep -o '"status":"[^"]*"' | head -1);  sb=$(echo "$rb" | grep -o '"status":"[^"]*"' | head -1)
if [ -n "$na" ] && [ -n "$nb" ] && [ "$na" != "$nb" ] && [ "$sa" = '"status":"Healthy"' ] && [ "$sb" = '"status":"Healthy"' ];
then sonuc "T1 düğüm kimlikleri" 0 "A=$na B=$nb (ikisi de Healthy)"
else sonuc "T1 düğüm kimlikleri" 1 "A=$na/$sa B=$nb/$sb — kimlikler farklı ve Healthy olmalı"; fi

# T2: /ready her iki düğümde 200 + dataprotection Healthy (B'de DP anahtarları çözülebiliyor = P0-3 kanıtı)
for etiket in A B; do
  url=$([ $etiket = A ] && echo "$NODE_A" || echo "$NODE_B")
  r=$(curl -s --max-time 10 "$url/ready")
  if echo "$r" | grep -q '"name":"dataprotection","status":"Healthy"' && echo "$r" | grep -q '"status":"Healthy"';
  then sonuc "T2 /ready+DP ($etiket)" 0 "PG+Redis-state+DP Healthy"
  else sonuc "T2 /ready+DP ($etiket)" 1 "yanıt: $(echo "$r" | head -c 200)"; fi
done

# T3: challenge A'dan alınır, B'de tüketilir (device state Redis'te — P0-1 kanıtı)
ch=$(curl -s --max-time 10 "$NODE_A/api/store/device/challenge" | grep -o '"challenge":"[^"]*"' | cut -d'"' -f4)
if [ -z "$ch" ]; then sonuc "T3 çapraz challenge" 1 "A'dan challenge alınamadı"; else
  y1=$(curl -s --max-time 10 -X POST "$NODE_B/api/store/device/attest" -H "Content-Type: application/json" \
       -d "{\"platform\":\"android\",\"attestation\":\"at-test\",\"challenge\":\"$ch\"}")
  y2=$(curl -s --max-time 10 -X POST "$NODE_B/api/store/device/attest" -H "Content-Type: application/json" \
       -d "{\"platform\":\"android\",\"attestation\":\"at-test\",\"challenge\":\"$ch\"}")
  # 1. deneme: challenge B'de BULUNMALI (hata challenge DEĞİL attestation'dan gelmeli); 2. deneme: challenge tükenmiş olmalı
  if ! echo "$y1" | grep -q "Challenge geçersiz" && echo "$y2" | grep -q "Challenge geçersiz";
  then sonuc "T3 çapraz challenge" 0 "A'nın challenge'ı B'de bulundu+tüketildi; tekrar reddedildi"
  else sonuc "T3 çapraz challenge" 1 "1.: $(echo "$y1" | head -c 80) / 2.: $(echo "$y2" | head -c 80)"; fi
fi

# T4: login kilidi A'da dolar, B'de geçerli (sayaç Redis'te — P1 kanıtı)
eposta="at-kabul-$(date +%s)@ornek.dev"
tokA=$(curl -s --max-time 15 "$NODE_A/" -H "Host: $HOSTH" | grep -o 'ms-api-token" content="[^"]*' | sed 's/.*content="//')
tokB=$(curl -s --max-time 15 "$NODE_B/" -H "Host: $HOSTH" | grep -o 'ms-api-token" content="[^"]*' | sed 's/.*content="//')
if [ -z "$tokA" ] || [ -z "$tokB" ]; then sonuc "T4 çapraz login kilidi" 1 "SSR web token alınamadı (HOSTH=$HOSTH doğru mu?)"; else
  for i in 1 2 3 4 5; do
    curl -s -o /dev/null --max-time 10 -X POST "$NODE_A/api/store/auth/login" \
      -H "Authorization: Bearer $tokA" -H "Content-Type: application/json" \
      -d "{\"email\":\"$eposta\",\"password\":\"yanlis\"}"
  done
  yb=$(curl -s --max-time 10 -X POST "$NODE_B/api/store/auth/login" \
      -H "Authorization: Bearer $tokB" -H "Content-Type: application/json" \
      -d "{\"email\":\"$eposta\",\"password\":\"yanlis\"}")
  if echo "$yb" | grep -q "Çok fazla hatalı deneme";
  then sonuc "T4 çapraz login kilidi" 0 "A'da 5 deneme → B'de kilitli (15 dk sonra kendiliğinden açılır)"
  else sonuc "T4 çapraz login kilidi" 1 "B yanıtı: $(echo "$yb" | head -c 120)"; fi
fi

echo "──"
echo "Otomatik: $GECTI geçti, $KALDI kaldı."
echo "Elle kalanlar (runbook §A-T): upload A→okuma B (paylaşımlı disk) · feed tetik A→worker B üretir ·"
echo "cache bust (A'da kapsam değişikliği → B'nin sitesinde 60 sn beklemeden görünür) · --kesinti modu."
[ "$KALDI" = 0 ]
