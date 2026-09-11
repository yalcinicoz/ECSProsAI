"""Explicitly authorized .59 -> multi-test IAM sync; payload stays in memory.

Inspect is read-only. Rehearse rolls back. Apply uses an optimistic snapshot
guard under table locks. Existing passwords/superadmin flags and target-only
users are preserved. Sessions, preferences and business records never copied.
"""
import sys, json, os, subprocess, uuid, hashlib, datetime
sys.tracebacklimit=0

c = json.load(sys.stdin)
env = os.environ.copy()
env.update(PGPASSWORD=c['password'], PGCONNECT_TIMEOUT='5',
           PGOPTIONS='-c statement_timeout=30000 -c lock_timeout=5000')
command = ['psql','-X','-q','-t','-A','-v','ON_ERROR_STOP=1',
           '-h','192.168.0.241','-U',c['user'],'-d','ecommerce_db']
def run(sql, readonly=False):
    e=env.copy()
    if readonly: e['PGOPTIONS'] += ' -c default_transaction_read_only=on'
    r=subprocess.run(command,input=sql,text=True,env=e,capture_output=True)
    if r.returncode:
        # Never echo postgres DETAIL/query, which may contain password hashes.
        print('Database operation failed (details suppressed to protect credentials).',file=sys.stderr)
        sys.exit(2)
    return r.stdout.strip()

source=json.loads(c['snapshot'])
target_text=run(c['query'],True)
target=json.loads(target_text)
tables={'roles':'iam_roles','permissions':'iam_permissions','users':'iam_users',
        'userRoles':'iam_user_roles','rolePermissions':'iam_role_permissions','userPermissions':'iam_user_permissions'}
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
active=lambda rows:[x for x in rows if not x.get('IsDeleted',False)]
maps={}; expected={}; counts={}; statements=[]
def fail(message):
    raise RuntimeError(message)
def literal(value):
    return "'"+json.dumps(value,ensure_ascii=False).replace("'","''")+"'::json"
def quoted(name):
    assert name.replace('_','').isalnum()
    return '"'+name+'"'
def emit(key,row,existing=None):
    fields=list(row)
    table='iam.'+tables[key]
    record='json_populate_record(NULL::'+table+','+literal(row)+')'
    if existing is None:
        statements.append('INSERT INTO '+table+' ('+','.join(map(quoted,fields))+') SELECT '+','.join(map(quoted,fields))+' FROM '+record+';')
        counts[key]['insert']+=1
    elif any(existing.get(k)!=v for k,v in row.items() if k not in ('UpdatedAt','UpdatedBy')):
        fields=[k for k in fields if k!='Id']
        statements.append('UPDATE '+table+' t SET '+','.join(quoted(k)+'=s.'+quoted(k) for k in fields)+' FROM '+record+' s WHERE t."Id"=s."Id";')
        counts[key]['update']+=1
    else:
        row['UpdatedAt']=existing.get('UpdatedAt')
        row['UpdatedBy']=existing.get('UpdatedBy')

# Reference ownership is matched by immutable business code, never guessed.
for key in ('firms','channels'):
    maps[key]={}
    for s in source[key]:
        candidates=[t for t in target[key] if t['Code']==s['Code']]
        if len(candidates)==1:
            t=candidates[0]
            if key=='channels' and maps['firms'].get(s['FirmId'])!=t['FirmId']: fail('Channel firm mismatch')
            maps[key][s['Id']]=t['Id']
def remap(key,value):
    if value is None:return None
    if value not in maps[key]:fail('Unmapped '+key+' reference: '+value)
    return maps[key][value]

for key in ('roles','permissions','users'):
    maps[key]={}; expected[key]=[]; counts[key]={'insert':0,'update':0,'deactivate':0}
    for s in active(source[key]):
        if key=='users':
            matches=[t for t in target[key] if t['Id']==s['Id'] or t['Username'].casefold()==s['Username'].casefold() or t['Email'].casefold()==s['Email'].casefold()]
        else: matches=[t for t in target[key] if t['Id']==s['Id'] or t['Code']==s['Code']]
        if len(matches)>1:fail('Ambiguous '+key+' identity')
        old=matches[0] if matches else None
        if old and old['IsDeleted']:fail('Soft-deleted identity requires manual review')
        if old and key!='users' and old['Code']!=s['Code']:fail('Conflicting code identity')
        row=dict(s); row['Id']=old['Id'] if old else s['Id']
        if row['Id'] in maps[key].values():fail('Multiple source identities match one target')
        maps[key][s['Id']]=row['Id']
        if key=='users':
            row['FirmId']=remap('firms',s.get('FirmId'))
            if old:
                for field in ('PasswordHash','IsSuperAdmin','PasswordChangedAt','MustChangePassword','Username','Email'):
                    row[field]=old[field]
            elif row['IsActive'] and not row['PasswordHash'].startswith(('$2a$','$2b$','$2y$')):
                fail('New active user has incompatible password hash')
        # Source audit IDs are not silently assigned to unrelated target users.
        for field in ('CreatedBy','UpdatedBy','DeletedBy'):row[field]=None
        if old:
            row['CreatedAt']=old['CreatedAt']; row['CreatedBy']=old['CreatedBy']
        row['UpdatedAt']=now
        emit(key,row,old); expected[key].append(row)

