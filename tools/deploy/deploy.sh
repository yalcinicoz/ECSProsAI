#!/usr/bin/env bash
# FAZ 10 / A10 (2026-08-30) — çok düğümlü deploy betiği.
#
# Akış: temiz publish → (--migrate ile) migration'lar → her uzak düğüme rsync →
#       sıralı restart talimatı (/ready bekleyerek — iki düğüm aynı anda kapanmaz).
#
# Düğüm listesi: tools/deploy/nodes.conf — satır başına bir uzak düğüm:
#   ad  user@host  /opt/ECSProsAI/publish  http://host:5000/ready
# Dosya yoksa/boşsa tek sunucu modu: yalnız yerel publish yapılır.
#
# NOT: restart sudo ister ve bu betik sudo ÇALIŞTIRMAZ (proje kuralı) — her düğüm için
# çalıştırılacak komutu sırayla YAZDIRIR; operatör her adımda /ready 200 görmeden
# sonraki düğüme geçmemelidir. Migration'lar TEK düğümden uygulanır (bu betiğin
# koştuğu makine); diğer düğümlerde Node__MigrateOnStartup=false olmalıdır.
set -euo pipefail
cd "$(dirname "$0")/../.."

MIGRATE=0
for arg in "$@"; do [ "$arg" = "--migrate" ] && MIGRATE=1; done

echo "── 1/4 Temiz publish (…/publish)"
dotnet publish src/ECSPros.Api/ECSPros.Api.csproj -c Release -o "$PWD/publish"

if [ "$MIGRATE" = "1" ]; then
  echo "── 2/4 Migration'lar (tek düğümden — tüm context'ler)"
  for ctx in IamDbContext CoreDbContext CatalogDbContext InventoryDbContext OrderDbContext \
             CrmDbContext CmsDbContext PosDbContext PromotionDbContext FinanceDbContext \
             FulfillmentDbContext IntegrationDbContext StorefrontDbContext AccountsDbContext; do
    dotnet ef database update --project src/ECSPros.Api/ECSPros.Api.csproj --context "$ctx"
  done
else
  echo "── 2/4 Migration atlandı (--migrate verilmedi)"
fi

NODES_CONF="tools/deploy/nodes.conf"
if [ -s "$NODES_CONF" ]; then
  echo "── 3/4 Uzak düğümlere rsync"
  grep -vE '^\s*(#|$)' "$NODES_CONF" | while read -r ad hedef dizin ready; do
    echo "   → $ad ($hedef:$dizin)"
    rsync -az --delete-after --exclude 'appsettings.Production.json' "$PWD/publish/" "$hedef:$dizin/"
    # Production config'i ayrıca ve SİLMEDEN gönder (drift E4 önlemi — dosya her düğümde aynı olmalı)
    rsync -az "$PWD/publish/appsettings.Production.json" "$hedef:$dizin/appsettings.Production.json"
  done
else
  echo "── 3/4 Tek sunucu modu (nodes.conf yok/boş) — rsync yok"
fi

echo "── 4/4 SIRALI restart talimatı (operatör çalıştırır, her adımda /ready 200 beklenir):"
echo "   [yerel] sudo systemctl restart ecspros && curl -s -o /dev/null -w 'ready %{http_code}\n' --retry 30 --retry-delay 2 --retry-all-errors http://localhost:5000/ready"
if [ -s "$NODES_CONF" ]; then
  grep -vE '^\s*(#|$)' "$NODES_CONF" | while read -r ad hedef dizin ready; do
    echo "   [$ad] ssh $hedef 'sudo systemctl restart ecspros' && curl -s -o /dev/null -w 'ready %{http_code}\n' --retry 30 --retry-delay 2 --retry-all-errors ${ready:-http://DUZENLE:5000/ready}"
  done
fi
echo "Bitti — bir düğümün /ready'si 200 olmadan SONRAKİ düğümü yeniden başlatmayın."
