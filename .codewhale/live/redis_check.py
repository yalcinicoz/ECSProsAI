import json, re, socket

# Redis baglanti bilgisini appsettings'ten oku (ekrana yazdirmadan)
cfg = json.load(open('/opt/ECSProsAI/src/ECSPros.Api/appsettings.json'))
red = cfg.get('ConnectionStrings', {}).get('Redis', '')
host = '127.0.0.1'
port = 6379
m = re.search(r'password=([^,;]+)', red)
pwd = m.group(1) if m else None

def cmd(*parts):
    out = [b'*%d' % len(parts)]
    for p in parts:
        b = p.encode()
        out.append(b'$%d' % len(b))
        out.append(b)
    return b'\r\n'.join(out) + b'\r\n'

s = socket.create_connection((host, port), timeout=3)
s.settimeout(5)

def send(c):
    s.sendall(c)

def read():
    # basit RESP okuyucu (bulk string / integer / array icin yeterli)
    b = s.recv(1)
    if not b:
        return None
    prefix = b
    line = b''
    while not line.endswith(b'\r\n'):
        line += s.recv(1)
    line = line[:-2]  # \r\n
    if prefix == b'$':
        n = int(line)
        data = b''
        while len(data) < n + 2:
            data += s.recv(n + 2 - len(data))
        return data[:n].decode('utf-8', 'replace')
    if prefix == b':':
        return int(line)
    if prefix == b'+':
        return line.decode()
    if prefix == b'-':
        return 'ERR ' + line.decode()
    if prefix == b'*':
        n = int(line)
        return [read() for _ in range(n)]
    return line.decode()

if pwd:
    send(cmd('AUTH', pwd))
    print('AUTH ->', read())

for section in ['stats', 'memory', 'keyspace', 'clients', 'commandstats']:
    send(cmd('INFO', section))
    r = read()
    print('\n===== INFO', section, '=====')
    if isinstance(r, str):
        # sadece ilgili satirlar
        for ln in r.splitlines():
            if section == 'stats' and any(k in ln for k in ['keyspace_hits', 'keyspace_misses', 'total_commands_processed', 'total_connections_received', 'rejected_connections', 'expired_keys', 'evicted_keys']):
                print(ln)
            elif section == 'memory' and any(k in ln for k in ['used_memory_human', 'used_memory_peak_human', 'maxmemory_human', 'maxmemory_policy']):
                print(ln)
            elif section == 'keyspace':
                print(ln)
            elif section == 'clients' and any(k in ln for k in ['connected_clients', 'blocked_clients', 'maxclients']):
                print(ln)
            elif section == 'commandstats' and any(k in ln for k in ['cmdstat_get', 'cmdstat_set', 'cmdstat_del', 'cmdstat_info', 'cmdstat_ping']):
                print(ln)
    else:
        print(r)

send(cmd('DBSIZE'))
print('\nDBSIZE ->', read())
