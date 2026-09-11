param([ValidateSet('Inspect','Rehearse','Apply')][string]$Action='Inspect')
$ErrorActionPreference='Stop'
# Reuse the pinned, read-only reference connection; no source writes or snapshot files.
$base=Get-Content -Raw (Join-Path $PSScriptRoot 'compare-reference-definitions.ps1')
$prefix=$base.Substring(0,$base.IndexOf('$n=$cfg.Infrastructure.SSH.Api1'))
$sourceSql=Get-Content -Raw (Join-Path $PSScriptRoot 'sync-reference-iam-snapshot.sql')
$prefix=[regex]::Replace($prefix,'(?s)sql=""".*?"""',('sql="""'+$sourceSql+'"""'))
$prefix=$prefix.Replace("param([ValidateSet('Inspect','Rehearse','Apply')][string]`$Action='Inspect')",'')
$configPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../appsettingsTest.json')).Replace("'","''")
$prefix=$prefix.Replace("(Join-Path `$PSScriptRoot '../../appsettingsTest.json')",("'"+$configPath+"'"))
Invoke-Expression $prefix
$n=$cfg.Infrastructure.SSH.Api1
$b=[System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString([string]$cfg.ConnectionStrings.DefaultConnection)
if($n.Host -ne '5.39.57.245' -or $b.get_Item('Host') -ne '192.168.0.241' -or $b.get_Item('Database') -ne 'ecommerce_db'){throw 'Unexpected target'}
$payload=@{snapshot=$snapshot;query=$sourceSql;action=$Action;user=$b.get_Item('Username');password=$b.get_Item('Password')}|ConvertTo-Json -Compress
$py=Get-Content -Raw (Join-Path $PSScriptRoot 'sync-reference-iam.py')
$enc=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
$payload | & 'C:/Windows/System32/OpenSSH/ssh.exe' -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=10 -p $n.Port -i $n.PrivateKeyPath ($n.Username+'@'+$n.Host) ('python3 -c ''import base64;exec(base64.b64decode("{0}"))''' -f $enc)
if($LASTEXITCODE -ne 0){throw 'IAM operation failed; no credentials logged'}
$snapshot=$null; $payload=$null
