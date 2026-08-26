import csv, re, collections

rows = list(csv.reader(open('drinkware.tsv', encoding='utf-8'), delimiter='\t'))
data = [r for r in rows if len(r) == 3]

# Turkish color lexicon (lowercased). Order matters for multi-word matches.
colors = [
    'gunmetal shine', 'hammertone rose', 'hammertone green', 'spring green',
    'mor çiçek', 'altın mercan', 'kil rengi', 'toz pembe', 'parlak krem',
    'koyu mavi', 'açık mavi', 'lacivert', 'turkuaz', 'turuncu',
    'siyah', 'beyaz', 'kırmızı', 'mavi', 'yeşil', 'sarı', 'pembe',
    'mor', 'gri', 'krem', 'bej', 'kahverengi', 'bordo', 'lila',
    'gümüş', 'altın', 'çelik', 'haki', 'kızıl', 'şeffaf', 'doğal',
    'dried pine', 'rose quartz', 'twilight', 'ash', 'cream', 'chili',
    'gunmetal', 'hammertone', 'quencher', 'iceflow', 'aerolight',
]

found = collections.Counter()
no_color = []
for grp, code, name in data:
    nl = name.lower()
    matched = [c for c in colors if c in nl]
    if matched:
        # pick the longest match as most specific
        best = max(matched, key=len)
        found[best] += 1
    else:
        no_color.append((grp, code, name))

print("=== COLOR matches (count) ===")
for k, c in sorted(found.items(), key=lambda kv: -kv[1]):
    print(f"{k!r}  -> {c}")
print("\n=== no color detected ===", len(no_color))
for g, c, n in no_color:
    print(f"  [{g}] {n}")
