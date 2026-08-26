#!/usr/bin/env bash
cd /opt/ECSProsAI
for m in Iam Core Catalog Inventory Crm Order Finance Promotion Cms Pos Fulfillment Integration Accounts Storefront Requests; do
  dir=$(find src/Modules -path "*ECSPros.$m.Infrastructure/Migrations" -type d 2>/dev/null | head -1)
  if [ -z "$dir" ]; then echo "$m: NO_DIR"; continue; fi
  cnt=$(find "$dir" -maxdepth 1 -name '*.cs' ! -name '*.Designer.cs' ! -name '*ModelSnapshot.cs' | wc -l)
  echo "$m: $cnt"
done
