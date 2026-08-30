#!/usr/bin/env bash
# FAZ 11 / K2 — atomik current symlink, health gate, rollback ve güvenli retention.
set -euo pipefail
DEPLOY_ROOT="${1:-}"; RELEASE_ID="${2:-}"; READY_URL="${3:-http://127.0.0.1:5000/ready}"
SERVICE_NAME="${SERVICE_NAME:-ecspros.service}"; RETAIN_RELEASES="${RETAIN_RELEASES:-5}"
[ -n "$DEPLOY_ROOT" ] && [ -n "$RELEASE_ID" ] || { echo "Kullanım: activate-release.sh /opt/ECSProsAI RELEASE_ID READY_URL" >&2; exit 2; }
case "$DEPLOY_ROOT" in /*) ;; *) echo "DEPLOY_ROOT mutlak yol olmalı." >&2; exit 2 ;; esac
[ "$DEPLOY_ROOT" != "/" ] || { echo "DEPLOY_ROOT / olamaz." >&2; exit 2; }
[[ "$RELEASE_ID" =~ ^[0-9]{8}T[0-9]{6}Z_[A-Za-z0-9._-]+$ ]] || { echo "Geçersiz RELEASE_ID." >&2; exit 2; }
[[ "$RETAIN_RELEASES" =~ ^[1-9][0-9]*$ ]] || { echo "RETAIN_RELEASES pozitif sayı olmalı." >&2; exit 2; }

ROOT_REAL="$(realpath -m "$DEPLOY_ROOT")"; RELEASES_REAL="$(realpath -m "$ROOT_REAL/releases")"
TARGET_REAL="$(realpath -m "$RELEASES_REAL/$RELEASE_ID")"
case "$TARGET_REAL" in "$RELEASES_REAL"/*) ;; *) echo "Release yolu güvenli kökün dışında." >&2; exit 3 ;; esac
[ -f "$TARGET_REAL/ECSPros.Api.dll" ] || { echo "Release eksik: ECSPros.Api.dll yok." >&2; exit 3; }
[ -f "$TARGET_REAL/appsettings.Production.json" ] || { echo "Production config yok." >&2; exit 3; }

CURRENT_LINK="$ROOT_REAL/current"; PREVIOUS_TARGET=""
if [ -L "$CURRENT_LINK" ]; then PREVIOUS_TARGET="$(readlink -f "$CURRENT_LINK")"; fi
TEMP_LINK="$ROOT_REAL/.current.$RELEASE_ID"
ln -sfn "$TARGET_REAL" "$TEMP_LINK"; mv -Tf "$TEMP_LINK" "$CURRENT_LINK"
rollback() {
  if [ -n "$PREVIOUS_TARGET" ] && [ -d "$PREVIOUS_TARGET" ]; then
    echo "Health başarısız; önceki release'e dönülüyor." >&2
    ln -sfn "$PREVIOUS_TARGET" "$TEMP_LINK"; mv -Tf "$TEMP_LINK" "$CURRENT_LINK"
    systemctl restart "$SERVICE_NAME"
  else echo "Health başarısız; önceki current yok." >&2; fi
}

systemctl restart "$SERVICE_NAME"
if ! curl --fail --silent --show-error --max-time 5 --retry 30 --retry-delay 2 --retry-all-errors "$READY_URL" >/dev/null; then
  rollback; exit 5
fi
echo "Health başarılı: $READY_URL"

mapfile -t RELEASE_DIRS < <(find "$RELEASES_REAL" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort -r)
kept=0
for directory_name in "${RELEASE_DIRS[@]}"; do
  directory_real="$(realpath -m "$RELEASES_REAL/$directory_name")"
  [ "$directory_real" = "$TARGET_REAL" ] && { kept=$((kept+1)); continue; }
  if [ "$kept" -lt "$RETAIN_RELEASES" ]; then kept=$((kept+1)); continue; fi
  case "$directory_real" in "$RELEASES_REAL"/*) rm -rf -- "$directory_real" ;;
    *) echo "Güvenlik: release kökü dışı yol silinmedi." >&2 ;; esac
done
echo "Aktivasyon tamamlandı: $RELEASE_ID"
