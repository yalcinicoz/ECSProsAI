#!/usr/bin/env bash
# VM-1 üzerinde çalışır; private ağdaki iki API node'unu çapraz doğrular.
set -euo pipefail
URL_A="${1:-http://192.168.0.242:25101}"
URL_B="${2:-http://192.168.0.243:25102}"
[[ "$URL_A" == http://192.168.0.* && "$URL_B" == http://192.168.0.* ]] \
  || { echo "Yalnız private acceptance URL kabul edilir." >&2; exit 2; }

READY_A="$(curl -fsS --max-time 15 "$URL_A/ready")"
READY_B="$(curl -fsS --max-time 15 "$URL_B/ready")"
echo "$READY_A" | grep -q '"nodeId":"acceptance-api-a"'
echo "$READY_B" | grep -q '"nodeId":"acceptance-api-b"'
for ready in "$READY_A" "$READY_B"; do
  echo "$ready" | grep -q '"status":"Healthy"'
  echo "$ready" | grep -q '"name":"postgresql".*"status":"Healthy"'
  echo "$ready" | grep -q '"name":"redis-state".*"status":"Healthy"'
  echo "$ready" | grep -q '"name":"dataprotection".*"status":"Healthy"'
done
echo "two-vm: both-private-nodes-ready"

CHALLENGE="$(curl -fsS --max-time 10 "$URL_A/api/store/device/challenge" \
  | grep -o '"challenge":"[^"]*"' | cut -d'"' -f4)"
[ -n "$CHALLENGE" ]
FIRST="$(curl -sS --max-time 10 -X POST "$URL_B/api/store/device/attest" -H 'Content-Type: application/json' \
  -d "{\"platform\":\"android\",\"attestation\":\"acceptance-invalid\",\"challenge\":\"$CHALLENGE\"}")"
SECOND="$(curl -sS --max-time 10 -X POST "$URL_B/api/store/device/attest" -H 'Content-Type: application/json' \
  -d "{\"platform\":\"android\",\"attestation\":\"acceptance-invalid\",\"challenge\":\"$CHALLENGE\"}")"
! echo "$FIRST" | grep -q 'Challenge geçersiz'
echo "$SECOND" | grep -q 'Challenge geçersiz'
echo "two-vm: redis-state-cross-vm-consume-ok"
echo "two-vm-api-regression: OK"
