#!/usr/bin/env bash
# FAZ 11 / K4 — iki ayrı PostgreSQL client process ile advisory-lock kill/recovery kabulü.
set -euo pipefail

DATABASE_NAME="${1:-}"
POSTGRES_HOST="${2:-}"
POSTGRES_PORT="${3:-5432}"
POSTGRES_USER="${4:-}"
[[ "$DATABASE_NAME" =~ ^[A-Za-z0-9_-]*(test|acceptance)[A-Za-z0-9_-]*$ ]] \
  || { echo "Güvenlik: DB adı test veya acceptance içermelidir." >&2; exit 2; }
[[ "$POSTGRES_HOST" =~ ^[A-Za-z0-9.-]+$ ]] || { echo "Geçersiz PostgreSQL host." >&2; exit 2; }
[[ "$POSTGRES_PORT" =~ ^[0-9]{1,5}$ ]] && [ "$POSTGRES_PORT" -ge 1 ] && [ "$POSTGRES_PORT" -le 65535 ] \
  || { echo "Geçersiz PostgreSQL port." >&2; exit 2; }
[[ "$POSTGRES_USER" =~ ^[A-Za-z0-9_.-]+$ ]] || { echo "Geçersiz PostgreSQL user." >&2; exit 2; }
command -v psql >/dev/null || { echo "psql bulunamadı." >&2; exit 3; }
IFS= read -r PGPASSWORD || true
# Windows PowerShell native pipe satırı CRLF ile sonlandırabilir; Bash read LF'yi atıp CR'yi bırakır.
PGPASSWORD="${PGPASSWORD%$'\r'}"
[ -n "$PGPASSWORD" ] || { echo "PostgreSQL parolası stdin üzerinden verilmedi." >&2; exit 3; }
export PGHOST="$POSTGRES_HOST" PGPORT="$POSTGRES_PORT" PGUSER="$POSTGRES_USER" PGPASSWORD PGCONNECT_TIMEOUT=5

PSQL=(psql)

LOCK_NAME="ecspros:acceptance:worker-kill:$(date +%s):$$"
HOLDER_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ecspros-worker-lock.XXXXXXXX")"
HOLDER_FIFO="$HOLDER_DIR/stdin"
HOLDER_LOG="$HOLDER_DIR/holder.log"
holder_process=""
mkfifo -- "$HOLDER_FIFO"
cleanup() {
  if [ -n "$holder_process" ] && kill -0 "$holder_process" 2>/dev/null; then kill -KILL "$holder_process" 2>/dev/null || true; fi
  exec 9>&- 2>/dev/null || true
  case "$HOLDER_DIR" in "${TMPDIR:-/tmp}"/ecspros-worker-lock.*)
      rm -f -- "$HOLDER_FIFO" "$HOLDER_LOG"
      rmdir -- "$HOLDER_DIR" 2>/dev/null || true
      ;;
    *) echo "Güvenlik: beklenmeyen holder yolu silinmedi." >&2 ;;
  esac
}
trap cleanup EXIT

# Worker'ın tur boyunca açık tuttuğu fakat sorgu çalıştırmadığı DB session'ını temsil eder.
# FIFO writer açık kaldığı sürece psql EOF görmez; lock alındıktan sonra backend idle olur.
"${PSQL[@]}" -X -v ON_ERROR_STOP=1 -d "$DATABASE_NAME" <"$HOLDER_FIFO" >"$HOLDER_LOG" 2>&1 &
holder_process=$!
exec 9>"$HOLDER_FIFO"
printf "SELECT pg_advisory_lock(hashtextextended('%s', 8317));\n" "$LOCK_NAME" >&9

lock_observed=0
for _ in $(seq 1 20); do
  result="$("${PSQL[@]}" -X -v ON_ERROR_STOP=1 -d "$DATABASE_NAME" -Atqc \
    "SELECT pg_try_advisory_lock(hashtextextended('$LOCK_NAME', 8317));")"
  if [ "$result" = "f" ]; then lock_observed=1; break; fi
  sleep 0.25
done
[ "$lock_observed" -eq 1 ] || { echo "İlk process lock alamadı." >&2; exit 4; }
echo "worker-lock: first-process-owned"

[ "$(ps -o comm= -p "$holder_process" 2>/dev/null | xargs)" = "psql" ] \
  || { echo "Kilit sahibi psql process bulunamadı." >&2; exit 5; }
kill -KILL "$holder_process"
wait "$holder_process" 2>/dev/null || true

recovered=0
for _ in $(seq 1 20); do
  result="$("${PSQL[@]}" -X -v ON_ERROR_STOP=1 -d "$DATABASE_NAME" -Atqc \
    "SELECT pg_try_advisory_lock(hashtextextended('$LOCK_NAME', 8317));")"
  if [ "$result" = "t" ]; then recovered=1; break; fi
  sleep 0.25
done
[ "$recovered" -eq 1 ] || { echo "İkinci process lock devralamadı." >&2; exit 6; }
echo "worker-lock-process-regression: OK"
