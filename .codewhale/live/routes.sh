#!/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH
cd /opt/ECSProsAI

echo "===== Storefront controller route'ları ====="
grep -rEn '\[Route\(|\[HttpGet\(|\[HttpPost\(' src/ECSPros.Api/Controllers/Store/*.cs 2>/dev/null | sed 's#src/ECSPros.Api/##' | head -80

echo
echo "===== Üst seviye controller'lar (admin/auth) ====="
grep -rEn '\[Route\(|\[HttpGet\(|\[HttpPost\(' src/ECSPros.Api/Controllers/*.cs 2>/dev/null | sed 's#src/ECSPros.Api/##' | head -60

echo
echo "===== Razor sayfa rotaları (Storefront sayfaları) ====="
find src/ECSPros.Api/Views -maxdepth 2 -name '*.cshtml' 2>/dev/null | sed 's#src/ECSPros.Api/Views##' | head -60
