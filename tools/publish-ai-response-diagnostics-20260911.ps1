param([switch]$ClarificationFix, [switch]$PredicateDiagnostics, [switch]$PredicateShape, [switch]$PredicateReason, [switch]$WindowAbsence, [switch]$PredicateDates)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cfg = Get-Content -Raw -LiteralPath (Join-Path $root 'appsettingsTest.json') | ConvertFrom-Json
$dll = Join-Path $root 'output/publish/ai-response-diagnostics-20260911/ECSPros.Api.dll'
if ($ClarificationFix) { $dll = Join-Path $root 'output/publish/ai-clarification-fix-20260911/ECSPros.Api.dll' }
if ($PredicateDiagnostics) { $dll = Join-Path $root 'output/publish/ai-predicate-diagnostics-20260911/ECSPros.Api.dll' }
if ($PredicateShape) { $dll = Join-Path $root 'output/publish/ai-predicate-shape-20260911/ECSPros.Api.dll' }
if ($PredicateReason) { $dll = Join-Path $root 'output/publish/ai-predicate-reason-20260911/ECSPros.Api.dll' }
if ($WindowAbsence) { $dll = Join-Path $root 'output/publish/ai-window-absence-20260911/ECSPros.Api.dll' }
if ($PredicateDates) { $dll = Join-Path $root 'output/publish/ai-predicate-dates-20260911/ECSPros.Api.dll' }
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
    $remoteFile = if ($ClarificationFix) { '/tmp/ecspros-ai-clarification-20260911.dll' } else { '/tmp/ecspros-ai-diagnostics-20260911.dll' }
    if ($PredicateDiagnostics) { $remoteFile = '/tmp/ecspros-ai-predicate-diagnostics-20260911.dll' }
    if ($PredicateShape) { $remoteFile = '/tmp/ecspros-ai-predicate-shape-20260911.dll' }
    if ($PredicateReason) { $remoteFile = '/tmp/ecspros-ai-predicate-reason-20260911.dll' }
    if ($WindowAbsence) { $remoteFile = '/tmp/ecspros-ai-window-absence-20260911.dll' }
    if ($PredicateDates) { $remoteFile = '/tmp/ecspros-ai-predicate-dates-20260911.dll' }
    Remote $node "test ! -e '$remoteFile'"
    & $scp -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=15 -i $node.PrivateKeyPath -P $node.Port $dll "$($node.Username)@$($node.Host):$remoteFile"
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed' }
    $script = @'
set -eu
link=/opt/ECSProsAI/current
previous=/opt/ECSProsAI/releases/20260911_ai_binding_replacement
target=/opt/ECSProsAI/releases/20260911_ai_response_diagnostics
test -L "$link"
test "$(readlink -f "$link")" = "$previous"
test ! -e "$target"
printf '%s  %s\n' '41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be' "$previous/ECSPros.Api.dll" | sha256sum -c -
printf '%s  %s\n' '__HASH__' '/tmp/ecspros-ai-diagnostics-20260911.dll' | sha256sum -c -
pid=$(systemctl show ecspros.service -p MainPID --value)
tr '\0' '\n' < /proc/$pid/environ | grep -qx 'Node__MigrateOnStartup=false'
url=$(tr '\0' '\n' < /proc/$pid/environ | sed -n 's/^ASPNETCORE_URLS=//p')
case "$url" in http://192.168.0.245:5050|http://192.168.0.58:5050) ;; *) exit 11;; esac
curl -fsS --max-time 10 "$url/ready" >/dev/null
mkdir "$target"
cp -a "$previous/." "$target/"
cp /tmp/ecspros-ai-diagnostics-20260911.dll "$target/ECSPros.Api.dll"
printf '%s  %s\n' '__HASH__' "$target/ECSPros.Api.dll" | sha256sum -c -
ln -s "$target" "$link.ai-diagnostics"
mv -Tf "$link.ai-diagnostics" "$link"
if ! systemctl restart ecspros.service || ! curl -fsS --max-time 5 --retry 10 --retry-delay 2 --retry-all-errors "$url/ready" >/dev/null; then
 ln -s "$previous" "$link.ai-diagnostics-rollback"
 mv -Tf "$link.ai-diagnostics-rollback" "$link"
 systemctl restart ecspros.service
 exit 12
fi
systemctl is-active ecspros.service
curl -fsS --max-time 10 "$url/ready"
rm -- /tmp/ecspros-ai-diagnostics-20260911.dll
'@
    Write-Output "Activating $name diagnostics"
    if ($ClarificationFix) {
        $script = $script.Replace('20260911_ai_response_diagnostics','20260911_ai_clarification_fix').Replace('20260911_ai_binding_replacement','20260911_ai_response_diagnostics').Replace('41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be','445664a59245a3ab3c436d2a09f6b50bb895a20edcf03e286e4cbcbc9f3c1c7d').Replace('/tmp/ecspros-ai-diagnostics-20260911.dll',$remoteFile)
    }
    if ($PredicateDiagnostics) {
        $script = $script.Replace('20260911_ai_response_diagnostics','20260911_ai_predicate_diagnostics').Replace('20260911_ai_binding_replacement','20260911_ai_card_window').Replace('41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be','6283d16782993eab37d3db351a75b62d4c4a2c007976fd8237bc90fde596b6c4').Replace('/tmp/ecspros-ai-diagnostics-20260911.dll',$remoteFile)
    }
    if ($PredicateShape) {
        $script = $script.Replace('20260911_ai_response_diagnostics','20260911_ai_predicate_shape').Replace('20260911_ai_binding_replacement','20260911_ai_predicate_diagnostics').Replace('41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be','e3a0edd7cd07e51ff1b15d81f5231a0c7e4f6cbb444227b8b971ed9efa843d97').Replace('/tmp/ecspros-ai-diagnostics-20260911.dll',$remoteFile)
    }
    if ($PredicateReason) {
        $script = $script.Replace('20260911_ai_response_diagnostics','20260911_ai_predicate_reason').Replace('20260911_ai_binding_replacement','20260911_ai_predicate_shape').Replace('41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be','b1975141bb6649e681a92da37f6b6291a2e1ed09dac44f0e2ec5f0ba65d137fc').Replace('/tmp/ecspros-ai-diagnostics-20260911.dll',$remoteFile)
    }
    if ($WindowAbsence) {
        $script = $script.Replace('20260911_ai_response_diagnostics','20260911_ai_window_absence').Replace('20260911_ai_binding_replacement','20260911_ai_predicate_reason').Replace('41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be','e0721cb067b3ecfd84049b16c7702696f12549795c0e9ba18a690c4133072db9').Replace('/tmp/ecspros-ai-diagnostics-20260911.dll',$remoteFile)
    }
    if ($PredicateDates) {
        $script = $script.Replace('20260911_ai_response_diagnostics','20260911_ai_predicate_dates').Replace('20260911_ai_binding_replacement','20260911_ai_window_absence').Replace('41a3eaa2696385e17d783bf280a8ba30bb190356e7749aab39c2672bc737a2be','0f8c20fb802d3277e84bccbf593316008385d690c747ff700c88c189ecdddd4c').Replace('/tmp/ecspros-ai-diagnostics-20260911.dll',$remoteFile)
    }
    Remote $node ($script.Replace('__HASH__',$hash))
}
