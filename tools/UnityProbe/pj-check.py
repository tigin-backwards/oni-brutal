#!/usr/bin/env python3
"""pj-check.py: projection, look-at, rotation and camera math from the probe's PJ lines
(PROBE_SCENARIO=projection, ProjectionMath.cs).

  pj-check.py rule LOG          how many logged cases each model reproduces bit for bit:
                                PO Mesa's _math_float_ortho, PF Mesa's _math_matrix_frustum,
                                PP the measured Perspective (cosf / sinf of (fov / 360) pi),
                                PL gluLookAt's basis in Unity's order, PR AngleAxis, PA Mesa's
                                invert_matrix_3d_general, PC the camera's projectionMatrix and
                                cameraToWorldMatrix, PCP gluProject / gluUnProject (double)
  pj-check.py diff ORIG OURS    the lines and fields whose bits differ between the two players

LOG is the original's Player.log or our player's stdout. NaNs compare equal whatever their sign
(Python makes positive NaNs, x86 negative ones; the unit tests check the bits). sinf, cosf come
from this machine's libm through ctypes, as the player's and .NET's MathF do on Linux.
"""
import ctypes
import math
import re
import struct
import sys

LINE = re.compile(r'PROBE f\d+ PJ (\w+) (.*)$')
LM = ctypes.CDLL('libm.so.6')
for _n in ('sinf', 'cosf'):
    getattr(LM, _n).restype = ctypes.c_float
    getattr(LM, _n).argtypes = [ctypes.c_float]
PI32 = struct.unpack('<f', struct.pack('<f', math.pi))[0]


def f32(x):
    try:
        return struct.unpack('<f', struct.pack('<f', x))[0]
    except OverflowError:
        return math.copysign(math.inf, x)


def bits(x):
    return struct.unpack('<I', struct.pack('<f', x))[0]


def unhex(s):
    return [struct.unpack('<f', struct.pack('<I', int(h, 16)))[0] for h in s.split(',')]


def load(path):
    """[(kind, index tuple, {key: floats or str})] in log order."""
    out = []
    for raw in open(path, errors='replace'):
        m = LINE.search(raw.rstrip('\n'))
        if not m:
            continue
        kind, rest = m.group(1), m.group(2).split()
        idx = []
        while rest and '=' not in rest[0]:
            idx.append(rest.pop(0))
        fields = {}
        for kv in rest:
            if '=' in kv:
                k, v = kv.split('=', 1)
                parts = v.split(',')
                fields[k] = unhex(v) if all(re.fullmatch('[0-9a-f]{8}', p) for p in parts) else v
        out.append((kind, tuple(idx), fields))
    return out


def same1(x, y):
    return bits(x) == bits(y) or (x != x and y != y)


def same(a, b):
    return len(a) == len(b) and all(same1(x, y) for x, y in zip(a, b))


def D(a, b):
    if b == 0:
        if a == 0 or a != a:
            return math.nan
        return math.copysign(math.inf, math.copysign(1, a) * math.copysign(1, b))
    return f32(a / b)


def M(a, b): return f32(a * b)
def A(a, b): return f32(a + b)
def S(a, b): return f32(a - b)


# ---- models ----
def mesa_ortho(l, r, b, t, n, f):
    """_math_float_ortho (Mesa 20.3.5 m_matrix.c), row-major."""
    return [D(2.0, S(r, l)), 0.0, 0.0, D(-A(r, l), S(r, l)),
            0.0, D(2.0, S(t, b)), 0.0, D(-A(t, b), S(t, b)),
            0.0, 0.0, D(-2.0, S(f, n)), D(-A(f, n), S(f, n)),
            0.0, 0.0, 0.0, 1.0]


def mesa_frustum(l, r, b, t, n, f):
    """_math_matrix_frustum (Mesa 20.3.5 m_matrix.c), row-major."""
    return [D(M(2.0, n), S(r, l)), 0.0, D(A(r, l), S(r, l)), 0.0,
            0.0, D(M(2.0, n), S(t, b)), D(A(t, b), S(t, b)), 0.0,
            0.0, 0.0, D(-A(f, n), S(f, n)), D(-M(M(2.0, f), n), S(f, n)),
            0.0, 0.0, -1.0, 0.0]


def perspective(fov, a, n, f):
    """Measured: float, cosf / sinf of (fov / 360) pi, the depth row over (near - far)."""
    r = M(D(fov, 360.0), PI32)
    cot = D(LM.cosf(r), LM.sinf(r))
    return [D(cot, a), 0.0, 0.0, 0.0, 0.0, cot, 0.0, 0.0,
            0.0, 0.0, D(A(n, f), S(n, f)), D(M(M(2.0, n), f), S(n, f)), 0.0, 0.0, -1.0, 0.0]


