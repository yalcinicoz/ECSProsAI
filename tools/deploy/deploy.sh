#!/usr/bin/env bash
# FAZ 11 / K2 — değişmez release hazırlama ve çok düğüme dağıtım.
# Restart/aktivasyon yapmaz; activate-release.sh health gate ve rollback uygular.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd -P)"
cd "$REPO_ROOT"
MIGRATE=0
for arg in "$@"; do
  case "$arg" in --migrate) MIGRATE=1 ;; *) echo "Bilinmeyen argüman: $arg" >&2; exit 2 ;; esac
done

GIT_SHA="$(git rev-parse --short=12 HEAD 2>/dev/null || echo nogit)"
RELEASE_ID="$(date -u +%Y%m%dT%H%M%SZ)_${GIT_SHA}"
DEPLOY_ROOT="${DEPLOY_ROOT:-/opt/ECSProsAI}"
LOCAL_RELEASES="$DEPLOY_ROOT/releases"
LOCAL_RELEASE="$LOCAL_RELEASES/$RELEASE_ID"
STAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ecspros-deploy.XXXXXXXX")"
PUBLISH_DIR="$STAGE_DIR/publish"
cleanup() {
  case "$STAGE_DIR" in "${TMPDIR:-/tmp}"/ecspros-deploy.*) rm -rf -- "$STAGE_DIR" ;;
    *) echo "Güvenlik: beklenmeyen stage yolu silinmedi: $STAGE_DIR" >&2 ;; esac
}
trap cleanup EXIT

echo "── 1/5 Temiz ve benzersiz publish: $RELEASE_ID"
dotnet publish src/ECSPros.Api/ECSPros.Api.csproj -c Release --no-restore -o "$PUBLISH_DIR"

if [ "$MIGRATE" = "1" ]; then
  echo "── 2/5 Migration'lar (tek deploy düğümünden)"
  MIGRATION_CONTEXTS=(
    "Iam:IamDbContext" "Core:CoreDbContext" "Catalog:CatalogDbContext"
    "Inventory:InventoryDbContext" "Order:OrderDbContext" "Crm:CrmDbContext"
    "Cms:CmsDbContext" "Pos:PosDbContext" "Promotion:PromotionDbContext"
    "Finance:FinanceDbContext" "Fulfillment:FulfillmentDbContext"
    "Integration:IntegrationDbContext" "Storefront:StorefrontDbContext"
    "Accounts:AccountsDbContext" "Requests:RequestsDbContext"
    "Procurement:ProcurementDbContext"
  )
  for item in "${MIGRATION_CONTEXTS[@]}"; do
    module="${item%%:*}"; ctx="${item#*:}"
    project="src/Modules/$module/ECSPros.$module.Infrastructure/ECSPros.$module.Infrastructure.csproj"
    dotnet ef database update --project "$project" \
      --startup-project src/ECSPros.Api/ECSPros.Api.csproj --context "$ctx" \
      --configuration Release --no-build
  done
else
  echo "── 2/5 Migration atlandı (--migrate verilmedi)"
fi

echo "── 3/5 Yerel release hazırlanıyor: $LOCAL_RELEASE"
mkdir -p -- "$LOCAL_RELEASES"
[ ! -e "$LOCAL_RELEASE" ] || { echo "Release zaten var; üzerine yazılmayacak." >&2; exit 3; }
mkdir -- "$LOCAL_RELEASE"
rsync -a -- "$PUBLISH_DIR/" "$LOCAL_RELEASE/"
mkdir -p -- "$DEPLOY_ROOT/config"
if [ -f "$DEPLOY_ROOT/config/appsettings.Production.json" ]; then
  ln -s "$DEPLOY_ROOT/config/appsettings.Production.json" "$LOCAL_RELEASE/appsettings.Production.json"
else
  echo "UYARI: ortak production config yok; aktivasyondan önce oluşturulmalı." >&2
fi

NODES_CONF="tools/deploy/nodes.conf"
echo "── 4/5 Uzak düğümlere değişmez release dağıtımı"
if [ -s "$NODES_CONF" ]; then
  grep -vE '^\s*(#|$)' "$NODES_CONF" | while read -r node_name ssh_target node_root ready_url; do
    [ -n "$node_name" ] && [ -n "$ssh_target" ] && [ -n "$node_root" ] || { echo "Geçersiz nodes.conf satırı" >&2; exit 4; }
    echo "   → $node_name ($ssh_target:$node_root/releases/$RELEASE_ID)"
    ssh "$ssh_target" "mkdir -p '$node_root/releases/$RELEASE_ID' '$node_root/config' '$node_root/tools/deploy'"
    rsync -az -- "$PUBLISH_DIR/" "$ssh_target:$node_root/releases/$RELEASE_ID/"
    rsync -az -- tools/deploy/activate-release.sh "$ssh_target:$node_root/tools/deploy/activate-release.sh"
    ssh "$ssh_target" "if [ -f '$node_root/config/appsettings.Production.json' ]; then ln -sfn '$node_root/config/appsettings.Production.json' '$node_root/releases/$RELEASE_ID/appsettings.Production.json'; else echo 'UYARI: production config yok' >&2; fi"
  done
else
  echo "   Tek düğüm modu; nodes.conf yok/boş."
fi

echo "── 5/5 Aktivasyon talimatları"
echo "   [yerel] sudo bash tools/deploy/activate-release.sh '$DEPLOY_ROOT' '$RELEASE_ID' 'http://127.0.0.1:5000/ready'"
if [ -s "$NODES_CONF" ]; then
  grep -vE '^\s*(#|$)' "$NODES_CONF" | while read -r node_name ssh_target node_root ready_url; do
    echo "   [$node_name] ssh $ssh_target sudo bash '$node_root/tools/deploy/activate-release.sh' '$node_root' '$RELEASE_ID' '${ready_url:-http://127.0.0.1:5000/ready}'"
  done
fi
echo "Release hazır: $RELEASE_ID"
echo "Her düğüm /ready=200 olmadan sonrakini aktive etmeyin. GitHub'a gönderim yapılmadı."
