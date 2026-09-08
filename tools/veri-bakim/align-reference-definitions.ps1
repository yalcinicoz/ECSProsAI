param([ValidateSet('Rehearse','Apply')][string]$Action='Rehearse')
$ErrorActionPreference='Stop'
# .59 is strictly read-only; requires loopback tunnel 12259 via API01.
$pkg='C:/Users/garku/.nuget/packages'
Add-Type -Path "$pkg/bouncycastle.cryptography/2.7.0/lib/net6.0/BouncyCastle.Cryptography.dll"
Add-Type -Path "$pkg/microsoft.extensions.logging.abstractions/8.0.3/lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll"
$dll="$pkg/ssh.net/2026.0.0/lib/net10.0/Renci.SshNet.dll"
Add-Type -Path $dll
if(-not ('DefinitionReadHostPin' -as [type])){
Add-Type -ReferencedAssemblies $dll -TypeDefinition @'
using System;
using Renci.SshNet;
public static class DefinitionReadHostPin {
 public static void Attach(SshClient c,string[] keys) {
  c.HostKeyReceived+=(s,e)=>{e.CanTrust=Array.IndexOf(keys,Convert.ToBase64String(e.HostKey))>=0;};
 }
}
'@
}
$cfg=Get-Content -Raw (Join-Path $PSScriptRoot '../../appsettingsTest.json')|ConvertFrom-Json
$legacy=$cfg.Infrastructure.SSH.LegacyProduction
if($legacy.Host -ne '51.178.208.59'){throw 'Unexpected reference'}
$known=@(Get-Content C:/Users/garku/.ssh/known_hosts|Where-Object {$_ -match '^51\.178\.208\.59\s'}|ForEach-Object {($_ -split '\s+')[2]})
if(-not $known.Count){throw 'Missing host pin'}
$client=[Renci.SshNet.SshClient]::new('127.0.0.1',12259,[string]$legacy.Username,[string]$legacy.Password)
[DefinitionReadHostPin]::Attach($client,[string[]]$known)
$sourcePython=@'
import json,os,subprocess,pathlib,re,shlex
config={}
for filename in ('appsettings.json','appsettings.Production.json'):
 p=pathlib.Path('/opt/ECSProsAI/publish')/filename
 if p.exists():config.update(json.loads(p.read_text(encoding='utf-8-sig')).get('ConnectionStrings',{}))
envlines=subprocess.check_output(['systemctl','show','ecspros.service','-p','Environment','--value'],text=True)
overrides=dict(s.split('=',1) for s in shlex.split(envlines) if '=' in s)
connection=overrides.get('ConnectionStrings__DefaultConnection',config.get('DefaultConnection',''))
parts={m.group(1).strip().lower():m.group(2).strip().strip('"').replace('""','"') for m in re.finditer(r'(?:^|;)\s*([^=;]+)=("(?:[^"]|"")*"|[^;]*)',connection)}
host=parts.get('host','localhost')
if host not in ('localhost','127.0.0.1','192.168.0.59','51.178.208.59'):raise RuntimeError('Reference host rejected')
env=os.environ.copy();env.update(PGPASSWORD=parts.get('password',''),PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=20000',PGCONNECT_TIMEOUT='5')
sql="""SELECT json_build_object(
'identity',(SELECT current_database()='ecommerce_db' AND inet_server_addr()='172.18.0.3'::inet AND current_setting('transaction_read_only')='on'),
'dictionary',(SELECT coalesce(json_agg(d),'[]') FROM integration.erp_reference_items d WHERE d."TargetSystem"='erp:nebim' AND d."Kind"='product_group' AND NOT d."IsDeleted" AND d."IsActive"),
'mappings',(SELECT coalesce(json_agg(m),'[]') FROM integration.marketplace_category_mappings m WHERE m."Marketplace"='erp:nebim' AND NOT m."IsDeleted" AND m."Status"='active'),
'groups',(SELECT coalesce(json_agg(g),'[]') FROM definition.product_groups g WHERE NOT g."IsDeleted" AND g."IsActive"),
'types',(SELECT coalesce(json_agg(t),'[]') FROM definition.attribute_types t WHERE NOT t."IsDeleted" AND t."IsActive"),
'values',(SELECT coalesce(json_agg(v),'[]') FROM definition.attribute_values v JOIN definition.attribute_types t ON t."Id"=v."AttributeTypeId" WHERE NOT v."IsDeleted" AND v."IsActive" AND NOT t."IsDeleted" AND t."IsActive"),
'links',(SELECT coalesce(json_agg(a),'[]') FROM definition.product_group_attributes a WHERE NOT a."IsDeleted"),
'axes',(SELECT coalesce(json_agg(a),'[]') FROM definition.product_group_axis_sub_attributes a WHERE NOT a."IsDeleted"));"""
r=subprocess.run(['psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1','-h',host,'-p',parts.get('port','5432'),'-U',parts.get('username',parts.get('user id','')),'-d',parts.get('database','')],input=sql,text=True,env=env,capture_output=True)
if r.returncode:raise RuntimeError(r.stderr)
print(r.stdout)
'@
try {
 $client.Connect()
 $enc=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sourcePython))
 $cmd=$client.CreateCommand(('python3 -c ''import base64;exec(base64.b64decode("{0}"))''' -f $enc))
 $cmd.CommandTimeout=[TimeSpan]::FromSeconds(40)
 $snapshot=$cmd.Execute()
 if($cmd.ExitStatus -ne 0){throw ('Reference read failed: '+$cmd.Error)}
 $reference=$snapshot|ConvertFrom-Json
 if(-not $reference.identity){throw 'Reference DB identity rejected'}
} finally {$client.Dispose()}
$n=$cfg.Infrastructure.SSH.Api1
$b=[System.Data.Common.DbConnectionStringBuilder]::new();$b.set_ConnectionString([string]$cfg.ConnectionStrings.DefaultConnection)
if($n.Host -ne '5.39.57.245' -or $b.get_Item('Host') -ne '192.168.0.241' -or $b.get_Item('Database') -ne 'ecommerce_db'){throw 'Unexpected target'}
$sql=Get-Content -Raw (Join-Path $PSScriptRoot 'align-reference-definitions.sql')