for key,foreign in (('rolePermissions',('RoleId','roles','PermissionId','permissions')),
                    ('userRoles',('UserId','users','RoleId','roles')),
                    ('userPermissions',('UserId','users','PermissionId','permissions'))):
    counts[key]={'insert':0,'update':0,'deactivate':0}; expected[key]=[]
    for s in active(source[key]):
        row=dict(s)
        for field,mapping in zip(foreign[::2],foreign[1::2]):row[field]=remap(mapping,s[field])
        if 'FirmId' in row:row['FirmId']=remap('firms',row['FirmId'])
        if row.get('ChannelIds') is not None:
            row['ChannelIds']=[remap('channels',v) for v in row['ChannelIds']]
        keys=list(foreign[::2])+(['FirmId'] if key=='userRoles' else [])
        matches=[t for t in target[key] if all(t[k]==row[k] for k in keys)]
        if len(matches)>1:
            live=active(matches)
            if len(live)>1:fail('Duplicate active relation in '+key)
            matches=live or sorted(matches,key=lambda x:x['Id'])[:1]
        old=matches[0] if matches else None
        row['Id']=old['Id'] if old else str(uuid.uuid4())
        for field in ('CreatedBy','UpdatedBy','DeletedBy'):row[field]=None
        row['DeletedAt']=None;row['UpdatedAt']=now
        if old:row['CreatedAt']=old['CreatedAt'];row['CreatedBy']=old['CreatedBy']
        emit(key,row,old); expected[key].append(row)
    # Align only source-managed principals; target-only test users are untouched.
    principal=foreign[0];managed=set(maps[foreign[1]].values())
    kept={r['Id'] for r in expected[key]}
    for old in active(target[key]):
        if old[principal] in managed and old['Id'] not in kept:
            statements.append('UPDATE iam.'+tables[key]+' SET "IsDeleted"=true,"DeletedAt"=now(),"UpdatedAt"=now() WHERE "Id"=\''+old['Id']+'\';')
            counts[key]['deactivate']+=1

print(json.dumps({'action':c['action'],'changes':counts,'sourceCounts':{k:len(active(source[k])) for k in tables}},ensure_ascii=False))
if c['action']=='Inspect':sys.exit(0)
# Snapshot excludes volatile login times, sessions and preferences. Fail closed on
# concurrent IAM edits rather than overwriting another administrator's changes.
snapshot_hash=hashlib.md5(target_text.encode()).hexdigest()
query=c['query'].strip().rstrip(';')
guard="DO $guard$ BEGIN IF (SELECT md5(v::text) FROM ("+query+") q(v)) <> '"+snapshot_hash+"' THEN RAISE EXCEPTION 'IAM changed; retry inspection'; END IF; END $guard$;"
verify=[]
for key,rows in expected.items():
    for row in rows:
        # Exact field verification including preserved hashes stays server-side.
        fields=' AND '.join('t.'+quoted(k)+' IS NOT DISTINCT FROM s.'+quoted(k) for k in row)
        verify.append("DO $v$ BEGIN IF NOT EXISTS (SELECT 1 FROM iam."+tables[key]+" t CROSS JOIN json_populate_record(NULL::iam."+tables[key]+","+literal(row)+") s WHERE "+fields+") THEN RAISE EXCEPTION 'IAM verification failed'; END IF; END $v$;")
sql='BEGIN;\nLOCK TABLE '+','.join('iam.'+t for t in tables.values())+' IN SHARE ROW EXCLUSIVE MODE;\n'+guard+'\n'+'\n'.join(statements+verify)
if statements:
    audit={'Id':str(uuid.uuid4()),'UserId':None,'EntityType':'yetki.referans.aktarim',
           'EntityId':'00000000-0000-0000-0000-000000000000','Action':'aktarim',
           'OldValues':None,'NewValues':counts,'IpAddress':None,'UserAgent':'authorized-maintenance',
           'Context':{'ozet':'Kullanıcı onayıyla 0.59 IAM referansı multi-test ortamına aktarıldı. Mevcut şifreler ve süper admin bayrakları korundu.',
                      'source':'0.59 read-only','target':'multi-test','sessionsCopied':False},'CreatedAt':now}
    sql+='\nINSERT INTO iam.iam_audit_logs SELECT * FROM json_populate_record(NULL::iam.iam_audit_logs,'+literal(audit)+');'
sql+='\n'+('COMMIT;' if c['action']=='Apply' else 'ROLLBACK;')
run(sql)
print('Verified '+('COMMITTED' if c['action']=='Apply' else 'ROLLED BACK'))
