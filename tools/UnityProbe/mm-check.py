#!/usr/bin/env python3
"""mm-check.py: Matrix4x4's products from the probe's MM lines (PROBE_SCENARIO=matmul).

  mm-check.py rule LOG          for each product, how many cases each candidate reproduces bit
                                for bit: f32 (every multiply and add rounded to float, left to
                                right) and f64 (each component's expression in double, left to
                                right, rounded to float once)
  mm-check.py diff ORIG OURS    the products whose bits differ between the two players

LOG is the original's Player.log or our player's stdout. Plain Python (struct), no numpy.
"""
import re
import struct
import sys

LINE = re.compile(r'PROBE f\d+ MM (\d+) (.*)$')


def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def bits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


def unhex(s):
    return [struct.unpack('<f', struct.pack('<I', int(h, 16)))[0] for h in s.split(',')]


def cases(path):
    for raw in open(path, errors='replace'):
        m = LINE.search(raw.rstrip('\n'))
        if not m:
            continue
        fields = dict(kv.split('=', 1) for kv in m.group(2).split())
        yield int(m.group(1)), {k: unhex(v) for k, v in fields.items()}


def dot(row, vec, mode):
    """sum(row[i] * vec[i]) left to right."""
    if mode == 'f64':
        acc = 0.0
        for a, b in zip(row, vec):
            acc += a * b  # a product of two floats is exact in double
        return f32(acc)
    acc = None
    for a, b in zip(row, vec):
        p = f32(a * b)
        acc = p if acc is None else f32(acc + p)
    return acc


def rows(m):
    return [m[r * 4:r * 4 + 4] for r in range(4)]


def p3x4(m, p, mode):
    return [dot(r, p + [1.0], mode) for r in rows(m)[:3]]


def vec(m, p, mode):
    return [dot(r[:3], p, mode) for r in rows(m)[:3]]


def point(m, p, mode):
    x, y, z, w = [dot(r, p + [1.0], mode) for r in rows(m)]
    w = f32(1.0 / w)
    return [f32(x * w), f32(y * w), f32(z * w)]


def mq(m, q, mode):
    return [dot(r, q, mode) for r in rows(m)]


def mn(m, n, mode):
    cols = [[n[r * 4 + c] for r in range(4)] for c in range(4)]
    return [dot(r, col, mode) for r in rows(m) for col in cols]


PRODUCTS = {
    'p3x4': lambda c, mode: p3x4(c['m'], c['p'], mode),
    'vec': lambda c, mode: vec(c['m'], c['p'], mode),
    'pt': lambda c, mode: point(c['m'], c['p'], mode),
    'mq': lambda c, mode: mq(c['m'], c['q'], mode),
    'mn': lambda c, mode: mn(c['m'], c['n'], mode),
}


def same(a, b):
    return all(bits(x) == bits(y) or (x != x and y != y) for x, y in zip(a, b))


def rule(path):
    total = 0
    hits = {(k, mode): 0 for k in PRODUCTS for mode in ('f32', 'f64')}
    for _, c in cases(path):
        total += 1
        for k, fn in PRODUCTS.items():
            for mode in ('f32', 'f64'):
                hits[(k, mode)] += same(fn(c, mode), c[k])
    for k in PRODUCTS:
        print(f'{k:5} f32 {hits[(k, "f32")]}/{total}  f64 {hits[(k, "f64")]}/{total}')


def diff(orig, ours):
    a, b = dict(cases(orig)), dict(cases(ours))
    keys = ['p3x4', 'pt', 'vec', 'mq', 'mn', 'inv']
    differ = {k: 0 for k in keys}
    for i in sorted(set(a) & set(b)):
        for k in keys:
            if not same(a[i][k], b[i][k]):
                differ[k] += 1
    print(f'{len(set(a) & set(b))} cases; differing: ' + ', '.join(f'{k} {v}' for k, v in differ.items()))


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == 'rule':
        rule(sys.argv[2])
    elif len(sys.argv) == 4 and sys.argv[1] == 'diff':
        diff(sys.argv[2], sys.argv[3])
    else:
        sys.exit(__doc__)
