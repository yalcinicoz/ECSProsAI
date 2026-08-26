import csv, re, collections

rows = list(csv.reader(open('drinkware.tsv', encoding='utf-8'), delimiter='\t'))
data = [r for r in rows if len(r) == 3]

def norm_num(s):
    return s.replace(',', '.')

def to_ml_lit(val_str):
    try:
        return round(float(norm_num(val_str)) * 1000)
    except ValueError:
        return None

def parse_volume(name):
    """Return canonical (display, ml, source) or None."""
    nl = name.lower()
    # 1) liter value: number followed by L/Lt/l (word boundary), optional spaces
    lit = re.search(r'(\d+(?:[.,]\d+)?)\s*(?:lt|l)\b', nl)
    # 2) 'ml' but clearly a typo for liters (e.g. 0,35ml) -> treat as L
    ml_typo = re.search(r'(\d+[.,]\d+)\s*ml\b', nl)
    # 3) real ml (integer, for biberon)
    ml_int = re.search(r'(\d+)\s*ml\b', nl)
    # 4) oz
    oz = re.search(r'(\d+(?:[.,]\d+)?)\s*oz\b', nl)

    if lit:
        val = norm_num(lit.group(1))
        ml = to_ml_lit(lit.group(1))
        return (val, ml, 'L')
    if ml_typo:
        val = norm_num(ml_typo.group(1))
        ml = to_ml_lit(ml_typo.group(1))
        return (val, ml, 'L-typo')
    if ml_int:
        ml = int(ml_int.group(1))
        return (str(ml), ml, 'ml')
    if oz:
        ozv = float(norm_num(oz.group(1)))
        ml = round(ozv * 29.5735)
        return (str(ozv), ml, 'oz')
    return None

# oz -> liter canonical (for display mapping later)
def ml_to_lit_display(ml):
    if ml < 1000:
        # e.g. 230 -> "0,23", 350 -> "0,35", 940 -> "0,94"
        s = f"{ml/1000:.2f}".rstrip('0').rstrip('.')
        s = s.replace('.', ',')
        return s + " L"
    else:
        s = f"{ml/1000:.2f}".rstrip('0').rstrip('.')
        s = s.replace('.', ',')
        return s + " L"

ml_counter = collections.Counter()
unparsed = []
per_group = collections.defaultdict(list)
for grp, code, name in data:
    r = parse_volume(name)
    if r is None:
        unparsed.append((grp, code, name))
        continue
    val, ml, src = r
    ml_counter[(ml, src)] += 1
    per_group[grp].append((code, name, val, ml, src))

print("=== distinct (ml, source) counts ===")
for (ml, src), c in sorted(ml_counter.items()):
    print(f"  ml={ml:6d}  src={src:7s}  display={ml_to_lit_display(ml) if src in ('L','L-typo','oz') else str(ml)+'ml'}  count={c}")

print("\n=== unparsed ===", len(unparsed))
for g, c, n in unparsed:
    print(f"  [{g}] {n}")
