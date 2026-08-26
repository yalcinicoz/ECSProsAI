import re, html
t = open('/tmp/paytr_home.html', encoding='utf-8', errors='ignore').read()
for m in re.findall(r'<a[^>]+href="([^"]+)"[^>]*>(.*?)</a>', t, flags=re.S):
    href = m[0]
    txt = re.sub(r'<[^>]+>', '', m[1])
    txt = html.unescape(re.sub(r'\s+', ' ', txt)).strip()
    if re.search(r'başvur|üye|kayıt|giriş|hesap|mağaza|başla|hemen|apply|register|login|sign', txt, re.I) or re.search(r'basvur|uye|register|login|basla|magaza|giris', href, re.I):
        print(href, '=>', txt[:90])
