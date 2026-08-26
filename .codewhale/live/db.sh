#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI

PASS=$(jq -r '.ConnectionStrings.DefaultConnection' src/ECSPros.Api/appsettings.json | sed -n 's/.*Password=\([^;]*\).*/\1/p')
export PGPASSWORD="$PASS"
PSQL="psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t"

echo "===== PostgreSQL sürüm / bağlantı ====="
psql -h 127.0.0.1 -U ecommerce -d ecommerce_db -X -A -t -c "select version();" 2>&1 | head -1

echo
echo "===== pg_stat_statements etkin mi? ====="
$PSQL -c "select extname from pg_extension where extname='pg_stat_statements';" 2>&1

echo
echo "===== aktif bağlantı sayısı ====="
$PSQL -c "select count(*) from pg_stat_activity where datname='ecommerce_db';" 2>&1

echo
echo "===== DB boyutu ====="
$PSQL -c "select pg_size_pretty(pg_database_size('ecommerce_db'));" 2>&1

echo
echo "===== en büyük tablolar ====="
$PSQL -c "select relname, pg_size_pretty(pg_total_relation_size(relid)) from pg_stat_user_tables order by pg_total_relation_size(relid) desc limit 15;" 2>&1

echo
echo "===== 1 sn'den uzun süren aktif sorgular ====="
$PSQL -c "select pid, state, now()-query_start as sure, left(query,100) from pg_stat_activity where state='active' and now()-query_start > interval '1 second';" 2>&1
