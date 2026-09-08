param([Parameter(Mandatory)][string]$Assembly,[ValidateSet('Read','Rehearse','Apply')][string]$Action='Read')
$ErrorActionPreference='Stop'
$cfg=Get-Content -Raw (Join-Path $PSScriptRoot '../../../appsettingsTest.json')|ConvertFrom-Json
$node=$cfg.Infrastructure.SSH.Api1
if($node.Host -ne '5.39.57.245'){throw 'Unexpected jump host'}
$ports=[Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port
if(11433 -in $ports -or 15432 -in $ports){throw 'Tunnel port in use; existing processes untouched'}
$argsList=@('-N','-o','BatchMode=yes','-o','StrictHostKeyChecking=yes','-o','ExitOnForwardFailure=yes','-o','ConnectTimeout=15','-i',$node.PrivateKeyPath,'-p',[string]$node.Port,'-L','127.0.0.1:11433:135.125.172.93:1433','-L','127.0.0.1:15432:192.168.0.241:5432',"$($node.Username)@$($node.Host)")
$quoted=($argsList|ForEach-Object {'"'+($_ -replace '"','\"')+'"'}) -join ' '
$process=Start-Process -FilePath C:/Windows/System32/OpenSSH/ssh.exe -ArgumentList $quoted -WindowStyle Hidden -PassThru
try {
 $ready=$false
 for($i=0;$i -lt 30;$i++) {
  if($process.HasExited){throw 'Tunnel failed'}
  $ports=[Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port
  if(11433 -in $ports -and 15432 -in $ports){$ready=$true;break}
  Start-Sleep -Milliseconds 200
 }
 if(-not $ready){throw 'Tunnel not ready'}
 @{v3=[string]$cfg.ConnectionStrings.ErpSource;pg=[string]$cfg.ConnectionStrings.DefaultConnection}|ConvertTo-Json -Compress|& dotnet $Assembly $Action
 if($LASTEXITCODE){throw 'Read-only report failed'}
} finally {if(-not $process.HasExited){Stop-Process -Id $process.Id -Force};$process.Dispose()}