def glu_perspective(fov, a, n, f):
    """gluPerspective (GLU 9.0.3 project.c): double, cos / sin, rounded to float on loading."""
    rad = fov / 2 * math.pi / 180
    if not math.isfinite(rad):
        return None  # C's sin gives NaN; GLU then multiplies NaNs, which the case's NaNs match anyway
    dz, s = f - n, math.sin(rad)
    if dz == 0 or s == 0 or a == 0:
        return None
    cot = math.cos(rad) / s
    return [f32(cot / a), 0.0, 0.0, 0.0, 0.0, f32(cot), 0.0, 0.0,
            0.0, 0.0, f32(-(f + n) / dz), f32(-2 * n * f / dz), 0.0, 0.0, -1.0, 0.0]


def tan_perspective(fov, a, n, f):
    """The tan form older GLUs used: ymax = near tan(fov pi / 360) in double, xmax = ymax aspect,
    then glFrustum (Mesa's frustum, in float)."""
    if not math.isfinite(fov):
        return None
    ymax = n * math.tan(fov * math.pi / 360)
    return mesa_frustum(f32(-ymax * a), f32(ymax * a), f32(-ymax), f32(ymax), n, f)


def _mag(v):
    s = A(A(M(v[0], v[0]), M(v[1], v[1])), M(v[2], v[2]))
    return f32(math.sqrt(s)) if s == s else s


def _cross(a, b):
    return [S(M(a[1], b[2]), M(a[2], b[1])), S(M(a[2], b[0]), M(a[0], b[2])), S(M(a[0], b[1]), M(a[1], b[0]))]


def lookat(fr, to, up, glu_order=False):
    """gluLookAt's normalize and cross in float; Unity's matrix (columns right, up, forward,
    translation `from`), right = up x forward, the identity under lengths of 1e-5. glu_order
    takes GLU's side = forward x up negated instead (differs in the signs of zeros)."""
    r = [1.0, 0, 0, fr[0], 0, 1.0, 0, fr[1], 0, 0, 1.0, fr[2], 0, 0, 0, 1.0]
    eps = f32(1e-5)
    z = [S(to[i], fr[i]) for i in range(3)]
    m = _mag(z)
    if m < eps:
        return r
    z = [D(v, m) for v in z]
    x = [-v for v in _cross(z, up)] if glu_order else _cross(up, z)
    m = _mag(x)
    if m < eps:
        return r
    x = [D(v, m) for v in x]
    y = _cross(z, x)
    return [x[0], y[0], z[0], fr[0], x[1], y[1], z[1], fr[1], x[2], y[2], z[2], fr[2], 0, 0, 0, 1.0]


def angleaxis(a, ax):
    m = _mag(ax)
    if not m > f32(1e-6):
        return [0.0, 0.0, 0.0, 1.0]
    r = M(D(a, 360.0), PI32)
    k = D(LM.sinf(r), m)
    return [M(ax[0], k), M(ax[1], k), M(ax[2], k), LM.cosf(r)]


def mesa_inverse_3d_general(m):
    """invert_matrix_3d_general (Mesa 20.3.5 m_matrix.c); None where it fails."""
    g = lambda r, c: m[r * 4 + c]
    pos = neg = 0.0
    for t in (M(M(g(0, 0), g(1, 1)), g(2, 2)), M(M(g(1, 0), g(2, 1)), g(0, 2)), M(M(g(2, 0), g(0, 1)), g(1, 2)),
              M(M(-g(2, 0), g(1, 1)), g(0, 2)), M(M(-g(1, 0), g(0, 1)), g(2, 2)), M(M(-g(0, 0), g(2, 1)), g(1, 2))):
        if t >= 0:
            pos = A(pos, t)
        else:
            neg = A(neg, t)
    det = A(pos, neg)
    if not abs(det) >= 1e-25:
        return None
    det = D(1.0, det)
    c2 = lambda a, b, c, d: S(M(g(*a), g(*b)), M(g(*c), g(*d)))
    o = [0.0] * 16
    o[0] = M(c2((1, 1), (2, 2), (2, 1), (1, 2)), det)
    o[1] = M(-c2((0, 1), (2, 2), (2, 1), (0, 2)), det)
    o[2] = M(c2((0, 1), (1, 2), (1, 1), (0, 2)), det)
    o[4] = M(-c2((1, 0), (2, 2), (2, 0), (1, 2)), det)
    o[5] = M(c2((0, 0), (2, 2), (2, 0), (0, 2)), det)
    o[6] = M(-c2((0, 0), (1, 2), (1, 0), (0, 2)), det)
    o[8] = M(c2((1, 0), (2, 1), (2, 0), (1, 1)), det)
    o[9] = M(-c2((0, 0), (2, 1), (2, 0), (0, 1)), det)
    o[10] = M(c2((0, 0), (1, 1), (1, 0), (0, 1)), det)
    for r in range(3):
        o[r * 4 + 3] = -A(A(M(g(0, 3), o[r * 4]), M(g(1, 3), o[r * 4 + 1])), M(g(2, 3), o[r * 4 + 2]))
    o[15] = 1.0
    return o


