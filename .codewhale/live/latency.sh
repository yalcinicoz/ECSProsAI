#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI

PASS=$(jq -r '.ConnectionStrings.DefaultConnection' src/ECSPros.Api/appsettings.json | sed -n 's/.*Password=\([^;]*\).*/\1/p')
export PGPASSWORD="$PASS"

# Örnek ürün kodu ve ürün grubu slug'ı al
CODE=$(psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t -c "select code from products where is_deleted=false and code is not null order by created_at desc limit 1;" 2>/dev/null | tr -d '[:space:]')
echo "Örnek ürün kodu: $CODE"

measure() {
  local url="$1"; local label="$2"; local n="$3"
  echo "--- $label ($url) ---"
  for i in $(seq 1 "$n"); do
    curl -s -o /dev/null -w "  #$i  code=%{http_code}  ttfb=%{time_starttransfer}s  total=%{time_total}s  size=%{size_download}\n" "$url"
  done
}

echo
echo "############ MAĞAZA (doğrudan API :5000) ############"
measure "http://127.0.0.1:5000/" "Ana sayfa" 3
measure "http://127.0.0.1:5000/urun-listesi" "Ürün listesi" 3
measure "http://127.0.0.1:5000/urun/$CODE" "Ürün detayı" 3
measure "http://127.0.0.1:5000/hakkimizda" "Kurumsal (hafif)" 2
measure "http://127.0.0.1:5000/sepet" "Sepet" 2

echo
echo "############ MAĞAZA (nginx :80 üzerinden) ############"
measure "http://127.0.0.1/" "Ana sayfa (nginx)" 2
measure "http://127.0.0.1/urun-listesi" "Ürün listesi (nginx)" 2

echo
echo "############ ADMIN API (uç erişim kontrolü) ############"
measure "http://127.0.0.1:5000/api/orders" "Sipariş listesi (tokensiz)" 1
measure "http://127.0.0.1:5000/api/catalog/products" "Ürünler (tokensiz)" 1
