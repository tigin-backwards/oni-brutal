#!/usr/bin/env python3
# Compares two --dump-ui files (PLAN M9): elements are matched by path, and each differing field is
# reported. Exit status 0 when the dumps agree.
#   scripts/ui-diff.py SHIPPED.txt OURS.txt [--ignore FIELD ...]
import sys

def parse(path):
    elements = {}
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\n')
        if not line or line.startswith('  '):  # vertex listings (--dump-ui-vertices)
            continue
        head, *components = line.split(' | ')
        key, _, rest = head.partition('\t')
        fields = {'': rest}
        for c in components:
            name, _, values = c.partition(' ')
            fields[name] = values
        elements[key] = fields
    return elements

args = sys.argv[1:]
ignore = set()
if '--ignore' in args:
    i = args.index('--ignore')
    ignore = set(args[i + 1:])
    args = args[:i]
import os
for path in args[:2]:
    if not os.path.exists(path):
        print(f'missing dump: {path}')
        sys.exit(2)
a, b = parse(args[0]), parse(args[1])
differences = 0
for key in a.keys() - b.keys():
    print(f'only in {args[0]}: {key}'); differences += 1
for key in b.keys() - a.keys():
    print(f'only in {args[1]}: {key}'); differences += 1
for key in sorted(a.keys() & b.keys()):
    fa, fb = a[key], b[key]
    for name in sorted(fa.keys() | fb.keys()):
        if name in ignore:
            continue
        if fa.get(name) != fb.get(name):
            print(f'{key} [{name or "rect"}]\n  - {fa.get(name)}\n  + {fb.get(name)}')
            differences += 1
print(f'{len(a)} vs {len(b)} elements, {differences} differences')
sys.exit(1 if differences else 0)
