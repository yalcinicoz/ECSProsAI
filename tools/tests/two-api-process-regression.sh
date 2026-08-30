#!/usr/bin/env bash
# FAZ 11/K8 — aynı Linux VM'de iki gerçek API process'iyle çapraz-process kabulü.
set -euo pipefail

APP_DIR="${1:-}"
PORT_A="${2:-5101}"
PORT_B="${3:-5102}"
case "$APP_DIR" in /opt/ecspros-acceptance-tests/[a-f0-9][a-f0-9]*/app) ;;
  *) echo "Güvenlik: yalnız benzersiz acceptance app dizini kullanılabilir." >&2; exit 2 ;;
esac
[[ "$PORT_A" =~ ^[0-9]{4,5}$ && "$PORT_B" =~ ^[0-9]{4,5}$ && "$PORT_A" != "$PORT_B" ]] \
  || { echo "Geçersiz API portları." >&2; exit 2; }
[ -f "$APP_DIR/ECSPros.Api.dll" ] || { echo "ECSPros.Api.dll bulunamadı." >&2; exit 2; }

IFS= read -r DB_CONNECTION || true
IFS= read -r REDIS_CONNECTION || true
IFS= read -r JWT_SECRET || true
DB_CONNECTION="${DB_CONNECTION%$'\r'}"
REDIS_CONNECTION="${REDIS_CONNECTION%$'\r'}"
JWT_SECRET="${JWT_SECRET%$'\r'}"
[ -n "$DB_CONNECTION" ] && [ -n "$REDIS_CONNECTION" ] && [ "${#JWT_SECRET}" -ge 32 ] \
  || { echo "Test bağlantıları/JWT stdin üzerinden eksik geldi." >&2; exit 3; }
echo "$DB_CONNECTION" | grep -Eqi 'Database=[^;]*(test|acceptance)' \
  || { echo "Güvenlik: PostgreSQL DB adı test veya acceptance içermelidir." >&2; exit 3; }

RUN_DIR="$(dirname "$APP_DIR")/run"
mkdir -m 700 -- "$RUN_DIR"
LOG_A="$RUN_DIR/api-a.log"
LOG_B="$RUN_DIR/api-b.log"
PID_A=""
PID_B=""

cleanup() {
  for pid in "$PID_A" "$PID_B"; do
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      kill -TERM "$pid" 2>/dev/null || true
    fi
  done
  for pid in "$PID_A" "$PID_B"; do
    if [ -n "$pid" ]; then wait "$pid" 2>/dev/null || true; fi
  done
}
trap cleanup EXIT

export ASPNETCORE_ENVIRONMENT=Acceptance
export ConnectionStrings__DefaultConnection="$DB_CONNECTION"
export ConnectionStrings__MarketplaceRef=""
export ConnectionStrings__Redis="$REDIS_CONNECTION"
export ConnectionStrings__RedisCache="$REDIS_CONNECTION"
export ConnectionStrings__RedisState="$REDIS_CONNECTION"
export Redis__Cache__Mode=Standalone
export Redis__State__Mode=Standalone
export Redis__SignalR__Enabled=true
export Redis__SignalR__ChannelPrefix="ECSPros:acceptance:signalr"
export Node__Role=Api
export Node__MigrateOnStartup=false
export Postgres__RequirePrimary=true
export Jwt__Secret="$JWT_SECRET"
export Jwt__Issuer=ECSPros-Acceptance
export Jwt__Audience=ECSPros-Acceptance
export Storage__Provider=Local
export Storage__Catalog__Enabled=false
export Storage__Local__RootPath="$(dirname "$APP_DIR")/media"
export DataProtection__KeysPath="$(dirname "$APP_DIR")/dp-files"
export Legacy__Sync__Enabled=false
export Tracking__Enabled=false
export Feeds__Enabled=false
export CargoNotify__Enabled=false

start_api() {
  local node_id="$1" port="$2" log="$3"
  (
    cd "$APP_DIR"
    Node__Id="$node_id" ASPNETCORE_URLS="http://127.0.0.1:$port" \
      exec dotnet ECSPros.Api.dll
  ) >"$log" 2>&1 &
  STARTED_PID=$!
}

