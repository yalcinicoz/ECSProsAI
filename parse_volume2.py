import csv, re, collections

rows = list(csv.reader(open('drinkware.tsv', encoding='utf-8'), delimiter='\t'))
data = [r for r in rows if len(r) == 3]

SMALL = {'tlm_termos','tlm_mug','tlm_bardak','tlm_kadeh','tlm_kamp_matarasi','tlm_spor_matara','tlm_shaker'}

def parse_volume(name):
    nl = name.lower()
    nl = re.sub(r'(?<![\d.])(\.\d)', r'0\1', nl)
    m = re.search(r'(\d+(?:[.,]\d+)?)\s*(?:lt|l)\b', nl)
    if m:
        val = m.group(1).replace(',', '.')
        return round(float(val) * 1000), 'L'
    m = re.search(r'(\d+[.,]\d+)\s*ml\b', nl)
    if m:
        val = m.group(1).replace(',', '.')
        return round(float(val) * 1000), 'L-typo'
    m = re.search(r'(\d+)\s*ml\b', nl)
    if m:
        return int(m.group(1)), 'ml'
    return None, None

c = collections.Counter()
big_small = []
unparsed = []
for grp, code, name in data:
    ml, src = parse_volume(name)
    if ml is None:
        unparsed.append((grp, code, name))
        continue
    c[ml] += 1
    if ml >= 10000 and grp in SMALL:
        big_small.append((grp, code, name, ml))

print("=== distinct ml ===")
for ml, n in sorted(c.items()):
    print(f"  {ml:6d} -> {n}")

print("\n=== small-group products with ml>=10000 (suspicious) ===")
for g, code, name, ml in big_small:
    print(f"  [{g}] {code} ml={ml} :: {name}")

print("\n=== unparsed ===", len(unparsed))
for g, c, n in unparsed:
    print(f"  [{g}] {n}")
