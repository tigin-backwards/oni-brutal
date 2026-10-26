#!/usr/bin/env python3
"""Helpers of scripts/qa.sh.
  qa-tools.py signatures LOG              the log's exceptions and errors, one normalized line each
  qa-tools.py new-signatures REF NEW      lines of NEW whose canonical form REF lacks (signature files)
  qa-tools.py image REFERENCE NEW DIFF [--crop X,Y,W,H] [--crop-new X,Y,W,H] [--mask X,Y,W,H]...
                                          fraction of (unmasked) pixels differing visibly; writes
                                          DIFF (a mask). --crop cuts both (--crop-new the new one
                                          apart); masks are relative to the crop
  qa-tools.py verify LOG                  each verify mode's last report; exit 1 on a mismatch
  qa-tools.py perf STDOUT                 steady-window frame rates and game time span
"""
import re, sys

def normalize(text):
    text = re.sub(r' \(at [^)]*\)', '', text)  # source locations change with every edit
    text = re.sub(r' in \S+:line \d+', '', text)  # .NET's form of the same
    text = re.sub(r'0x[0-9a-fA-F]+', 'N', text)
    text = re.sub(r'\d+(\.\d+)?', 'N', text)
    return text.strip()

# Runtime internals: the first frame of a trace only says which runtime threw (Mono's
# System.IO.FileStream, .NET's Interop.ThrowExceptionForIoErrno, Unity's or our logger, our
# engine's dispatch), so a signature keeps the message alone when that frame is one of these.
RUNTIME_FRAME = re.compile(r'^(System|Interop|Microsoft|Mono)\.|^UnityEngine\.(Debug|Logger|DebugLogHandler|StackTraceUtility)\.'
                           r'|^(?:OniMoono|OniBrutal)\.')

def canonical(signature):
    """A signature as both runtimes write it: Mono and .NET word some exception messages
    differently ('…'. against "…", a trailing period or none), and stack frames come as
    "at A.B (x) [N] in <…>:N" (an exception's own, in both players; with symbols, in a file),
    "A:B(x)" (Unity's) and "A:B (x)" (ours before 2026-10-24). Idempotent, so it also applies
    to signatures stored by an earlier version (references)."""
    signature = signature.replace('OniBrutal.', 'OniMoono.')  # stored before the rename
    message, _, frame = signature.partition(' @ ')
    # A trace appended to the message on its own line ("Error:" + message + " " + StackTrace, as
    # WorldGen logs it) starts with its first frame: that frame comes before the one read from
    # the next line, unless it is the runtime's own. .NET leaves out a frame the JIT inlined (a
    # Dictionary indexer's), which Mono's trace has.
    inline = re.search(r'\s{2,}at (\S.*)$', message)
    if inline:
        message = message[:inline.start()]
        first = clean_frame(inline.group(1))
        if not RUNTIME_FRAME.search(first):
            frame = first
    message = re.sub(r'\.$', '', message.replace("'", '"').rstrip())
    # A path into the run's own folder (out/e2e/SPEC/RUN_ID/PLAYER-N/home/...) names the run
    # id, which differs between the invocations that ran the original and ours.
    message = re.sub(r'/[^"\s]*/(?:original|ours)-N/home/', 'RUN/home/', message)
    if frame:
        frame = clean_frame(frame)
        if RUNTIME_FRAME.search(frame):
            frame = ''
    return message + (' @ ' + frame if frame else '')

def clean_frame(frame):
    """A frame's method alone, as both runtimes name it."""
    frame = re.sub(r'^at ', '', frame.strip())
    # A Harmony replacement's frame is its patched method's: Mono names it "(wrapper
    # dynamic-method) MonoMod.Utils.DynamicMethodDefinition.T.M_PatchN", .NET "T.M_PatchN", and
    # ours patches some of Harmony's own methods (ARCHITECTURE D14, D38) that the original runs
    # unpatched, so a mod's failed patch shows "PatchProcessor.Patch_Patch2" in ours alone.
    frame = re.sub(r'^\(wrapper dynamic-method\) (?:MonoMod\.Utils\.DynamicMethodDefinition\.)?', '', frame)
    frame = re.sub(r'_Patch(?:N|\d+)(?=\s*(?:\(|$))', '', frame)
    # Mono's location: "[0x…] in <mvid>:0", or the source file with symbols (ours has the
    # engine's).
    frame = re.sub(r' (\[N\] in .*|in <[^>]*>(:N)?)$', '', frame)
    frame = re.sub(r'\s*\(.*\)$', '', frame).replace(':', '.')
    # Mono names a generic type's parameters ("Dictionary`2[TKey,TValue]"), .NET does not.
    return re.sub(r"(`(?:N|\d+))\[[^\]]*\]", r"\1", frame)

def unknown(signatures, reference):
    """The SIGNATURES not in REFERENCE once both are canonical. A reference entry with no frame
    (written when its first frame was runtime internals) stands for its message with any frame."""
    known = {canonical(s) for s in reference}
    return sorted(s for s in signatures
                  if canonical(s) not in known and canonical(s).partition(' @ ')[0] not in known)

