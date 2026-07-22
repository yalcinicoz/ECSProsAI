#!/usr/bin/env python3
"""Beden (attribute_values, tip=beden) SortOrder ataması — 2026-07-22 kullanıcı kararı.

Kural: tek artan dizi (1..N), gruplama yok.
  1) Harfli bedenler gerçek-hayat sırasıyla önce (XXS..10XL; SM/LX/XL-2XL gibi aralıklar dahil)
  2) Sayısal/yaşlı bedenler sayı değerine göre (AY -> /12 ile yaş cinsine çevrilir; "YENİ DOĞAN"
     sayısal bloğun başına; 8 < 9 < 10 — sözlük sırası ASLA kullanılmaz)
  3) Kalanlar (ST/STD vb.) alfabetik olarak en sona.

Tekrarlanabilir: go-live/veri aktarımı sonrası yeniden çalıştırılmalı (idempotent).
Kullanım: PGPASSWORD=... python3 tools/veri-bakim/beden-siralama.py [--dry-run]
"""
import os, re, subprocess, sys

TIP_ID = "bfe6347a-15e9-4f7f-84c3-4d9f4e96eef6"  # definition.attribute_types code='beden'
HARF = ['XXXS','2XS','XXS','XS','XS-S','XS/S','XSS','S','S-M','S/M','SM','M','M-L','M/L','ML',
        'L','L-XL','L/XL','LX','LXL','XL','XL-XXL','XL-2XL','XL/XXL','XLXXL','XXL','2XL',
        'XXL-3XL','2XL-3XL','3XL','XXXL','3XL-4XL','4XL','4XXL','5XL','6XL','7XL','8XL','9XL','10XL']
HARF_IX = {h: i for i, h in enumerate(HARF)}

def tr_upper(s): return s.replace('i', 'İ').replace('ı', 'I').upper()

def anahtar(ad):
    a = tr_upper(ad.strip()).replace(' ', '')
    if a in HARF_IX: return (0, HARF_IX[a], 0.0, a)
    if 'YENİDOĞAN' in a or 'YENIDOGAN' in a: return (1, -1.0, 0.0, a)
    m = re.match(r'^(\d+(?:[.,]\d+)?)', a)
    if m:
        n = float(m.group(1).replace(',', '.'))
        deger = n / 12.0 if ('AY' in a and 'YAŞ' not in a and 'AYAK' not in a) else n
        m2 = re.search(r'[-–/xX](\d+(?:[.,]\d+)?)', a[m.end():])
        n2 = float(m2.group(1).replace(',', '.')) if m2 else 0.0
        return (1, deger, n2, a)
    return (2, 0.0, 0.0, a)

def psql(sql, *ekstra):
    return subprocess.run(
        ['psql', '-h', 'localhost', '-U', 'ecommerce', '-d', 'ecommerce_db', '-t', '-A', *ekstra, '-c', sql],
        capture_output=True, text=True, check=True).stdout

if __name__ == '__main__':
    dry = '--dry-run' in sys.argv
    satirlar = [l.split('|', 1) for l in psql(
        f'SELECT "Id", "NameI18n"->>\'tr\' FROM definition.attribute_values '
        f'WHERE "AttributeTypeId"=\'{TIP_ID}\' AND NOT "IsDeleted";', '-F', '|').splitlines() if '|' in l]
    sirali = sorted(satirlar, key=lambda x: anahtar(x[1]))
    print(f'{len(sirali)} beden; ilk 10: {[x[1] for x in sirali[:10]]}')
    if dry: sys.exit(0)
    sql = 'BEGIN;\n' + '\n'.join(
        f'UPDATE definition.attribute_values SET "SortOrder"={i}, "UpdatedAt"=now() WHERE "Id"=\'{vid}\';'
        for i, (vid, _) in enumerate(sirali, 1)) + '\nCOMMIT;\nANALYZE definition.attribute_values;'
    subprocess.run(['psql', '-h', 'localhost', '-U', 'ecommerce', '-d', 'ecommerce_db', '-q'],
                   input=sql, text=True, check=True)
    print('UYGULANDI')
