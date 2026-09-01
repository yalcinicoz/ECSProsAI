#!/usr/bin/env bash
# RF3 (2026-09-01) — Pazaryeri sözlüğü SNAPSHOT PAKETİNİ kuruluma yükler (idempotent).
#
#   bash tools/sozluk/sozluk-yukle.sh <paket.tar.gz> [hedef-ref-db]
#
# Adımlar: manifest + sha256 doğrulanır → marketplace_ref pg_restore ile YENİDEN yüklenir
# (--clean --if-exists: tablolar paket içeriğiyle birebir; yerel senkron geçmişi de paketinkiyle
# değişir — sözlük zaten merkezî üretim, yerel fark istenmez) → kategori eşlemeleri grup KODU
# üzerinden upsert edilir (yerelde olmayan grup kodu ATLANIR ve raporlanır; mevcut aktif eşleme
# paket değeriyle güncellenir). Aynı paket ikinci kez yüklenirse sonuç değişmez.
# NOT: uygulama restart'ı gerekmez (ref DB her sorguda okunur; eşleme değişimi readiness'ı
# RF5 gereği bir sonraki senkron/sağlık turunda yakalar — istenirse panelden Hazırlığı Hesapla).
set -euo pipefail
cd "$(dirname "$0")/../.."
PAKET="${1:?kullanım: sozluk-yukle.sh <paket.tar.gz> [hedef-ref-db]}"

oku() { python3 -c "
import json,sys
cs=json.load(open('src/ECSPros.Api/appsettings.Production.json'))['ConnectionStrings']['$1']
d=dict(p.split('=',1) for p in cs.split(';') if '=' in p)
print(d.get('$2',''))"; }
REF_HOST=$(oku MarketplaceRef Host); REF_DB="${2:-$(oku MarketplaceRef Database)}"
REF_USER=$(oku MarketplaceRef Username)

IS_DIZINI="$(mktemp -d)"
trap 'rm -rf "$IS_DIZINI"' EXIT
tar -xzf "$PAKET" -C "$IS_DIZINI"

echo "1/3 manifest doğrulanıyor…"
python3 - "$IS_DIZINI" <<'PY'
import hashlib, json, sys
d = sys.argv[1]
m = json.load(open(f"{d}/manifest.json"))
assert m["schemaVersion"] == 1, f"desteklenmeyen şema sürümü: {m['schemaVersion']}"
for ad, bilgi in m["files"].items():
    gercek = hashlib.sha256(open(f"{d}/{ad}","rb").read()).hexdigest()
    assert gercek == bilgi["sha256"], f"sha256 uyuşmuyor: {ad}"
print(f"  paket v{m['version']} · üretim {m['producedAtUtc']} · doğrulandı ✓")
PY

echo "2/3 marketplace_ref geri yükleniyor ($REF_DB)…"
export PGPASSWORD=$(oku MarketplaceRef Password)
pg_restore -h "$REF_HOST" -U "$REF_USER" -d "$REF_DB" \
  --clean --if-exists --no-owner --no-acl "$IS_DIZINI/marketplace_ref.dump"
unset PGPASSWORD

echo "3/3 kategori eşlemeleri upsert ediliyor…"
python3 - "$IS_DIZINI" <<'PY'
import json, subprocess, sys
d = sys.argv[1]
esleme = json.load(open(f"{d}/kategori-eslemeleri.json"))
if not esleme:
    print("  pakette eşleme yok — atlandı."); sys.exit(0)
# psql'e tek transaction'lık betik: geçici tabloya kopyala, koda göre upsert
satirlar = "\n".join(
    "\t".join((r["group_code"], r["marketplace"], r["mapping_kind"],
               r.get("target_external_id") or "\\N", r.get("target_name") or "\\N",
               r.get("target_path") or "\\N",
               json.dumps(r["rules_json"]) if r.get("rules_json") else "\\N",
               json.dumps(r["pool_json"]) if r.get("pool_json") else "\\N"))
    for r in esleme)
sql = """
BEGIN;
CREATE TEMP TABLE paket_esleme (group_code text, marketplace text, mapping_kind text,
  target_external_id text, target_name text, target_path text, rules_json text, pool_json text);
COPY paket_esleme FROM STDIN;
""" + satirlar + "\n\\.\n" + """
-- yerelde OLMAYAN grup kodları raporlanır (uygulanmaz)
SELECT 'ATLANDI (grup yok): ' || pe.group_code
FROM paket_esleme pe LEFT JOIN definition.product_groups g
  ON g."Code" = pe.group_code AND NOT g."IsDeleted"
WHERE g."Id" IS NULL;
-- upsert: mevcut platform-geneli kayıt güncellenir, yoksa eklenir
INSERT INTO integration.marketplace_category_mappings
  ("Id","Marketplace","ProductGroupId","FirmPlatformId","MappingKind","TargetExternalId",
   "TargetName","TargetPath","RulesJson","PoolJson","Status","CreatedAt","IsDeleted")
SELECT gen_random_uuid(), pe.marketplace, g."Id", NULL, pe.mapping_kind, pe.target_external_id,
       pe.target_name, pe.target_path, pe.rules_json, pe.pool_json, 'active', now(), false
FROM paket_esleme pe JOIN definition.product_groups g ON g."Code"=pe.group_code AND NOT g."IsDeleted"
-- NOT: unique index FirmPlatformId NULL satırlarını yakalamaz (NULL != NULL) — idempotens
-- ON CONFLICT ile değil NOT EXISTS ile sağlanır.
WHERE NOT EXISTS (
  SELECT 1 FROM integration.marketplace_category_mappings m
  WHERE m."Marketplace"=pe.marketplace AND m."ProductGroupId"=g."Id"
    AND m."FirmPlatformId" IS NULL AND m."IsDeleted"=false);
UPDATE integration.marketplace_category_mappings m SET
  "MappingKind"=pe.mapping_kind, "TargetExternalId"=pe.target_external_id,
  "TargetName"=pe.target_name, "TargetPath"=pe.target_path,
  "RulesJson"=pe.rules_json, "PoolJson"=pe.pool_json,
  "Status"='active', "StatusNote"=NULL, "IsDeleted"=false, "UpdatedAt"=now()
FROM paket_esleme pe JOIN definition.product_groups g ON g."Code"=pe.group_code AND NOT g."IsDeleted"
WHERE m."ProductGroupId"=g."Id" AND m."Marketplace"=pe.marketplace AND m."FirmPlatformId" IS NULL;
SELECT 'eşleme upsert: ' || count(*) FROM paket_esleme;
COMMIT;
"""
r = subprocess.run(["psql","-h","localhost","-U","ecommerce","-d","ecommerce_db","-q","-At"],
                   input=sql, capture_output=True, text=True)
print(r.stdout.strip() or "(çıktı yok)")
if r.returncode != 0:
    print(r.stderr[-800:]); sys.exit(1)
PY
echo "TAMAM — sözlük yüklendi."