def new_signatures(reference, new):
    read = lambda path: open(path, encoding='utf-8', errors='replace').read().splitlines()
    for line in unknown(read(new), read(reference)):
        print(line)

def joined_inner_exceptions(lines):
    """LINES with each inner exception on its outer one's line: .NET's Exception.ToString starts
    " ---> Inner: message" on a line of its own, Mono's continues the outer message's line
    (ARCHITECTURE D37)."""
    out = []
    for line in lines:
        if out and re.match(r'\s*---> ', line):
            out[-1] = out[-1].rstrip() + ' ' + line.strip()
        else:
            out.append(line)
    return out

def signatures(path):
    lines = joined_inner_exceptions(open(path, encoding='utf-8', errors='replace').read().splitlines())
    found = set()
    for i, line in enumerate(lines):
        body = re.sub(r'^\[[^\]]*\] \[\d+\] \[[A-Z]+\] ', '', line)
        if re.search(r'\b\w+(Exception|Error)\b:', body) or '[ERROR]' in line:
            frame = ''
            for nxt in lines[i + 1:i + 9]:
                if re.match(r'\s*(at |[\w.`]+[:.][\w<>`]+ ?\()', nxt):
                    frame = nxt.strip()
                    if not RUNTIME_FRAME.search(re.sub(r'^at ', '', frame).replace(':', '.')):
                        break  # the first frame past the runtime's own
                    frame = ''
            found.add(canonical(normalize(body) + (' @ ' + normalize(frame) if frame else '')))
    for s in sorted(found):
        print(s)

def rect(text):
    x, y, w, h = (int(round(float(v))) for v in text.split(','))
    return x, y, w, h

def compare_images(reference, new, diff, crop=None, crop_new=None, masks=(), level=40, size_tolerance=2):
    """The fraction of unmasked pixels whose largest channel difference exceeds LEVEL. CROP
    (x, y, w, h) cuts both images, CROP_NEW the new one instead; MASKS are rectangles relative to
    the crop that do not count. Sizes up to SIZE_TOLERANCE pixels apart are trimmed to the
    smaller. DIFF gets the mask image: differing pixels white, masked ones grey."""
    from PIL import Image, ImageChops
    import os
    import numpy as np
    if not os.path.exists(reference) or not os.path.exists(new):
        return {'fraction': 1.0, 'note': 'missing'}
    images = []
    for path, box in ((reference, crop), (new, crop_new or crop)):
        image = Image.open(path).convert('RGB')
        if box:
            x, y, w, h = (int(round(v)) for v in box)
            clipped = (max(0, x), max(0, y), min(image.width, x + w), min(image.height, y + h))
            if clipped[2] <= clipped[0] or clipped[3] <= clipped[1]:
                return {'fraction': 1.0, 'note': f'crop {list(box)} is off the image'}
            image = image.crop(clipped)
        images.append(image)
    a, b = images
    if a.size != b.size:
        if abs(a.width - b.width) > size_tolerance or abs(a.height - b.height) > size_tolerance:
            return {'fraction': 1.0, 'note': f'size {a.size} against {b.size}'}
        size = (min(a.width, b.width), min(a.height, b.height))
        a, b = a.crop((0, 0) + size), b.crop((0, 0) + size)
    differs = np.asarray(ImageChops.difference(a, b)).max(axis=2) > level
    counted = np.ones(differs.shape, dtype=bool)
    for m in masks:
        x, y, w, h = (int(round(v)) for v in m)
        counted[max(0, y):max(0, y + h), max(0, x):max(0, x + w)] = False
    if diff:
        Image.fromarray(np.where(counted, differs * 255, 128).astype('uint8')).save(diff)
    total = int(counted.sum())
    fraction = float((differs & counted).sum()) / total if total else 0.0
    return {'fraction': round(fraction, 6), 'size': list(a.size), 'masked': int(counted.size - total)}

def image(reference, new, diff, *options):
    crop = crop_new = None
    masks, level = [], 40
    for name, value in zip(options[::2], options[1::2]):
        if name == '--crop': crop = rect(value)
        elif name == '--crop-new': crop_new = rect(value)
        elif name == '--mask': masks.append(rect(value))
        elif name == '--level': level = int(value)
        else: sys.exit(f'unknown option {name}')
    result = compare_images(reference, new, diff, crop, crop_new, masks, level)
    print(f'{result["fraction"]:.4f}' + (f' {result["note"]}' if 'note' in result else ''))

# Verify-mode lines: periodic summaries (each kind of check has its own, all counts must be zero)
# and per-incident lines (any is a failure).
VERIFY_NAMES = r'(fast-[a-z-]+|canvas skips?|component layouts|texel copies|partial uploads|parallel draws)'
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
    {'signatures': signatures, 'new-signatures': new_signatures, 'image': image, 'verify': verify, 'perf': perf}[cmd](*args)
