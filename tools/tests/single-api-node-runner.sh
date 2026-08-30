#!/usr/bin/env bash
# İki-VM acceptance için tek API node runner'ı. Bağlantılar yalnız stdin'den alınır.
set -euo pipefail

APP_DIR="${1:-}"
BIND_IP="${2:-}"
PORT="${3:-}"
NODE_ID="${4:-}"
case "$APP_DIR" in /opt/ecspros-acceptance-tests/[a-f0-9][a-f0-9]*/app) ;;
  *) echo "Güvenlik: acceptance app yolu geçersiz." >&2; exit 2 ;;
esac
[[ "$BIND_IP" =~ ^192\.168\.0\.[0-9]{1,3}$ ]] || { echo "Private bind IP geçersiz." >&2; exit 2; }
[[ "$PORT" =~ ^[0-9]{4,5}$ ]] || { echo "API portu geçersiz." >&2; exit 2; }
[[ "$NODE_ID" =~ ^acceptance-api-[ab]$ ]] || { echo "Node ID geçersiz." >&2; exit 2; }
[ -f "$APP_DIR/ECSPros.Api.dll" ] || { echo "API DLL bulunamadı." >&2; exit 2; }

IFS= read -r DB_CONNECTION || true
IFS= read -r REDIS_CONNECTION || true
IFS= read -r JWT_SECRET || true
DB_CONNECTION="${DB_CONNECTION%$'\r'}"
REDIS_CONNECTION="${REDIS_CONNECTION%$'\r'}"
JWT_SECRET="${JWT_SECRET%$'\r'}"
echo "$DB_CONNECTION" | grep -Eqi 'Database=[^;]*(test|acceptance)' \
  || { echo "Güvenlik: canlı DB reddedildi." >&2; exit 3; }
[ -n "$REDIS_CONNECTION" ] && [ "${#JWT_SECRET}" -ge 32 ] || { echo "Test payload eksik." >&2; exit 3; }

ROOT_DIR="$(dirname "$APP_DIR")"
RUN_DIR="$ROOT_DIR/run"
mkdir -m 700 -- "$RUN_DIR" "$ROOT_DIR/media" "$ROOT_DIR/dp-files"
LOG_FILE="$RUN_DIR/api.log"
PID_FILE="$RUN_DIR/api.pid"
API_PID=""
cleanup() {
  if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then kill -TERM "$API_PID" 2>/dev/null || true; fi
  if [ -n "$API_PID" ]; then wait "$API_PID" 2>/dev/null || true; fi
  rm -f -- "$PID_FILE"
}
trap cleanup EXIT

export ASPNETCORE_ENVIRONMENT=Acceptance
export ASPNETCORE_URLS="http://$BIND_IP:$PORT"
export ConnectionStrings__DefaultConnection="$DB_CONNECTION"
export ConnectionStrings__MarketplaceRef=""
export ConnectionStrings__Redis="$REDIS_CONNECTION"
export ConnectionStrings__RedisCache="$REDIS_CONNECTION"
export ConnectionStrings__RedisState="$REDIS_CONNECTION"
export Redis__Cache__Mode=Standalone Redis__State__Mode=Standalone
export Redis__SignalR__Enabled=true Redis__SignalR__ChannelPrefix="ECSPros:acceptance:signalr"
export Node__Id="$NODE_ID" Node__Role=Api Node__MigrateOnStartup=false
export Postgres__RequirePrimary=true
export Jwt__Secret="$JWT_SECRET" Jwt__Issuer=ECSPros-Acceptance Jwt__Audience=ECSPros-Acceptance
export Storage__Provider=Local Storage__Catalog__Enabled=false Storage__Local__RootPath="$ROOT_DIR/media"
export DataProtection__KeysPath="$ROOT_DIR/dp-files"
export Legacy__Sync__Enabled=false Tracking__Enabled=false Feeds__Enabled=false CargoNotify__Enabled=false

(cd "$APP_DIR" && exec dotnet ECSPros.Api.dll) >"$LOG_FILE" 2>&1 &
API_PID=$!
printf '%s\n' "$API_PID" >"$PID_FILE"
for _ in $(seq 1 75); do
  kill -0 "$API_PID" 2>/dev/null || { tail -n 50 "$LOG_FILE" >&2 || true; exit 4; }
  if curl -fsS --max-time 3 "http://$BIND_IP:$PORT/ready" >/dev/null; then
    echo "single-api-node-ready: $NODE_ID $BIND_IP:$PORT"
    wait "$API_PID"
    exit $?
  fi
  sleep 1
done
echo "API readiness zaman aşımı: $NODE_ID" >&2
tail -n 50 "$LOG_FILE" >&2 || true
exit 5
