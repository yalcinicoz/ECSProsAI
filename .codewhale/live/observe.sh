#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI

echo "===== systemctl ecspros ====="
systemctl is-active ecspros
systemctl status ecspros --no-pager 2>&1 | head -15

echo
echo "===== docker compose ps ====="
docker compose ps 2>&1 | head -20

echo
echo "===== surec kaynak kullanimi (ECSPros.Api) ====="
ps aux | grep -E 'ECSPros\.Api|dotnet' | grep -v grep | head -10

echo
echo "===== free -h ====="
free -h

echo
echo "===== nproc / uptime ====="
nproc
uptime
