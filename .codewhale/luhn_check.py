def luhn(k):
    r = ''.join(c for c in k if c.isdigit())
    if len(r) < 12:
        return False
    s = 0
    alt = False
    for i in range(len(r) - 1, -1, -1):
        d = int(r[i])
        if alt:
            d *= 2
            if d > 9:
                d -= 9
        s += d
        alt = not alt
    return s % 10 == 0

for k in ['4355084355084358', '5406675406675403', '9792030394440796', '4355084355084359', '1234567890123456', '0000000000000000']:
    print(k, luhn(k))
