#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI

echo "===== istemci araçları ====="
for c in redis-cli psql pg_isready curl jq; do
  p=$(command -v "$c" 2>/dev/null)
  echo "$c -> ${p:-YOK}"
done

echo
echo "===== journalctl: Redis cache durum satırı ====="
journalctl -u ecspros --no-pager 2>/dev/null | grep -i "Redis cache" | tail -5

echo
echo "===== journalctl: son 24s hata/uyarı (ERR/WRN/Exception/fail) ====="
journalctl -u ecspros --since "24 hours ago" --no-pager 2>/dev/null | grep -iE 'ERR|WRN|Exception|fail|unhandled|timeout' | grep -viE 'Legacy senkron|Kuyruk boş' | tail -40

echo
echo "===== journalctl: toplam satır ve seviye dağılımı (son 24s) ====="
journalctl -u ecspros --since "24 hours ago" --no-pager 2>/dev/null | grep -oE '\[[0-9:]+ (INF|WRN|ERR|FTL|DBG)\]' | awk '{print $2}' | sort | uniq -c
