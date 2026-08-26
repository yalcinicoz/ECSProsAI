import re, html, sys
for f in ['/tmp/paytr_test_araclari.html', '/tmp/paytr_test_kart.html']:
    t = open(f, encoding='utf-8', errors='ignore').read()
    t = re.sub(r'<script.*?</script>', ' ', t, flags=re.S)
    t = re.sub(r'<style.*?</style>', ' ', t, flags=re.S)
    t = re.sub(r'<[^>]+>', ' ', t)
    t = html.unescape(t)
    t = re.sub(r'\s+', ' ', t).strip()
    print('=====', f, '=====')
    print(t[:4000])
    print()
