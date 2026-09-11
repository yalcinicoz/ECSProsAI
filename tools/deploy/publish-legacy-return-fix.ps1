$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cfg=Get-Content -Raw (Join-Path $root 'appsettingsTest.json') | ConvertFrom-Json
$n=$cfg.Infrastructure.SSH.Api1
if($n.Host -ne '5.39.57.245'){throw 'Unexpected host'}
$ssh='C:/Windows/System32/OpenSSH/ssh.exe'
$scp='C:/Windows/System32/OpenSSH/scp.exe'
$release='20260911_return_fix_6ae426a8'
$dir=Join-Path $root 'output/publish/ai-final-6ae426a8'
$archive=Join-Path $root "output/$release.tar.gz"
if(Test-Path $archive){throw 'Archive already exists'}
if(-not(Test-Path (Join-Path $dir 'ECSPros.Api.dll'))){throw 'Publish missing'}
& tar -czf $archive --exclude='appsettings*.json' -C $dir .
if($LASTEXITCODE -ne 0){throw 'Packaging failed'}
$hash=(Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$dll=(Get-FileHash (Join-Path $dir 'ECSPros.Api.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
& $scp -o BatchMode=yes -o StrictHostKeyChecking=yes -i $n.PrivateKeyPath -P $n.Port $archive "$($n.Username)@$($n.Host):/tmp/$release.tar.gz"
if($LASTEXITCODE -ne 0){throw 'Upload failed'}
$script=@'
set -eu
link=/opt/ECSProsAI/worker-current
target=/opt/ECSProsAI/worker-releases/20260911_return_fix_6ae426a8
archive=/tmp/20260911_return_fix_6ae426a8.tar.gz
test -L "$link"
previous=$(readlink -f "$link")
case "$previous" in /opt/ECSProsAI/worker-releases/*) ;; *) exit 10;; esac
test ! -e "$target"
pid=$(systemctl show ecspros-legacy-import.service -p MainPID --value)
tr '\0' '\n' < /proc/$pid/environ | grep -qx 'Node__MigrateOnStartup=false'
tr '\0' '\n' < /proc/$pid/environ | grep -qx 'Node__WorkerProfile=LegacyImport'
curl -fsS --max-time 10 http://127.0.0.1:5060/ready >/dev/null
printf '%s  %s\n' '__HASH__' "$archive" | sha256sum -c -
mkdir "$target"
tar -xzf "$archive" -C "$target"
printf '%s  %s\n' '__DLL__' "$target/ECSPros.Api.dll" | sha256sum -c -
cp -pL "$previous"/appsettings*.json "$target/"
ln -s "$target" "$link.return-fix"
mv -Tf "$link.return-fix" "$link"
if ! systemctl restart ecspros-legacy-import.service || ! curl -fsS --retry 10 --retry-delay 2 --retry-all-errors --max-time 5 http://127.0.0.1:5060/ready >/dev/null; then
 ln -s "$previous" "$link.return-rollback"
 mv -Tf "$link.return-rollback" "$link"
 systemctl restart ecspros-legacy-import.service
 exit 12
fi
systemctl is-active ecspros-legacy-import.service
curl -fsS --max-time 10 http://127.0.0.1:5060/ready
rm -- "$archive"
'@
$script=$script.Replace('__HASH__',$hash).Replace('__DLL__',$dll)
& $ssh -o BatchMode=yes -o StrictHostKeyChecking=yes -i $n.PrivateKeyPath -p $n.Port "$($n.Username)@$($n.Host)" $script
if($LASTEXITCODE -ne 0){throw 'Activation failed; inspect before retry'}
if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($archive)) -ne (Join-Path $root 'output')){throw 'Cleanup guard'}
Remove-Item -LiteralPath $archive