def mesa_inverse(mm):
    """invert_matrix_general in float, a zero pivot giving zero (as mm-check.py)."""
    r0, r1, r2, r3 = [list(mm[r * 4:r * 4 + 4]) + [1.0 if k == r else 0.0 for k in range(4)] for r in range(4)]
    zero = [0.0] * 16
    if abs(r3[0]) > abs(r2[0]): r3, r2 = r2, r3
    if abs(r2[0]) > abs(r1[0]): r2, r1 = r1, r2
    if abs(r1[0]) > abs(r0[0]): r1, r0 = r0, r1
    if r0[0] == 0: return zero
    m1, m2, m3 = D(r1[0], r0[0]), D(r2[0], r0[0]), D(r3[0], r0[0])
    for k in range(1, 8):
        s = r0[k]
        if k >= 4 and s == 0: continue
        r1[k] = S(r1[k], M(m1, s)); r2[k] = S(r2[k], M(m2, s)); r3[k] = S(r3[k], M(m3, s))
    if abs(r3[1]) > abs(r2[1]): r3, r2 = r2, r3
    if abs(r2[1]) > abs(r1[1]): r2, r1 = r1, r2
    if r1[1] == 0: return zero
    m2, m3 = D(r2[1], r1[1]), D(r3[1], r1[1])
    for k in range(2, 8):
        s = r1[k]
        if k >= 4 and s == 0: continue
        r2[k] = S(r2[k], M(m2, s)); r3[k] = S(r3[k], M(m3, s))
    if abs(r3[2]) > abs(r2[2]): r3, r2 = r2, r3
    if r2[2] == 0: return zero
    m3 = D(r3[2], r2[2])
    for k in range(3, 8): r3[k] = S(r3[k], M(m3, r2[k]))
    if r3[3] == 0: return zero
    s = D(1.0, r3[3])
    for k in range(4, 8): r3[k] = M(r3[k], s)
    m2, s = r2[3], D(1.0, r2[2])
    for k in range(4, 8): r2[k] = M(s, S(r2[k], M(r3[k], m2)))
    for row in (r1, r0):
        m = row[3]
        for k in range(4, 8): row[k] = S(row[k], M(r3[k], m))
    m1, s = r1[2], D(1.0, r1[1])
    for k in range(4, 8): r1[k] = M(s, S(r1[k], M(r2[k], m1)))
    m0 = r0[2]
    for k in range(4, 8): r0[k] = S(r0[k], M(r2[k], m0))
    m0, s = r0[1], D(1.0, r0[0])
    for k in range(4, 8): r0[k] = M(s, S(r0[k], M(r1[k], m0)))
    return [v for r in (r0, r1, r2, r3) for v in r[4:8]]


def _mv(m, v):
    return [sum(m[r * 4 + k] * v[k] for k in range(4)) for r in range(4)]


def glu_project(cam, w):
    """gluProject in double from the logged matrices and pixel rect; Unity's z is the distance."""
    P, V, pr = cam['proj'], cam['w2c'], cam['prect']
    e = _mv(V, w + [1.0])
    c = _mv(P, e)
    if c[3] == 0:
        return None
    x, y = (c[0] / c[3]) * 0.5 + 0.5, (c[1] / c[3]) * 0.5 + 0.5
    return [f32(x * pr[2] + pr[0]), f32(y * pr[3] + pr[1]), f32(-e[2])]


