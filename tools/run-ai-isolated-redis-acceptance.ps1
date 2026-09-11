$ErrorActionPreference = 'Stop'
$cfg = Get-Content -Raw (Join-Path $PSScriptRoot '../appsettingsTest.json') | ConvertFrom-Json
$node = $cfg.Infrastructure.SSH.Redis1
if ($node.Host -ne '5.39.57.243') { throw 'Unexpected target' }
$ssh = 'C:/Windows/System32/OpenSSH/ssh.exe'
$argsBase = @('-o','BatchMode=yes','-o','StrictHostKeyChecking=yes','-o','ConnectTimeout=10','-i',$node.PrivateKeyPath,'-p',[string]$node.Port)
$destination = "$($node.Username)@$($node.Host)"
$tag = 'ecspros-quota-' + [Guid]::NewGuid().ToString('N')
$previous = $env:ECSPROS_ACCEPTANCE_AI_QUOTA_DISPOSABLE_REDIS
$tunnel = $null
$started = $false
try {
    if (16379 -in [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port) { throw 'Local port occupied' }
    & $ssh @argsBase $destination "test -z `"`$(ss -ltnH 'sport = :16379')`" && redis-server --bind 127.0.0.1 --port 16379 --save '' --appendonly no --daemonize yes --pidfile /tmp/$tag.pid --logfile /dev/null"
    if ($LASTEXITCODE -ne 0) { throw 'Isolated Redis start failed' }
    $started = $true
    $forward = @($argsBase) + @('-N','-T','-o','ExitOnForwardFailure=yes','-L','127.0.0.1:16379:127.0.0.1:16379',$destination)
    $quoted = ($forward | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $tunnel = Start-Process $ssh -ArgumentList $quoted -WindowStyle Hidden -PassThru
    for ($i=0; $i -lt 50; $i++) {
        if ($tunnel.HasExited) { throw 'Tunnel exited' }
        if (16379 -in [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port) { break }
        Start-Sleep -Milliseconds 200
    }
    $env:ECSPROS_ACCEPTANCE_AI_QUOTA_DISPOSABLE_REDIS = '1'
    dotnet test (Join-Path $PSScriptRoot '../tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj') --no-build --no-restore --filter 'FullyQualifiedName~AiQuotaRedisAcceptanceTests' --verbosity minimal
    $result = $LASTEXITCODE
} finally {
    $env:ECSPROS_ACCEPTANCE_AI_QUOTA_DISPOSABLE_REDIS = $previous
    if ($null -ne $tunnel -and -not $tunnel.HasExited) { Stop-Process -InputObject $tunnel -Force }
    if ($started) {
        & $ssh @argsBase $destination "test -f /tmp/$tag.pid && redis-cli -p 16379 shutdown nosave"
        if ($LASTEXITCODE -ne 0) { throw 'Isolated Redis cleanup needs inspection' }
    }
}
exit $result