$payload=@{snapshot=$snapshot;sql=$sql;action=$Action;user=$b.get_Item('Username');password=$b.get_Item('Password')}|ConvertTo-Json -Compress
$targetPython=@'
import sys,json,subprocess,os,pathlib,datetime
c=json.load(sys.stdin);env=os.environ.copy()
env.update(PGPASSWORD=c['password'],PGOPTIONS='-c statement_timeout=30000 -c lock_timeout=5000'+(' -c default_transaction_read_only=on' if c['action']=='Inspect' else ''))
if c['action']=='Apply':
 check=subprocess.run(['psql','-X','-q','-t','-A','-h','192.168.0.241','-U',c['user'],'-d','ecommerce_db','-c',"SELECT current_database()='ecommerce_db' AND inet_server_addr()='192.168.0.241'::inet"],env=env,capture_output=True,text=True,check=True)
 if check.stdout.strip()!='t':raise RuntimeError('Backup target identity rejected')
 folder=pathlib.Path('/opt/ECSProsAI/backups')
 folder.mkdir(exist_ok=True)
 backup=folder/('reference-definitions-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.dump')
 args=['pg_dump','-h','192.168.0.241','-U',c['user'],'-d','ecommerce_db','-Fc','--no-owner','--no-privileges']
 for table in ['definition.product_groups','definition.attribute_types','definition.attribute_values','definition.product_group_attributes','definition.product_group_axis_sub_attributes','integration.erp_reference_items','integration.marketplace_category_mappings']:
  args+=['-t',table]
 with backup.open('xb') as f:
  os.chmod(backup,0o600)
  subprocess.run(args,env=env,stdout=f,check=True)
 subprocess.run(['pg_restore','--list',str(backup)],stdout=subprocess.DEVNULL,check=True)
 print('RECOVERY_BACKUP',str(backup),flush=True)
literal="'"+c['snapshot'].replace("'","''")+"'::jsonb"
sql=c['sql'].replace('__SNAPSHOT__',literal)
if c['action']!='Inspect':sql+='\n'+('COMMIT;' if c['action']=='Apply' else 'ROLLBACK;')
r=subprocess.run(['psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1','-h','192.168.0.241','-U',c['user'],'-d','ecommerce_db'],input=sql,text=True,env=env)
sys.exit(r.returncode)
'@
$enc=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($targetPython))
$payload | & "$env:WINDIR/System32/OpenSSH/ssh.exe" -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=10 -p ([string]$n.Port) -i ([string]$n.PrivateKeyPath) ($n.Username+'@'+$n.Host) ('python3 -c ''import base64;exec(base64.b64decode("{0}"))''' -f $enc)
if($LASTEXITCODE -ne 0){throw 'Target operation failed'}
