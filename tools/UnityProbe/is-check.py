#!/usr/bin/env python3
"""is-check.py: the invscale scenario's IS lines (InverseScale.cs).

  is-check.py diff ORIG OURS    each logged value of IS1-IS6 compared bit for bit, per tag and
                                key, with the first differences shown
  is-check.py table LOG OUT     from an original run with PROBE_IS_PART=full, the engine's
                                inverse-scale table (src/OniMoono.UnityEngine/Math/
                                InverseScale.bin): each mantissa's bits(f) - bits(1/s) + 1, two
                                bits each, four to a byte with the first in the high bits, raw
                                deflate

LOG is the original's Player.log or our player's stdout. Plain Python.
"""
import re
import sys
import zlib
from collections import Counter


def lines(path):
    out = {}
    for raw in open(path, errors='replace'):
        m = re.search(r' (IS[1-46]) (.*)$', raw.rstrip())
        if m:
            tag, rest = m.groups()
            parts = rest.split(' ')
            key = tag + ' ' + (' '.join(p for p in parts if p.startswith('s=') or '=' not in p) if tag == 'IS6' else parts[0])
            out.setdefault(key, []).append(rest)
    return out


def diff(orig, ours):
    a, b = lines(orig), lines(ours)
    total, bad, shown = Counter(), Counter(), 0
    for key, values in a.items():
        tag = key.split()[0]
        if key not in b:
            total[tag + ' missing'] += 1
            continue
        for x, y in zip(values, b[key]):
            kx = dict(p.split('=', 1) for p in x.split(' ') if '=' in p)
            ky = dict(p.split('=', 1) for p in y.split(' ') if '=' in p)
            for field, value in kx.items():
                total[tag + ' ' + field] += 1
                if value != ky.get(field):
                    bad[tag + ' ' + field] += 1
                    if shown < 10:
                        print('differs', key, field, value[:100], '|', str(ky.get(field))[:100])
                        shown += 1
    for k in sorted(total):
        print(f'{k:14s} {total[k] - bad[k]}/{total[k]}')


def table(log, out):
    classes = {}
    for raw in open(log, errors='replace'):
        m = re.search(r' IS5 (\d+) ([0-9a-f]+)', raw)
        if m:
            classes[int(m.group(1))] = bytes.fromhex(m.group(2))
    if sorted(classes) != list(range(2048)):
        sys.exit(f'{log}: {len(classes)} of 2048 IS5 lines')
    data = b''.join(classes[i] for i in range(2048))
    if any(((byte >> s) & 3) == 3 for byte in data for s in (0, 2, 4, 6)):
        sys.exit('a mantissa more than one ulp from 1/s')
    c = zlib.compressobj(9, zlib.DEFLATED, -15)
    open(out, 'wb').write(c.compress(data) + c.flush())


if __name__ == '__main__':
    if len(sys.argv) == 4 and sys.argv[1] == 'diff':
        diff(sys.argv[2], sys.argv[3])
    elif len(sys.argv) == 4 and sys.argv[1] == 'table':
        table(sys.argv[2], sys.argv[3])
    else:
        sys.exit(__doc__)
