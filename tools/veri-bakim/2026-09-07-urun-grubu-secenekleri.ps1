param([ValidateSet('Inspect','Rehearse','Apply')][string]$Action='Inspect')
$ErrorActionPreference='Stop'
$cfg=Get-Content -Raw (Join-Path $PSScriptRoot '../../appsettingsTest.json') | ConvertFrom-Json
$n=$cfg.Infrastructure.SSH.Api1
$b=[System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString([string]$cfg.ConnectionStrings.DefaultConnection)
if($n.Host -ne '5.39.57.245' -or $b.get_Item('Host') -ne '192.168.0.241' -or $b.get_Item('Database') -ne 'ecommerce_db'){throw 'Unexpected target'}
$sql=Get-Content -Raw (Join-Path $PSScriptRoot '2026-09-07-urun-grubu-secenekleri.sql')
if($Action -eq 'Inspect'){$sql="BEGIN READ ONLY;"+$sql.Substring($sql.IndexOf('-- REPORT'))+" ROLLBACK;"}
else{$sql+=if($Action -eq 'Apply'){" COMMIT;"}else{" ROLLBACK;"}}
$payload=@{user=$b.get_Item('Username');password=$b.get_Item('Password');sql=$sql;readOnly=($Action -eq 'Inspect')}|ConvertTo-Json -Compress
$py=@'
import sys,json,os,subprocess
c=json.load(sys.stdin);env=os.environ.copy()
env.update(PGPASSWORD=c['password'],PGOPTIONS='-c statement_timeout=30000 -c default_transaction_read_only='+('on' if c['readOnly'] else 'off'),PGCONNECT_TIMEOUT='5')
r=subprocess.run(['psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1','-h','192.168.0.241','-U',c['user'],'-d','ecommerce_db'],input=c['sql'],text=True,env=env)
sys.exit(r.returncode)
'@
$encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
$remote='python3 -c ''import base64;exec(base64.b64decode("{0}"))''' -f $encoded
try {
 $payload | & "$env:WINDIR/System32/OpenSSH/ssh.exe" -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=10 -p ([string]$n.Port) -i ([string]$n.PrivateKeyPath) ($n.Username+'@'+$n.Host) $remote
 if($LASTEXITCODE -ne 0){throw 'Operation failed; transaction not committed'}
} finally {$payload=$null;$cfg=$null;$b=$null}
