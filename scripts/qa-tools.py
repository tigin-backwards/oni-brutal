#!/usr/bin/env python3
"""Helpers of scripts/qa.sh.
  qa-tools.py signatures LOG              the log's exceptions and errors, one normalized line each
  qa-tools.py image REFERENCE NEW DIFF    fraction of pixels differing visibly; writes DIFF (a mask)
  qa-tools.py verify LOG                  each verify mode's last report; exit 1 on a mismatch
  qa-tools.py perf STDOUT                 steady-window frame rates and game time span
"""
import re, sys

def normalize(text):
    text = re.sub(r' \(at [^)]*\)', '', text)  # source locations change with every edit
    text = re.sub(r'0x[0-9a-fA-F]+', 'N', text)
    text = re.sub(r'\d+(\.\d+)?', 'N', text)
    return text.strip()

def signatures(path):
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    found = set()
    for i, line in enumerate(lines):
        body = re.sub(r'^\[[^\]]*\] \[\d+\] \[[A-Z]+\] ', '', line)
        if re.search(r'\b\w+(Exception|Error)\b:', body) or '[ERROR]' in line:
            frame = ''
            for nxt in lines[i + 1:i + 4]:
                if re.match(r'\s*(at |[\w.`]+[:.][\w<>`]+ ?\()', nxt):
                    frame = nxt.strip()
                    break
            found.add(normalize(body) + (' @ ' + normalize(frame) if frame else ''))
    for s in sorted(found):
        print(s)

def image(reference, new, diff):
    from PIL import Image, ImageChops
    import os
    if not os.path.exists(reference) or not os.path.exists(new):
        print('1.0000 missing'); return
    a = Image.open(reference).convert('RGB')
    b = Image.open(new).convert('RGB')
    if a.size != b.size:
        print(f'1.0000 size {a.size} against {b.size}')
        return
    d = ImageChops.difference(a, b)
    import numpy as np
    m = np.asarray(d).max(axis=2) > 40
    Image.fromarray((m * 255).astype('uint8')).save(diff)
    print(f'{m.mean():.4f}')

# Verify-mode lines: periodic summaries (each kind of check has its own, all counts must be zero)
# and per-incident lines (any is a failure).
VERIFY_NAMES = r'(fast-[a-z-]+|canvas skips?|component layouts|texel copies)'
SUMMARY = re.compile(r'^\d+ [a-z ,\d-]*?\d+ (?:wrong skips|wrong|stale items not marked|stale|mismatches|changed while copying)'
                     r'(?:, [a-z\d ]+)?$')
ZERO_COUNTS = re.compile(r'(\d+) (?:wrong skips|wrong|stale items not marked|stale|mismatches|changed while copying)')

def verify(path):
    last, incidents = {}, []
    for line in open(path, encoding='utf-8', errors='replace'):
        if 'canvas skip wrong' in line:
            incidents.append(line.strip()); continue
        m = re.search(VERIFY_NAMES + r': (.*)$', line)
        if not m: continue
        name, report = m.group(1), m.group(2).strip()
        if SUMMARY.match(report):
            # The summary's shape (its words) names the check: one tweak can report several.
            last[(name, re.sub(r'\d+', 'N', report))] = report
        elif re.search(r'\(\d+\)$|wrong|stale|mismatch|ran \d+ times|had stale', report):
            incidents.append(f'{name}: {report}')
    bad = False
    for (name, _), report in sorted(last.items()):
        ok = all(c == '0' for c in ZERO_COUNTS.findall(report))
        bad |= not ok
        print(f'{"ok  " if ok else "FAIL"} {name}: {report}')
    for i in incidents[:20]:
        print(f'FAIL incident {i}')
    bad |= bool(incidents)
    sys.exit(1 if bad else 0)

def perf(path):
    rows = []
    for line in open(path, encoding='utf-8', errors='replace'):
        m = re.search(r'PERF f(\d+) fps ([\d.]+) cycle (\d+)(?: time (\d+))?', line)
        if m: rows.append((int(m.group(1)), float(m.group(2)), int(m.group(3)), int(m.group(4) or 0)))
    steady = [r[1] for r in rows[1:]]  # the first window holds the load
    mean = sum(steady) / len(steady) if steady else 0.0
    span = rows[-1][3] - rows[0][3] if len(rows) > 1 else 0
    print(f'{mean:.2f} {span} {len(steady)}')

if __name__ == '__main__':
    cmd, *args = sys.argv[1:]
    {'signatures': signatures, 'image': image, 'verify': verify, 'perf': perf}[cmd](*args)
