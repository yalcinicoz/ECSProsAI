#!/usr/bin/env bash
# FAZ 11 / K2 — systemd/curl taklitleriyle atomik aktivasyon, rollback ve retention regresyonu.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd -P)"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/ecspros-activate-test.XXXXXXXX")"
cleanup() {
  case "$TEST_ROOT" in "${TMPDIR:-/tmp}"/ecspros-activate-test.*) rm -rf -- "$TEST_ROOT" ;;
    *) echo "Güvenlik: beklenmeyen test yolu silinmedi: $TEST_ROOT" >&2 ;;
  esac
}
trap cleanup EXIT

DEPLOY_ROOT="$TEST_ROOT/deploy"
FAKE_BIN="$TEST_ROOT/bin"
CALL_LOG="$TEST_ROOT/calls.log"
mkdir -p "$DEPLOY_ROOT/releases" "$FAKE_BIN"

make_release() {
  local release_id="$1"
  mkdir -p "$DEPLOY_ROOT/releases/$release_id"
  : > "$DEPLOY_ROOT/releases/$release_id/ECSPros.Api.dll"
  printf '{}\n' > "$DEPLOY_ROOT/releases/$release_id/appsettings.Production.json"
}

cat > "$FAKE_BIN/systemctl" <<'EOF'
#!/usr/bin/env bash
printf 'systemctl %s\n' "$*" >> "$CALL_LOG"
EOF
cat > "$FAKE_BIN/curl" <<'EOF'
#!/usr/bin/env bash
printf 'curl %s\n' "$*" >> "$CALL_LOG"
exit "${FAKE_CURL_EXIT:-0}"
EOF
chmod +x "$FAKE_BIN/systemctl" "$FAKE_BIN/curl"
export PATH="$FAKE_BIN:$PATH" CALL_LOG

OLD="20260830T100000Z_old"
STALE="20260830T090000Z_stale"
GOOD="20260830T110000Z_good"
BAD="20260830T120000Z_bad"
make_release "$OLD"
make_release "$STALE"
make_release "$GOOD"
ln -s "$DEPLOY_ROOT/releases/$OLD" "$DEPLOY_ROOT/current"
if [ ! -L "$DEPLOY_ROOT/current" ]; then
  echo "deploy-activation-regression: SKIP (bu ortam gerçek POSIX symlink desteklemiyor)"
  exit 0
fi

RETAIN_RELEASES=2 FAKE_CURL_EXIT=0 \
  bash "$REPO_ROOT/tools/deploy/activate-release.sh" "$DEPLOY_ROOT" "$GOOD" "http://127.0.0.1:5000/ready"
[ "$(readlink -f "$DEPLOY_ROOT/current")" = "$(readlink -f "$DEPLOY_ROOT/releases/$GOOD")" ] \
  || { echo "Başarılı aktivasyon current symlink'i değiştirmedi." >&2; exit 10; }
[ ! -d "$DEPLOY_ROOT/releases/$STALE" ] \
  || { echo "Retention eski release'i temizlemedi." >&2; exit 11; }

make_release "$BAD"
set +e
RETAIN_RELEASES=2 FAKE_CURL_EXIT=22 \
  bash "$REPO_ROOT/tools/deploy/activate-release.sh" "$DEPLOY_ROOT" "$BAD" "http://127.0.0.1:5000/ready"
status=$?
set -e
[ "$status" -eq 5 ] || { echo "Health hatası beklenen exit=5 yerine exit=$status döndürdü." >&2; exit 12; }
[ "$(readlink -f "$DEPLOY_ROOT/current")" = "$(readlink -f "$DEPLOY_ROOT/releases/$GOOD")" ] \
  || { echo "Health hatasında önceki release'e rollback yapılmadı." >&2; exit 13; }
[ "$(grep -c '^systemctl restart ecspros.service$' "$CALL_LOG")" -eq 3 ] \
  || { echo "Beklenen restart çağrıları oluşmadı." >&2; exit 14; }

echo "deploy-activation-regression: OK"
