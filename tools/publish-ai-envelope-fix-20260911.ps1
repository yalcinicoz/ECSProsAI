$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cfg = Get-Content -Raw -LiteralPath (Join-Path $root 'appsettingsTest.json') | ConvertFrom-Json
$dll = Join-Path $root 'output/publish/ai-stock-detail-20260911/ECSPros.Api.dll'
$hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($cfg.Infrastructure.SSH.Api1.Host -ne '5.39.57.245' -or $cfg.Infrastructure.SSH.Api2.Host -ne '51.178.208.58') { throw 'Unexpected hosts' }
$ssh = 'C:\Windows\System32\OpenSSH\ssh.exe'
$scp = 'C:\Windows\System32\OpenSSH\scp.exe'
function Remote($node, [string]$command) {
    & $ssh -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=15 -i $node.PrivateKeyPath -p $node.Port "$($node.Username)@$($node.Host)" $command
    if ($LASTEXITCODE -ne 0) { throw 'Remote step failed; next node preserved' }
}
foreach ($name in @('Api1','Api2')) {
    $node = $cfg.Infrastructure.SSH.$name
    Remote $node "test ! -e /tmp/ecspros-ai-envelope-20260911.dll"
    & $scp -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=15 -i $node.PrivateKeyPath -P $node.Port $dll "$($node.Username)@$($node.Host):/tmp/ecspros-ai-envelope-20260911.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
    $script = @'
set -eu
link=/opt/ECSProsAI/current
previous=/opt/ECSProsAI/releases/20260911_ai_stock_detail
target=/opt/ECSProsAI/releases/20260911_ai_stock_envelope
test -L "$link"
test "$(readlink -f "$link")" = "$previous"
test ! -e "$target"
printf '%s  %s\n' 'c6adccb9f361a93571db89740155e04f0897481f3b1e3bd2e16f9b1a8d434029' "$previous/ECSPros.Api.dll" | sha256sum -c -
printf '%s  %s\n' '__HASH__' '/tmp/ecspros-ai-envelope-20260911.dll' | sha256sum -c -
pid=$(systemctl show ecspros.service -p MainPID --value)
tr '\0' '\n' < /proc/$pid/environ | grep -qx 'Node__MigrateOnStartup=false'
url=$(tr '\0' '\n' < /proc/$pid/environ | sed -n 's/^ASPNETCORE_URLS=//p')
case "$url" in http://192.168.0.245:5050|http://192.168.0.58:5050) ;; *) exit 11;; esac
curl -fsS --max-time 10 "$url/ready" >/dev/null
mkdir "$target"
cp -a "$previous/." "$target/"
cp /tmp/ecspros-ai-envelope-20260911.dll "$target/ECSPros.Api.dll"
printf '%s  %s\n' '__HASH__' "$target/ECSPros.Api.dll" | sha256sum -c -
ln -s "$target" "$link.ai-envelope"
mv -Tf "$link.ai-envelope" "$link"
if ! systemctl restart ecspros.service || ! curl -fsS --max-time 5 --retry 10 --retry-delay 2 --retry-all-errors "$url/ready" >/dev/null; then
 ln -s "$previous" "$link.ai-envelope-rollback"
 mv -Tf "$link.ai-envelope-rollback" "$link"
 systemctl restart ecspros.service
 exit 12
fi
systemctl is-active ecspros.service
curl -fsS --max-time 10 "$url/ready"
rm -- /tmp/ecspros-ai-envelope-20260911.dll
'@
    Write-Output "Activating $name envelope fix"
    Remote $node ($script.Replace('__HASH__',$hash))
}
