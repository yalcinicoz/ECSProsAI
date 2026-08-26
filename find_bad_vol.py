import csv, re
rows = list(csv.reader(open('drinkware.tsv', encoding='utf-8'), delimiter='\t'))
data = [r for r in rows if len(r) == 3]
for grp, code, name in data:
    nl = name.lower()
    m = re.search(r'(?<![\d.,])(\d+(?:[.,]\d+)?)\s*(?:lt|l)\b', nl)
    if m and m.group(1) in ('35','47','75'):
        print(f"{m.group(1)}L  [{grp}] {name}")
