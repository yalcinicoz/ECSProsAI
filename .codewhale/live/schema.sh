#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI
PASS=$(jq -r '.ConnectionStrings.DefaultConnection' src/ECSPros.Api/appsettings.json | sed -n 's/.*Password=\([^;]*\).*/\1/p')
export PGPASSWORD="$PASS"

echo "===== products tablo kolonları ====="
psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t -c "select column_name from information_schema.columns where table_name='products' order by ordinal_position;" 2>&1 | head -40

echo
echo "===== schema'lar ====="
psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t -c "select table_schema, count(*) from information_schema.tables group by table_schema order by 2 desc;" 2>&1 | head -20
