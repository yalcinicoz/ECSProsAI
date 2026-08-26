#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI
PASS=$(jq -r '.ConnectionStrings.DefaultConnection' src/ECSPros.Api/appsettings.json | sed -n 's/.*Password=\([^;]*\).*/\1/p')
export PGPASSWORD="$PASS"

CODE=$(psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t -c 'select "Code" from catalog.products where "IsDeleted"=false and "Code" is not null order by "CreatedAt" desc limit 1;' 2>/dev/null | tr -d '[:space:]')
echo "Ürün kodu: [$CODE]"

measure() {
  echo "--- $1 ---"
  for i in 1 2 3; do
    curl -s -o /dev/null -w "  #$i  code=%{http_code}  ttfb=%{time_starttransfer}s  total=%{time_total}s  size=%{size_download}\n" "$2"
  done
}

echo
measure "Ürün detayı (kod: $CODE)" "http://127.0.0.1:5000/urun/$CODE"

echo
echo "===== ürün detayının DB sorgu sayısı tahmini (yavaş olma nedeni) ====="
# Ürün/varyant/attribute/resim satır sayıları (kartezyen çarpım etkisini görmek için)
psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t -c "select 'products', count(*) from catalog.products where \"IsDeleted\"=false union all select 'product_variants', count(*) from catalog.product_variants union all select 'product_variant_attributes', count(*) from catalog.product_variant_attributes union all select 'product_attributes', count(*) from catalog.product_attributes union all select 'product_images', count(*) from catalog.product_images;" 2>&1
