#!/usr/bin/env bash
# RF3 (2026-09-01) — Pazaryeri sözlüğü SNAPSHOT PAKETİ üretimi (merkezî ortamda koşulur).
# Plan: docs/pazaryeri-referans-ve-esleme-plani.md · K1 kararı: merkezî snapshot paketi.
#
# Paket içeriği (tek tar.gz):
#   manifest.json      — şema sürümü, üretim zamanı, pazaryeri sayıları, sha256'lar
#   marketplace_ref.dump — pg_dump custom format (kategori/özellik/değer/senkron geçmişi;
#                          ~10M satır JSON yerine kanıtlanmış araçla)
#   kategori-eslemeleri.json — platform-geneli (FirmPlatformId NULL) kategori eşlemeleri,
#                          ÜRÜN GRUBU KODU anahtarıyla (kurulumlar arası taşınabilir kimlik;
#                          özellik/değer eşlemeleri Ortak Sözlük kimlik çalışmasına kadar v1 DIŞI)
#
# Kullanım: bash tools/sozluk/sozluk-paketle.sh [çıktı-dizini]
# Bağlantılar appsettings.Production.json'dan okunur; paket adı sozluk-vYYYYMMDDHHMM.tar.gz
set -euo pipefail
cd "$(dirname "$0")/../.."
CIKTI="${1:-/tmp}"
SURUM="$(date -u +%Y%m%d%H%M)"
IS_DIZINI="$(mktemp -d)"
trap 'rm -rf "$IS_DIZINI"' EXIT

oku() { python3 -c "
import json,sys
cs=json.load(open('src/ECSPros.Api/appsettings.Production.json'))['ConnectionStrings']['$1']
d=dict(p.split('=',1) for p in cs.split(';') if '=' in p)
print(d.get('$2',''))"; }

# ── marketplace_ref dump ─────────────────────────────────────────────────────
REF_HOST=$(oku MarketplaceRef Host); REF_DB=$(oku MarketplaceRef Database)
REF_USER=$(oku MarketplaceRef Username); export PGPASSWORD=$(oku MarketplaceRef Password)
echo "1/3 marketplace_ref dökülüyor ($REF_DB)…"
pg_dump -h "$REF_HOST" -U "$REF_USER" -d "$REF_DB" -Fc --no-owner --no-acl \
  -f "$IS_DIZINI/marketplace_ref.dump"
unset PGPASSWORD

# ── Kategori eşlemeleri (grup KODU anahtarıyla) ──────────────────────────────
echo "2/3 kategori eşlemeleri dökülüyor…"
psql -h localhost -U ecommerce -d ecommerce_db -At -c "
SELECT COALESCE(json_agg(row_to_json(t)), '[]'::json) FROM (
  SELECT g.\"Code\" AS group_code, m.\"Marketplace\" AS marketplace,
         m.\"MappingKind\" AS mapping_kind, m.\"TargetExternalId\" AS target_external_id,
         m.\"TargetName\" AS target_name, m.\"TargetPath\" AS target_path,
         m.\"RulesJson\" AS rules_json, m.\"PoolJson\" AS pool_json
  FROM integration.marketplace_category_mappings m
  JOIN definition.product_groups g ON g.\"Id\" = m.\"ProductGroupId\"
  WHERE m.\"FirmPlatformId\" IS NULL AND NOT m.\"IsDeleted\" AND m.\"Status\"='active'
) t" > "$IS_DIZINI/kategori-eslemeleri.json"

# ── Manifest + arşiv ─────────────────────────────────────────────────────────
echo "3/3 manifest + arşiv…"
python3 - "$IS_DIZINI" "$SURUM" <<'PY'
import hashlib, json, sys, os
d, surum = sys.argv[1], sys.argv[2]
def sha(p): return hashlib.sha256(open(p,'rb').read()).hexdigest()
esleme = json.load(open(f"{d}/kategori-eslemeleri.json"))
manifest = {
    "schemaVersion": 1,
    "version": surum,
    "producedAtUtc": __import__('datetime').datetime.utcnow().isoformat() + "Z",
    "files": {
        "marketplace_ref.dump": {"sha256": sha(f"{d}/marketplace_ref.dump"),
                                  "bytes": os.path.getsize(f"{d}/marketplace_ref.dump")},
        "kategori-eslemeleri.json": {"sha256": sha(f"{d}/kategori-eslemeleri.json"),
                                      "count": len(esleme)},
    },
}
json.dump(manifest, open(f"{d}/manifest.json","w"), indent=2)
print(json.dumps({k:v for k,v in manifest.items() if k!="files"}))
PY
PAKET="$CIKTI/sozluk-v$SURUM.tar.gz"
tar -czf "$PAKET" -C "$IS_DIZINI" manifest.json marketplace_ref.dump kategori-eslemeleri.json
echo "PAKET: $PAKET ($(du -h "$PAKET" | cut -f1))"
