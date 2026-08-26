import json, re, socket
cfg = json.load(open('/opt/ECSProsAI/src/ECSPros.Api/appsettings.json'))
red = cfg.get('ConnectionStrings', {}).get('Redis', '')
pwd = re.search(r'password=([^,;]+)', red)
pwd = pwd.group(1) if pwd else None

def cmd(*parts):
    out = [b'*%d' % len(parts)]
    for p in parts:
        b = p.encode(); out.append(b'$%d' % len(b)); out.append(b)
    return b'\r\n'.join(out) + b'\r\n'

s = socket.create_connection(('127.0.0.1', 6379), timeout=3); s.settimeout(5)
def read():
    pre = s.recv(1)
    if not pre: return None
    line = b''
    while not line.endswith(b'\r\n'): line += s.recv(1)
    line = line[:-2]
    if pre == b'$':
        n = int(line); d = b''
        while len(d) < n + 2: d += s.recv(n + 2 - len(d))
        return d[:n].decode('utf-8','replace')
    if pre == b'*':
        n = int(line); return [read() for _ in range(n)]
    if pre == b':': return int(line)
    if pre == b'+': return line.decode()
    if pre == b'-': return 'ERR ' + line.decode()
    return line.decode()

if pwd:
    s.sendall(cmd('AUTH', pwd)); read()
s.sendall(cmd('KEYS', '*')); keys = read()
print('keys:', keys)
for k in (keys if isinstance(keys, list) else []):
    s.sendall(cmd('TTL', k)); print('  TTL', k, '->', read(), 'sn')
    s.sendall(cmd('TYPE', k)); print('    TYPE ->', read())