wait_live() {
  local pid="$1" port="$2" log="$3"
  for _ in $(seq 1 60); do
    kill -0 "$pid" 2>/dev/null || {
      echo "API process erken kapandı (port=$port)." >&2
      tail -n 40 "$log" >&2 || true
      return 1
    }
    if curl -fsS --max-time 2 "http://127.0.0.1:$port/live" >/dev/null; then return 0; fi
    sleep 1
  done
  echo "API /live zaman aşımı (port=$port)." >&2
  tail -n 40 "$log" >&2 || true
  return 1
}

start_api "acceptance-api-a" "$PORT_A" "$LOG_A"; PID_A="$STARTED_PID"
start_api "acceptance-api-b" "$PORT_B" "$LOG_B"; PID_B="$STARTED_PID"
wait_live "$PID_A" "$PORT_A" "$LOG_A"
wait_live "$PID_B" "$PORT_B" "$LOG_B"
echo "two-api: both-processes-live"

READY_A="$(curl -sS --max-time 15 "http://127.0.0.1:$PORT_A/ready")"
READY_B="$(curl -sS --max-time 15 "http://127.0.0.1:$PORT_B/ready")"
if ! echo "$READY_A" | grep -q '"nodeId":"acceptance-api-a"' ||
   ! echo "$READY_B" | grep -q '"nodeId":"acceptance-api-b"'; then
  echo "Node kimlikleri /ready yanıtında beklenen değerde değil." >&2
  echo "A: $READY_A" >&2; echo "B: $READY_B" >&2
  exit 4
fi
for item in "A|$READY_A|$LOG_A" "B|$READY_B|$LOG_B"; do
  label="${item%%|*}"; rest="${item#*|}"; ready="${rest%|*}"; log="${item##*|}"
  if ! echo "$ready" | grep -q '"status":"Healthy"' ||
     ! echo "$ready" | grep -q '"name":"postgresql".*"status":"Healthy"' ||
     ! echo "$ready" | grep -q '"name":"redis-state".*"status":"Healthy"' ||
     ! echo "$ready" | grep -q '"name":"dataprotection".*"status":"Healthy"'; then
    echo "API-$label readiness sağlıklı değil: $ready" >&2
    echo "API-$label son loglar:" >&2
    tail -n 35 "$log" >&2 || true
    exit 4
  fi
done
echo "two-api: readiness-shared-dependencies-healthy"

CHALLENGE="$(curl -fsS --max-time 10 "http://127.0.0.1:$PORT_A/api/store/device/challenge" \
  | grep -o '"challenge":"[^"]*"' | cut -d'"' -f4)"
[ -n "$CHALLENGE" ] || { echo "API-A challenge üretmedi." >&2; exit 5; }
FIRST="$(curl -sS --max-time 10 -X POST "http://127.0.0.1:$PORT_B/api/store/device/attest" \
  -H 'Content-Type: application/json' \
  -d "{\"platform\":\"android\",\"attestation\":\"acceptance-invalid\",\"challenge\":\"$CHALLENGE\"}")"
SECOND="$(curl -sS --max-time 10 -X POST "http://127.0.0.1:$PORT_B/api/store/device/attest" \
  -H 'Content-Type: application/json' \
  -d "{\"platform\":\"android\",\"attestation\":\"acceptance-invalid\",\"challenge\":\"$CHALLENGE\"}")"
! echo "$FIRST" | grep -q 'Challenge geçersiz'
echo "$SECOND" | grep -q 'Challenge geçersiz'
echo "two-api: redis-cross-process-state-consume-ok"

kill -TERM "$PID_A"
wait "$PID_A" 2>/dev/null || true
PID_A=""
for _ in $(seq 1 20); do
  if curl -fsS --max-time 2 "http://127.0.0.1:$PORT_B/ready" >/dev/null; then
    echo "two-api: peer-survived-api-a-stop"
    echo "two-api-process-regression: OK"
    exit 0
  fi
  sleep 0.5
done
echo "API-B, API-A durduktan sonra hazır kalmadı." >&2
exit 6
