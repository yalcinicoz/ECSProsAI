param()
$ErrorActionPreference = 'Stop'
$aiTunnelProcess = $null
$aiPreviousConnection = $env:ECSPROS_ACCEPTANCE_AI_STOCK_READ
$aiExitCode = 1
$aiStage = 'configuration'
try {
    $aiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $aiCfg = Get-Content -Raw -LiteralPath (Join-Path $aiRoot 'appsettingsTest.json') | ConvertFrom-Json
    $aiNode = $aiCfg.Infrastructure.SSH.Api1
    if ($aiNode.Host -ne '5.39.57.245' -or [string]::IsNullOrWhiteSpace($aiNode.PrivateKeyPath)) { throw 'Unexpected SSH configuration' }
    if (15432 -in [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port) { throw 'Existing listener preserved' }
    $aiConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
    # PowerShell dictionary adaptation shadows property assignment; use the actual setter/getter.
    $aiConnection.set_ConnectionString([string]$aiCfg.ConnectionStrings.DefaultConnection)
    if ([string]$aiConnection['Host'] -ne '192.168.0.241' -or [string]$aiConnection['Database'] -ne 'ecommerce_db') { throw 'Unexpected database target' }
    $aiConnection['Host'] = '127.0.0.1'
    $aiConnection['Port'] = '15432'
    $aiConnection['Options'] = '-c default_transaction_read_only=on -c statement_timeout=15000'
    $aiArgs = @('-N', '-T', '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', 'ExitOnForwardFailure=yes',
        '-o', 'ConnectTimeout=15', '-i', [string]$aiNode.PrivateKeyPath, '-p', [string]$aiNode.Port,
        '-L', '127.0.0.1:15432:192.168.0.241:5432', "$($aiNode.Username)@$($aiNode.Host)")
    $aiQuoted = ($aiArgs | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join ' '
    $aiStage = 'SSH transport'
    # Let ssh read its key; never read, convert, print or change the key here.
    $aiTunnelProcess = Start-Process -FilePath 'C:\Windows\System32\OpenSSH\ssh.exe' -ArgumentList $aiQuoted -WindowStyle Hidden -PassThru
    $aiReady = $false
    for ($aiAttempt = 0; $aiAttempt -lt 100; $aiAttempt++) {
        if ($aiTunnelProcess.HasExited) { throw 'SSH failed' }
        if (15432 -in [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port) { $aiReady = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $aiReady) { throw 'Tunnel not ready' }
    $env:ECSPROS_ACCEPTANCE_AI_STOCK_READ = $aiConnection.get_ConnectionString()
    $aiStage = 'guarded read-only acceptance'
    & dotnet test (Join-Path $aiRoot 'tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj') --no-build --no-restore --filter 'FullyQualifiedName~AiStockReadAcceptanceTests' --verbosity minimal
    $aiExitCode = $LASTEXITCODE
} catch {
    Write-Output ('Stopped at ' + $aiStage + ': ' + $_.Exception.GetType().Name)
} finally {
    $env:ECSPROS_ACCEPTANCE_AI_STOCK_READ = $aiPreviousConnection
    if ($null -ne $aiTunnelProcess) {
        if (-not $aiTunnelProcess.HasExited) { Stop-Process -InputObject $aiTunnelProcess -Force }
        $aiTunnelProcess.Dispose()
    }
    Write-Output 'Task-owned tunnel closed; process environment restored.'
}
exit $aiExitCode
