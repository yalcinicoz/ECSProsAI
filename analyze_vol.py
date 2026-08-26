import csv, re, collections

rows = list(csv.reader(open('drinkware.tsv', encoding='utf-8'), delimiter='\t'))
header = rows[0]
data = rows[1:]

# volume patterns
vol_pat = re.compile(r'(\d+(?:[.,]\d+)?)\s*(?:Lt?|LT|l|L)\b', re.IGNORECASE)
oz_pat  = re.compile(r'(\d+(?:[.,]\d+)?)\s*oz\b', re.IGNORECASE)
ml_pat  = re.compile(r'(\d+(?:[.,]\d+)?)\s*ml\b', re.IGNORECASE)

vols = collections.Counter()
ozs = collections.Counter()
mls = collections.Counter()
no_vol = []
for grp, code, name in data:
    v = vol_pat.findall(name)
    o = oz_pat.findall(name)
    m = ml_pat.findall(name)
    for x in v: vols[x] += 1
    for x in o: ozs[x] += 1
    for x in m: mls[x] += 1
    if not v and not o and not m:
        no_vol.append((grp, code, name))

print("=== LITER volumes (count) ===")
for k, c in sorted(vols.items(), key=lambda kv: float(kv[0].replace(',', '.'))):
    print(f"{k}  -> {c}")
print("\n=== OZ volumes (count) ===")
for k, c in sorted(ozs.items(), key=lambda kv: float(kv[0].replace(',', '.'))):
    print(f"{k}  -> {c}")
print("\n=== ML volumes (count) ===")
for k, c in sorted(mls.items(), key=lambda kv: float(kv[0].replace(',', '.'))):
    print(f"{k}  -> {c}")
print("\n=== no volume detected ===", len(no_vol))
for g, c, n in no_vol:
    print(f"  [{g}] {n}")