def rule(path):
    lines = load(path)
    by = {}
    for kind, idx, f in lines:
        by.setdefault(kind, []).append((idx, f))

    def count(kind, fn, key):
        cases = by.get(kind, [])
        ok = sum(1 for _, c in cases if (lambda g: g is not None and same(g, c[key]))(fn(c)))
        return ok, len(cases)

    def show(name, r):
        print(f'{name:44} {r[0]}/{r[1]}')

    show('PO Ortho = Mesa _math_float_ortho', count('PO', lambda c: mesa_ortho(*c['in']), 'm'))
    show('PF Frustum = Mesa _math_matrix_frustum', count('PF', lambda c: mesa_frustum(*c['in']), 'm'))
    show('PP Perspective = measured (cosf/sinf)', count('PP', lambda c: perspective(*c['in']), 'm'))
    show('PP Perspective = GLU gluPerspective', count('PP', lambda c: glu_perspective(*c['in']), 'm'))
    show('PP Perspective = frustum of n tan(fov pi / 360)', count('PP', lambda c: tan_perspective(*c['in']), 'm'))
    show('PL LookAt = GLU basis, Unity order', count('PL', lambda c: lookat(c['from'], c['to'], c['up']), 'm'))
    show('PL LookAt = GLU basis, GLU order', count('PL', lambda c: lookat(c['from'], c['to'], c['up'], True), 'm'))
    show('PR AngleAxis = measured (sinf/cosf)', count('PR', lambda c: angleaxis(c['a'][0], c['axis']), 'aa'))
    pa = by.get('PA', [])
    ok_agree = sum(1 for _, c in pa if (mesa_inverse_3d_general(c['m']) is not None) == (c['ok'] == '1'))
    full = sum(1 for _, c in pa if c['ok'] == '1' and (lambda g: g is not None and same(g, c['r']))(mesa_inverse_3d_general(c['m'])))
    upper = sum(1 for _, c in pa if c['ok'] == '1' and (lambda g: g is not None and same(
        [g[r * 4 + k] for r in range(3) for k in range(4)], [c['r'][r * 4 + k] for r in range(3) for k in range(4)]))(mesa_inverse_3d_general(c['m'])))
    print(f'{"PA Inverse3DAffine = Mesa 3d_general":44} {full}/{sum(c["ok"] == "1" for _, c in pa)} invertible'
          f' (top three rows {upper}); success flag agrees {ok_agree}/{len(pa)}')
    cams = {idx[0]: c for idx, c in by.get('PC', [])}

    def cam_proj(c):
        if c['ortho'] == '1':
            s, a = c['size'][0], c['aspect'][0]
            return mesa_ortho(-M(s, a), M(s, a), -s, s, c['near'][0], c['far'][0])
        return perspective(c['fov'][0], c['aspect'][0], c['near'][0], c['far'][0])
    show('PC projectionMatrix = Perspective / Ortho', count('PC', cam_proj, 'proj'))
    show('PC cameraToWorldMatrix = inverse(w2c)', count('PC', lambda c: mesa_inverse(c['w2c']), 'c2w'))
    pcp = by.get('PCP', [])
    ok = sum(1 for idx, c in pcp if (lambda g: g is not None and same(g, c['w2s']))(glu_project(cams[idx[0]], c['w'])))
    show('PCP WorldToScreenPoint = gluProject (double)', (ok, len(pcp)))
    ok = sum(1 for idx, c in pcp if same([D(S(c['w2s'][0], cams[idx[0]]['prect'][0]), cams[idx[0]]['prect'][2]),
                                          D(S(c['w2s'][1], cams[idx[0]]['prect'][1]), cams[idx[0]]['prect'][3])], c['w2v'][:2]))
    show('PCP WorldToViewportPoint = (screen - x) / w', (ok, len(pcp)))


def diff(orig, ours):
    a = {(k, i): f for k, i, f in load(orig)}
    b = {(k, i): f for k, i, f in load(ours)}
    kinds = sorted({k for k, _ in a})
    for kind in kinds:
        keys = [key for key in a if key[0] == kind and key in b]
        differ = {}
        lines = 0
        for key in keys:
            bad = [f for f in a[key] if f in b[key] and isinstance(a[key][f], list) and not same(a[key][f], b[key][f])]
            for f in bad:
                differ[f] = differ.get(f, 0) + 1
            lines += bool(bad)
        missing = sum(1 for key in a if key[0] == kind and key not in b)
        print(f'{kind:4} {len(keys)} lines, {lines} differ' + (f', {missing} missing in ours' if missing else '')
              + (': ' + ', '.join(f'{f} {n}' for f, n in sorted(differ.items())) if differ else ''))


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == 'rule':
        rule(sys.argv[2])
    elif len(sys.argv) == 4 and sys.argv[1] == 'diff':
        diff(sys.argv[2], sys.argv[3])
    else:
        sys.exit(__doc__)
