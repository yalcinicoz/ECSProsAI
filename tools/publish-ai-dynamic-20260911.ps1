param([switch]$ResumeAdminOnly, [switch]$StockDetail, [switch]$BindingGuard, [switch]$BindingReplacement, [switch]$CardWindow, [switch]$FinalReturns, [switch]$ResumePrepared)
$ErrorActionPreference = 'Stop'
$release = '20260911_ai_dynamic_returns'
if ($StockDetail) { $release = '20260911_ai_stock_detail' }
if ($BindingGuard) { $StockDetail = $true; $release = '20260911_ai_binding_guard' }
if ($BindingReplacement) { $StockDetail = $true; $release = '20260911_ai_binding_replacement' }
if ($CardWindow) { $StockDetail = $true; $release = '20260911_ai_card_window' }
if ($FinalReturns) { $StockDetail = $true; $release = '20260911_final_returns_6ae426a8' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cfg = Get-Content -Raw -LiteralPath (Join-Path $root 'appsettingsTest.json') | ConvertFrom-Json
$ssh = 'C:\Windows\System32\OpenSSH\ssh.exe'
$scp = 'C:\Windows\System32\OpenSSH\scp.exe'
function Remote($node, [string]$command) {
    $command = $command.Replace("`r`n", "`n")
    & $ssh -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=15 -i $node.PrivateKeyPath -p $node.Port "$($node.Username)@$($node.Host)" $command
    if ($LASTEXITCODE -ne 0) { throw 'Remote step failed; later nodes were not activated.' }
}
if ($cfg.Infrastructure.SSH.Api1.Host -ne '5.39.57.245' -or $cfg.Infrastructure.SSH.Api2.Host -ne '51.178.208.58' -or $cfg.Infrastructure.SSH.NginxLb.Host -ne '51.178.208.56') { throw 'Unexpected deployment host' }
$apiDir = Join-Path $root 'output/publish/ai-dynamic-20260911'
if ($StockDetail) { $apiDir = Join-Path $root 'output/publish/ai-stock-detail-20260911' }
if ($BindingGuard) { $apiDir = Join-Path $root 'output/publish/ai-binding-guard-20260911' }
if ($BindingReplacement) { $apiDir = Join-Path $root 'output/publish/ai-binding-replacement-20260911' }
if ($CardWindow) { $apiDir = Join-Path $root 'output/publish/ai-card-window-20260911' }
if ($FinalReturns) { $apiDir = Join-Path $root 'output/publish/ai-final-6ae426a8' }
$adminDir = Join-Path $root 'admin/dist'
if (-not (Test-Path -LiteralPath (Join-Path $apiDir 'ECSPros.Api.dll'))) { throw 'API publish missing' }
$assets = Get-ChildItem -LiteralPath (Join-Path $adminDir 'assets') -Filter 'index-*.js'
if ($FinalReturns -and -not ($assets | Where-Object { $s=Get-Content -Raw -LiteralPath $_.FullName; $s.Contains('Teslimatsız İade') -and $s.Contains('Ödenecek') -and $s.Contains('Kapanan') })) { throw 'Return labels missing from admin package' }
if (-not ($assets | Where-Object { (Get-Content -Raw -LiteralPath $_.FullName).Contains('İadeler — detay ve özet') })) { throw 'Admin build is stale' }
if ($StockDetail -and -not ($assets | Where-Object { (Get-Content -Raw -LiteralPath $_.FullName).Contains('Güncel stok — detay ve özet') })) { throw 'Stock detail admin build is stale' }
if ($CardWindow -and -not ($assets | Where-Object { $content = Get-Content -Raw -LiteralPath $_.FullName; $content.Contains('cardWindowMonths') -and $content.Contains('gözlem süresi tamamlanmamış kartlar dışarıda kalır') -and $content.Contains('Personel işlem kayıtları') })) { throw 'Card window admin build is stale' }
$apiArchive = Join-Path $root "output/$release-api.tar.gz"
$adminArchive = Join-Path $root "output/$release-admin.tar.gz"
if (-not $ResumeAdminOnly -and -not $ResumePrepared) {
foreach ($archive in @($apiArchive,$adminArchive)) { if (Test-Path -LiteralPath $archive) { throw 'Existing archive preserved' } }
& tar -czf $apiArchive --exclude='appsettings*.json' -C $apiDir .
if ($LASTEXITCODE -ne 0) { throw 'API packaging failed' }
& tar -czf $adminArchive -C $adminDir .
if ($LASTEXITCODE -ne 0) { throw 'Admin packaging failed' }
} elseif (-not (Test-Path -LiteralPath $adminArchive) -or -not (Test-Path -LiteralPath $apiArchive)) { throw 'Resume archives missing' }
$apiHash = (Get-FileHash -LiteralPath $apiArchive -Algorithm SHA256).Hash.ToLowerInvariant()
$adminHash = (Get-FileHash -LiteralPath $adminArchive -Algorithm SHA256).Hash.ToLowerInvariant()
$dllHash = (Get-FileHash -LiteralPath (Join-Path $apiDir 'ECSPros.Api.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($name in $(if ($ResumeAdminOnly) { @('NginxLb') } else { @('Api1','Api2','NginxLb') })) {
    $node = $cfg.Infrastructure.SSH.$name
    $isAdmin = $name -eq 'NginxLb'
    $archive = if ($isAdmin) { $adminArchive } else { $apiArchive }
    $hash = if ($isAdmin) { $adminHash } else { $apiHash }
    $remoteArchive = "/tmp/$release-$(if($isAdmin){'admin'}else{'api'}).tar.gz"
    $present = Remote $node "if test -e '$remoteArchive'; then echo present; else echo missing; fi"
    if ($present -eq 'present') {
        if (-not $ResumePrepared) { throw 'Existing remote archive preserved' }
        Remote $node "echo '$hash  $remoteArchive' | sha256sum -c -"
    } else {
        & $scp -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=15 -i $node.PrivateKeyPath -P $node.Port $archive "$($node.Username)@$($node.Host):$remoteArchive"
        if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
    }
    $script = if ($isAdmin) { @'
set -eu
target=/usr/share/nginx/admin-releases/20260911_ai_dynamic_returns
link=/usr/share/nginx/html/admin
test -L "$link"
previous=$(readlink -f "$link")
case "$previous" in /usr/share/nginx/admin-releases/*) ;; *) exit 10;; esac
test ! -e "$target"
printf '%s  %s\n' '__HASH__' '__ARCHIVE__' | sha256sum -c -
mkdir "$target"
tar -xzf '__ARCHIVE__' -C "$target"
test -f "$target/index.html"
ln -s "$target" "$link.20260911_ai_dynamic_returns"
mv -Tf "$link.20260911_ai_dynamic_returns" "$link"
echo "Admin activated: $(readlink -f "$link")"
rm -- '__ARCHIVE__'
'@ } else { @'
set -eu
target=/opt/ECSProsAI/releases/20260911_ai_dynamic_returns
link=/opt/ECSProsAI/current
test -L "$link"
previous=$(readlink -f "$link")
case "$previous" in /opt/ECSProsAI/releases/*) ;; *) exit 10;; esac
test ! -e "$target"
pid=$(systemctl show ecspros.service -p MainPID --value)
tr '\0' '\n' < /proc/$pid/environ | grep -qx 'Node__MigrateOnStartup=false'
url=$(tr '\0' '\n' < /proc/$pid/environ | sed -n 's/^ASPNETCORE_URLS=//p')
case "$url" in http://192.168.0.245:5050|http://192.168.0.58:5050) ;; *) exit 11;; esac
curl -fsS --max-time 10 "$url/ready" >/dev/null
printf '%s  %s\n' '__HASH__' '__ARCHIVE__' | sha256sum -c -
mkdir "$target"
tar -xzf '__ARCHIVE__' -C "$target"
printf '%s  %s\n' '__DLLHASH__' "$target/ECSPros.Api.dll" | sha256sum -c -
cp -pL "$previous"/appsettings*.json "$target/"
python3 - "$target/appsettings.Production.json" <<'PY'
import json, sys
p=sys.argv[1]
with open(p) as f: d=json.load(f)
if '__PRESERVE_CONFIG__' == 'true':
 assert d.get('AiReporting',{}).get('DynamicEnabled') is True, 'Dynamic reporting is not enabled; configuration preserved'
else:
 d.setdefault('AiReporting',{})['DynamicEnabled']=True
 with open(p,'w') as f: json.dump(d,f,ensure_ascii=False,indent=2)
PY
ln -s "$target" "$link.20260911_ai_dynamic_returns"
mv -Tf "$link.20260911_ai_dynamic_returns" "$link"
if ! systemctl restart ecspros.service || ! curl -fsS --max-time 5 --retry 10 --retry-delay 2 --retry-all-errors "$url/ready" >/dev/null; then
 ln -s "$previous" "$link.rollback.20260911_ai_dynamic_returns"
 mv -Tf "$link.rollback.20260911_ai_dynamic_returns" "$link"
 systemctl restart ecspros.service
 echo 'Activation failed; previous link restored.' >&2
 exit 12
fi
systemctl is-active ecspros.service
curl -fsS --max-time 10 "$url/ready"
echo "API activated: $(readlink -f "$link")"
rm -- '__ARCHIVE__'
'@ }
    $script = $script.Replace('20260911_ai_dynamic_returns',$release).Replace('__PRESERVE_CONFIG__',([string][bool]$StockDetail).ToLowerInvariant()).Replace('__HASH__',$hash).Replace('__DLLHASH__',$dllHash).Replace('__ARCHIVE__',$remoteArchive)
    Write-Output "Activating $name"
    Remote $node $script
}
foreach ($archive in @($apiArchive,$adminArchive)) {
    $resolved = [IO.Path]::GetFullPath($archive)
    if ([IO.Path]::GetDirectoryName($resolved) -ne (Join-Path $root 'output')) { throw 'Unexpected cleanup target' }
    Remove-Item -LiteralPath $resolved
}
Write-Output 'Release activation completed; task archives cleaned. Previous releases retained.'
